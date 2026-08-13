package main

import (
	"context"
	"encoding/binary"
	"errors"
	"fmt"
	"log"
	"net"
	"sync"
	"sync/atomic"
	"time"
)

const (
	ntpPacketSize = 48
	modeClient    = 3
	modeServer    = 4
	ntpEpochDelta = 2208988800
)

type serverConfig struct {
	ListenAddress  string
	Stratum        byte
	Precision      int8
	ReferenceID    [4]byte
	RootDispersion uint32
	LogInterval    time.Duration
}

func defaultServerConfig() serverConfig {
	return serverConfig{
		ListenAddress:  "127.0.0.1:123",
		Stratum:        10,
		Precision:      -20,
		ReferenceID:    [4]byte{'L', 'O', 'C', 'L'},
		RootDispersion: 1 << 16,
		LogInterval:    time.Minute,
	}
}

type serverStats struct {
	Requests  uint64
	Responses uint64
	Dropped   uint64
	Errors    uint64
}

type ntpServer struct {
	conn          *net.UDPConn
	config        serverConfig
	referenceTime time.Time
	requests      atomic.Uint64
	responses     atomic.Uint64
	dropped       atomic.Uint64
	errors        atomic.Uint64
	closeOnce     sync.Once
}

func newNTPServer(config serverConfig) (*ntpServer, error) {
	if config.Stratum == 0 || config.Stratum > 15 {
		return nil, fmt.Errorf("stratum must be between 1 and 15")
	}
	address, err := net.ResolveUDPAddr("udp", config.ListenAddress)
	if err != nil {
		return nil, fmt.Errorf("resolve listen address: %w", err)
	}
	conn, err := net.ListenUDP("udp", address)
	if err != nil {
		return nil, fmt.Errorf("listen on %s: %w", config.ListenAddress, err)
	}
	// Larger kernel buffers absorb short LAN bursts without allocating a goroutine per request.
	_ = conn.SetReadBuffer(1 << 20)
	_ = conn.SetWriteBuffer(1 << 20)
	return &ntpServer{
		conn:          conn,
		config:        config,
		referenceTime: time.Now().UTC(),
	}, nil
}

func (s *ntpServer) localAddr() *net.UDPAddr {
	return s.conn.LocalAddr().(*net.UDPAddr)
}

func (s *ntpServer) close() error {
	var err error
	s.closeOnce.Do(func() { err = s.conn.Close() })
	return err
}

func (s *ntpServer) stats() serverStats {
	return serverStats{
		Requests:  s.requests.Load(),
		Responses: s.responses.Load(),
		Dropped:   s.dropped.Load(),
		Errors:    s.errors.Load(),
	}
}

func (s *ntpServer) serve(ctx context.Context) error {
	stopLogger := make(chan struct{})
	if s.config.LogInterval > 0 {
		go s.logStats(ctx, stopLogger)
	}
	defer close(stopLogger)

	// Closing the socket is the portable way to unblock ReadFromUDP on shutdown.
	readStopped := make(chan struct{})
	go func() {
		select {
		case <-ctx.Done():
			_ = s.close()
		case <-readStopped:
		}
	}()
	defer close(readStopped)

	packet := make([]byte, 512)
	backoff := 5 * time.Millisecond
	for {
		n, remote, err := s.conn.ReadFromUDP(packet)
		receivedAt := time.Now().UTC()
		if err != nil {
			if ctx.Err() != nil || errors.Is(err, net.ErrClosed) {
				return nil
			}
			s.errors.Add(1)
			if temporary, ok := err.(net.Error); ok && temporary.Temporary() {
				time.Sleep(backoff)
				if backoff < time.Second {
					backoff *= 2
				}
				continue
			}
			return fmt.Errorf("read UDP packet: %w", err)
		}
		backoff = 5 * time.Millisecond
		s.requests.Add(1)

		transmittedAt := time.Now().UTC()
		response, ok := buildResponse(packet[:n], receivedAt, transmittedAt, s.config)
		if !ok {
			s.dropped.Add(1)
			continue
		}
		// Manual or RTC correction can move the host clock backwards. Keep the
		// reference timestamp at or before the packet timestamps so strict NTP
		// clients do not reject an otherwise valid response.
		if s.referenceTime.After(receivedAt) {
			s.referenceTime = receivedAt
		}
		putTimestamp(response[16:24], s.referenceTime)
		if _, err := s.conn.WriteToUDP(response[:], remote); err != nil {
			s.errors.Add(1)
			continue
		}
		s.responses.Add(1)
	}
}

func (s *ntpServer) logStats(ctx context.Context, stopped <-chan struct{}) {
	ticker := time.NewTicker(s.config.LogInterval)
	defer ticker.Stop()
	var previousResponses uint64
	for {
		select {
		case <-ctx.Done():
			return
		case <-stopped:
			return
		case <-ticker.C:
			stats := s.stats()
			log.Printf("health requests=%d responses=%d dropped=%d errors=%d interval_responses=%d",
				stats.Requests, stats.Responses, stats.Dropped, stats.Errors, stats.Responses-previousResponses)
			previousResponses = stats.Responses
		}
	}
}

func buildResponse(request []byte, receivedAt, transmittedAt time.Time, config serverConfig) ([ntpPacketSize]byte, bool) {
	var response [ntpPacketSize]byte
	if len(request) < ntpPacketSize {
		return response, false
	}
	version := (request[0] >> 3) & 0x07
	mode := request[0] & 0x07
	if (version != 3 && version != 4) || mode != modeClient {
		return response, false
	}

	response[0] = version<<3 | modeServer // LI=0, client NTP version, server mode.
	response[1] = config.Stratum
	response[2] = request[2] // Preserve the client's polling interval.
	response[3] = byte(config.Precision)
	binary.BigEndian.PutUint32(response[4:8], 0) // Root delay: local LAN clock source.
	binary.BigEndian.PutUint32(response[8:12], config.RootDispersion)
	copy(response[12:16], config.ReferenceID[:])
	copy(response[24:32], request[40:48])
	putTimestamp(response[32:40], receivedAt)
	putTimestamp(response[40:48], transmittedAt)
	return response, true
}

func durationToNTPShort(value time.Duration) uint32 {
	seconds := uint64(value / time.Second)
	fraction := (uint64(value%time.Second) << 16) / uint64(time.Second)
	return uint32(seconds<<16 | fraction)
}

func putTimestamp(target []byte, value time.Time) {
	value = value.UTC()
	seconds := uint64(value.Unix() + ntpEpochDelta)
	fraction := (uint64(value.Nanosecond()) << 32) / 1_000_000_000
	binary.BigEndian.PutUint32(target[0:4], uint32(seconds))
	binary.BigEndian.PutUint32(target[4:8], uint32(fraction))
}

func readTimestamp(source []byte) time.Time {
	seconds := int64(binary.BigEndian.Uint32(source[0:4])) - ntpEpochDelta
	fraction := uint64(binary.BigEndian.Uint32(source[4:8]))
	nanoseconds := int64((fraction * 1_000_000_000) >> 32)
	return time.Unix(seconds, nanoseconds).UTC()
}
