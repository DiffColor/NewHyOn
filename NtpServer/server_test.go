package main

import (
	"bytes"
	"context"
	"encoding/binary"
	"net"
	"os"
	"path/filepath"
	"sync"
	"testing"
	"time"
)

func makeClientRequest(version byte, transmit time.Time) []byte {
	request := make([]byte, ntpPacketSize)
	request[0] = version<<3 | modeClient
	request[2] = 6
	putTimestamp(request[40:48], transmit)
	return request
}

func TestBuildResponsePreservesVersionAndOriginateTimestamp(t *testing.T) {
	clientTransmit := time.Date(2026, 8, 12, 13, 0, 0, 123456789, time.UTC)
	received := clientTransmit.Add(2 * time.Millisecond)
	transmitted := received.Add(50 * time.Microsecond)
	request := makeClientRequest(4, clientTransmit)

	response, ok := buildResponse(request, received, transmitted, serverConfig{
		Stratum:     2,
		Precision:   -20,
		ReferenceID: [4]byte{'L', 'O', 'C', 'L'},
	})
	if !ok {
		t.Fatal("valid client request was rejected")
	}
	if got := response[0] >> 3 & 0x07; got != 4 {
		t.Fatalf("version = %d, want 4", got)
	}
	if got := response[0] & 0x07; got != modeServer {
		t.Fatalf("mode = %d, want %d", got, modeServer)
	}
	if got := response[1]; got != 2 {
		t.Fatalf("stratum = %d, want 2", got)
	}
	if string(response[12:16]) != "LOCL" {
		t.Fatalf("reference ID = %q, want LOCL", response[12:16])
	}
	if got, want := binary.BigEndian.Uint64(response[24:32]), binary.BigEndian.Uint64(request[40:48]); got != want {
		t.Fatalf("originate timestamp = %x, want %x", got, want)
	}
	if got := readTimestamp(response[32:40]); got.Sub(received).Abs() > time.Nanosecond {
		t.Fatalf("receive timestamp = %s, want %s", got, received)
	}
	if got := readTimestamp(response[40:48]); got.Sub(transmitted).Abs() > time.Nanosecond {
		t.Fatalf("transmit timestamp = %s, want %s", got, transmitted)
	}
}

func TestBuildResponseRejectsMalformedAndNonClientPackets(t *testing.T) {
	now := time.Now()
	cfg := defaultServerConfig()
	if _, ok := buildResponse(make([]byte, ntpPacketSize-1), now, now, cfg); ok {
		t.Fatal("short packet was accepted")
	}
	request := makeClientRequest(4, now)
	request[0] = 4<<3 | modeServer
	if _, ok := buildResponse(request, now, now, cfg); ok {
		t.Fatal("server-mode packet was accepted")
	}
}

func TestBuildResponseSupportsNTPv3(t *testing.T) {
	now := time.Now().UTC()
	response, ok := buildResponse(makeClientRequest(3, now), now, now, defaultServerConfig())
	if !ok {
		t.Fatal("valid NTPv3 request was rejected")
	}
	if got := response[0] >> 3 & 0x07; got != 3 {
		t.Fatalf("version = %d, want 3", got)
	}
}

func TestDefaultConfigAdvertisesConservativeLocalClock(t *testing.T) {
	config := defaultServerConfig()
	if config.Stratum != 10 || string(config.ReferenceID[:]) != "LOCL" {
		t.Fatalf("default identity = stratum %d ref %q, want 10/LOCL", config.Stratum, config.ReferenceID)
	}
	if config.ListenAddress != "127.0.0.1:123" {
		t.Fatalf("default listen address = %q, want loopback only", config.ListenAddress)
	}
	if config.RootDispersion != 1<<16 {
		t.Fatalf("default root dispersion = %#x, want one second", config.RootDispersion)
	}
}

func TestDurationToNTPShort(t *testing.T) {
	for value, want := range map[time.Duration]uint32{
		0:                       0,
		500 * time.Millisecond:  1 << 15,
		time.Second:             1 << 16,
		1500 * time.Millisecond: 1<<16 | 1<<15,
	} {
		if got := durationToNTPShort(value); got != want {
			t.Fatalf("durationToNTPShort(%s) = %#x, want %#x", value, got, want)
		}
	}
}

func TestASCIIValidation(t *testing.T) {
	if !isASCII("LOCL") {
		t.Fatal("ASCII reference ID was rejected")
	}
	if isASCII("éé") || isASCII("A\nBC") {
		t.Fatal("non-ASCII/control reference ID was accepted")
	}
}

func TestDefaultHostOptionsAllowAnyRemoteAddress(t *testing.T) {
	options := defaultHostOptions()
	if options.RemoteAddress != "Any" {
		t.Fatalf("default remote address = %q, want Any", options.RemoteAddress)
	}
	if options.Port != 123 || options.RuleName == "" {
		t.Fatalf("default host options = %+v", options)
	}
}

func TestValidateRemoteAddressRejectsUnsafeCommandValues(t *testing.T) {
	for _, value := range []string{"", "Any\nprofile=any", `Any"`, "Any\x00"} {
		if err := validateRemoteAddress(value); err == nil {
			t.Fatalf("unsafe remote address %q was accepted", value)
		}
	}
	for _, value := range []string{"Any", "LocalSubnet", "192.168.0.0/24", "192.168.0.10,192.168.0.11"} {
		if err := validateRemoteAddress(value); err != nil {
			t.Fatalf("valid remote address %q rejected: %v", value, err)
		}
	}
}

func TestRotatingFileWriterBoundsLogsAndPreservesBackups(t *testing.T) {
	logPath := filepath.Join(t.TempDir(), "logs", "ntp-server.log")
	writer, err := newRotatingFileWriter(logPath, 10, 2)
	if err != nil {
		t.Fatalf("newRotatingFileWriter: %v", err)
	}
	for _, entry := range []string{"first-line", "secondline", "third-line"} {
		if _, err := writer.Write([]byte(entry)); err != nil {
			t.Fatalf("write %q: %v", entry, err)
		}
	}
	if err := writer.Close(); err != nil {
		t.Fatalf("close: %v", err)
	}
	for path, want := range map[string]string{
		logPath:        "third-line",
		logPath + ".1": "secondline",
		logPath + ".2": "first-line",
	} {
		data, err := os.ReadFile(path)
		if err != nil {
			t.Fatalf("read %s: %v", path, err)
		}
		if string(data) != want {
			t.Fatalf("%s = %q, want %q", path, data, want)
		}
	}
}

func TestRotatingFileWriterRecoversAfterTransientRotationFailure(t *testing.T) {
	logPath := filepath.Join(t.TempDir(), "ntp-server.log")
	writer, err := newRotatingFileWriter(logPath, 10, 1)
	if err != nil {
		t.Fatalf("newRotatingFileWriter: %v", err)
	}
	defer writer.Close()
	if _, err := writer.Write([]byte("first-line")); err != nil {
		t.Fatalf("initial write: %v", err)
	}
	blockedBackup := logPath + ".1"
	if err := os.Mkdir(blockedBackup, 0o755); err != nil {
		t.Fatalf("create blocked backup: %v", err)
	}
	if err := os.WriteFile(filepath.Join(blockedBackup, "lock"), []byte("locked"), 0o644); err != nil {
		t.Fatalf("populate blocked backup: %v", err)
	}
	if _, err := writer.Write([]byte("secondline")); err == nil {
		t.Fatal("rotation unexpectedly succeeded with a non-empty backup directory")
	}
	if writer.file == nil {
		t.Fatal("active log was not reopened after rotation failure")
	}
	if err := os.RemoveAll(blockedBackup); err != nil {
		t.Fatalf("remove rotation obstacle: %v", err)
	}
	if _, err := writer.Write([]byte("third-line")); err != nil {
		t.Fatalf("write after transient rotation failure: %v", err)
	}
	data, err := os.ReadFile(logPath)
	if err != nil {
		t.Fatalf("read recovered active log: %v", err)
	}
	if string(data) != "third-line" {
		t.Fatalf("recovered active log = %q, want third-line", data)
	}
}

func TestRotatingFileWriterRecoversAfterCloseFailure(t *testing.T) {
	logPath := filepath.Join(t.TempDir(), "ntp-server.log")
	writer, err := newRotatingFileWriter(logPath, 10, 1)
	if err != nil {
		t.Fatalf("newRotatingFileWriter: %v", err)
	}
	defer writer.Close()
	if _, err := writer.Write([]byte("first-line")); err != nil {
		t.Fatalf("initial write: %v", err)
	}
	if err := writer.file.Close(); err != nil {
		t.Fatalf("inject closed file: %v", err)
	}
	if _, err := writer.Write([]byte("secondline")); err == nil {
		t.Fatal("rotation unexpectedly hid the injected close failure")
	}
	if writer.file == nil {
		t.Fatal("active log was not reopened after close failure")
	}
	if _, err := writer.Write([]byte("third-line")); err != nil {
		t.Fatalf("write after close failure: %v", err)
	}
	data, err := os.ReadFile(logPath)
	if err != nil {
		t.Fatalf("read recovered active log: %v", err)
	}
	if string(data) != "third-line" {
		t.Fatalf("recovered active log = %q, want third-line", data)
	}
}

type failingWriter struct{}

func (failingWriter) Write([]byte) (int, error) {
	return 0, os.ErrClosed
}

func TestLogOutputWritesFileWhenStdoutFails(t *testing.T) {
	var file bytes.Buffer
	output := independentLogWriter{file: &file, stdout: failingWriter{}}
	message := []byte("persistent diagnostic")
	if written, err := output.Write(message); err != nil || written != len(message) {
		t.Fatalf("Write = (%d, %v), want (%d, nil)", written, err, len(message))
	}
	if !bytes.Equal(file.Bytes(), message) {
		t.Fatalf("file output = %q, want %q", file.Bytes(), message)
	}
}

func TestOpenOperationalLogFallsBackForStartupDiagnostics(t *testing.T) {
	tempDir := t.TempDir()
	t.Setenv("TMPDIR", tempDir)
	blockedPath := filepath.Join(tempDir, "blocked-log-path")
	if err := os.Mkdir(blockedPath, 0o755); err != nil {
		t.Fatalf("create blocked log path: %v", err)
	}
	writer, fallbackPath, setupError := openOperationalLog(blockedPath, 10, 3)
	if setupError == nil {
		t.Fatal("unusable requested log path did not report a setup error")
	}
	if writer == nil {
		t.Fatal("fallback startup log writer was not created")
	}
	message := []byte("startup failure diagnostic")
	if _, err := writer.Write(message); err != nil {
		t.Fatalf("write fallback diagnostic: %v", err)
	}
	if err := writer.Close(); err != nil {
		t.Fatalf("close fallback writer: %v", err)
	}
	data, err := os.ReadFile(fallbackPath)
	if err != nil {
		t.Fatalf("read fallback startup log: %v", err)
	}
	if !bytes.Equal(data, message) {
		t.Fatalf("fallback startup log = %q, want %q", data, message)
	}
}

func TestOpenOperationalLogCanBeExplicitlyDisabled(t *testing.T) {
	writer, fallbackPath, setupError := openOperationalLog("", 0, 0)
	if writer != nil || fallbackPath != "" || setupError != nil {
		t.Fatalf("disabled file logging = (%v, %q, %v), want (nil, empty, nil)", writer, fallbackPath, setupError)
	}
}

func TestServerAnswersRepeatedUDPRequests(t *testing.T) {
	cfg := defaultServerConfig()
	cfg.ListenAddress = "127.0.0.1:0"
	cfg.LogInterval = 0

	server, err := newNTPServer(cfg)
	if err != nil {
		t.Fatalf("newNTPServer: %v", err)
	}
	ctx, cancel := context.WithCancel(context.Background())
	done := make(chan error, 1)
	go func() { done <- server.serve(ctx) }()
	t.Cleanup(func() {
		cancel()
		_ = server.close()
		select {
		case err := <-done:
			if err != nil {
				t.Errorf("serve returned error: %v", err)
			}
		case <-time.After(2 * time.Second):
			t.Error("server did not stop promptly")
		}
	})

	client, err := net.DialUDP("udp", nil, server.localAddr())
	if err != nil {
		t.Fatalf("DialUDP: %v", err)
	}
	defer client.Close()
	if err := client.SetDeadline(time.Now().Add(3 * time.Second)); err != nil {
		t.Fatalf("SetDeadline: %v", err)
	}

	response := make([]byte, ntpPacketSize)
	for i := 0; i < 1000; i++ {
		request := makeClientRequest(4, time.Now().UTC())
		if _, err := client.Write(request); err != nil {
			t.Fatalf("request %d write: %v", i, err)
		}
		n, err := client.Read(response)
		if err != nil {
			t.Fatalf("request %d read: %v", i, err)
		}
		if n != ntpPacketSize || response[0]&0x07 != modeServer {
			t.Fatalf("request %d invalid response: size=%d mode=%d", i, n, response[0]&0x07)
		}
	}
	stats := server.stats()
	if stats.Requests != 1000 || stats.Responses != 1000 || stats.Dropped != 0 {
		t.Fatalf("stats = %+v", stats)
	}
}

func TestServerSurvivesMalformedPacketsAndConcurrentClients(t *testing.T) {
	cfg := defaultServerConfig()
	cfg.ListenAddress = "127.0.0.1:0"
	cfg.LogInterval = 0

	server, err := newNTPServer(cfg)
	if err != nil {
		t.Fatalf("newNTPServer: %v", err)
	}
	ctx, cancel := context.WithCancel(context.Background())
	done := make(chan error, 1)
	go func() { done <- server.serve(ctx) }()
	defer func() {
		cancel()
		_ = server.close()
		if err := <-done; err != nil {
			t.Errorf("serve returned error: %v", err)
		}
	}()

	malformed, err := net.DialUDP("udp", nil, server.localAddr())
	if err != nil {
		t.Fatalf("dial malformed client: %v", err)
	}
	for i := 0; i < 100; i++ {
		if _, err := malformed.Write([]byte{0x23, byte(i)}); err != nil {
			t.Fatalf("malformed write %d: %v", i, err)
		}
	}
	_ = malformed.Close()

	const clients = 8
	const requestsPerClient = 250
	var wait sync.WaitGroup
	errors := make(chan error, clients)
	for clientIndex := 0; clientIndex < clients; clientIndex++ {
		wait.Add(1)
		go func() {
			defer wait.Done()
			client, err := net.DialUDP("udp", nil, server.localAddr())
			if err != nil {
				errors <- err
				return
			}
			defer client.Close()
			_ = client.SetDeadline(time.Now().Add(5 * time.Second))
			response := make([]byte, ntpPacketSize)
			for i := 0; i < requestsPerClient; i++ {
				if _, err := client.Write(makeClientRequest(4, time.Now().UTC())); err != nil {
					errors <- err
					return
				}
				if n, err := client.Read(response); err != nil || n != ntpPacketSize {
					if err == nil {
						errors <- &net.AddrError{Err: "invalid response size", Addr: server.localAddr().String()}
					} else {
						errors <- err
					}
					return
				}
			}
		}()
	}
	wait.Wait()
	close(errors)
	for err := range errors {
		t.Fatalf("concurrent client failed: %v", err)
	}

	deadline := time.Now().Add(2 * time.Second)
	for server.stats().Requests < 100+clients*requestsPerClient && time.Now().Before(deadline) {
		time.Sleep(time.Millisecond)
	}
	stats := server.stats()
	if stats.Requests != 2100 || stats.Responses != 2000 || stats.Dropped != 100 || stats.Errors != 0 {
		t.Fatalf("stats after malformed/concurrent traffic = %+v", stats)
	}
}

func TestServerRebasesFutureReferenceTimestampAfterClockMovesBackward(t *testing.T) {
	cfg := defaultServerConfig()
	cfg.ListenAddress = "127.0.0.1:0"
	cfg.LogInterval = 0
	server, err := newNTPServer(cfg)
	if err != nil {
		t.Fatalf("newNTPServer: %v", err)
	}
	server.referenceTime = time.Now().UTC().Add(24 * time.Hour)

	ctx, cancel := context.WithCancel(context.Background())
	done := make(chan error, 1)
	go func() { done <- server.serve(ctx) }()
	defer func() {
		cancel()
		_ = server.close()
		if err := <-done; err != nil {
			t.Errorf("serve returned error: %v", err)
		}
	}()

	client, err := net.DialUDP("udp", nil, server.localAddr())
	if err != nil {
		t.Fatalf("DialUDP: %v", err)
	}
	defer client.Close()
	_ = client.SetDeadline(time.Now().Add(2 * time.Second))
	if _, err := client.Write(makeClientRequest(4, time.Now().UTC())); err != nil {
		t.Fatalf("write request: %v", err)
	}
	response := make([]byte, ntpPacketSize)
	if _, err := client.Read(response); err != nil {
		t.Fatalf("read response: %v", err)
	}
	reference := readTimestamp(response[16:24])
	received := readTimestamp(response[32:40])
	if reference.After(received) {
		t.Fatalf("reference timestamp %s is after receive timestamp %s", reference, received)
	}
}
