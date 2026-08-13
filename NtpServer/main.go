package main

import (
	"bytes"
	"context"
	"flag"
	"fmt"
	"log"
	"net"
	"os"
	"os/signal"
	"runtime"
	"strconv"
	"syscall"
	"time"
)

var version = "dev"

func main() {
	config := defaultServerConfig()
	flags := flag.NewFlagSet("NtpServer", flag.ContinueOnError)
	var parseOutput bytes.Buffer
	flags.SetOutput(&parseOutput)
	listen := flags.String("listen", config.ListenAddress, "UDP listen address (for example :123 or 192.168.0.2:123)")
	stratum := flags.Uint("stratum", uint(config.Stratum), "NTP stratum (1-15)")
	referenceID := flags.String("ref-id", string(config.ReferenceID[:]), "four-character NTP reference ID")
	rootDispersion := flags.Duration("root-dispersion", time.Second, "estimated host clock error (0s-65535s)")
	logInterval := flags.Duration("log-interval", config.LogInterval, "health log interval; 0 disables periodic logs")
	logFile := flags.String("log-file", "ntp-server.log", "rotating log file path; empty disables file logging")
	noFileLog := flags.Bool("no-file-log", false, "disable file and fallback startup logging")
	logMaxMiB := flags.Int64("log-max-mib", 10, "maximum size of each log file in MiB")
	logBackups := flags.Int("log-backups", 3, "number of rotated log files to retain")
	showVersion := flags.Bool("version", false, "print version and exit")
	remoteAddress := flags.String("firewall-remote-address", "Any", "Windows firewall remote address (Any, LocalSubnet, CIDR, or comma-separated addresses)")
	skipHostSetup := flags.Bool("skip-host-setup", false, "skip automatic Windows Time and firewall setup")
	restoreHost := flags.Bool("restore-host", false, "restore Windows Time and remove the managed firewall rule, then exit")
	if err := flags.Parse(os.Args[1:]); err != nil {
		if err == flag.ErrHelp {
			fmt.Print(parseOutput.String())
			return
		}
		logCommandLineParseFailure(err, parseOutput.String(), *noFileLog || *logFile == "")
		os.Exit(2)
	}

	if *showVersion {
		fmt.Println(version)
		return
	}
	if *noFileLog {
		*logFile = ""
	}
	fileWriter, fallbackLog, logSetupError := openOperationalLog(*logFile, *logMaxMiB, *logBackups)
	defer fileWriter.Close()
	logger := log.New(logOutput(os.Stdout, fileWriter), "ntp-server ", log.Ldate|log.Ltime|log.LUTC|log.Lmsgprefix)
	log.SetOutput(logger.Writer())
	log.SetFlags(logger.Flags())
	log.SetPrefix(logger.Prefix())
	if logSetupError != nil {
		log.Fatalf("configure file logging: %v; fallback_log=%s", logSetupError, fallbackLog)
	}
	host := defaultHostOptions()
	host.RemoteAddress = *remoteAddress
	if err := validateRemoteAddress(host.RemoteAddress); err != nil {
		log.Fatalf("invalid -firewall-remote-address: %v", err)
	}
	if *restoreHost {
		if err := restoreWindowsHost(host); err != nil {
			log.Fatalf("restore Windows host: %v", err)
		}
		log.Printf("Windows host settings restored")
		return
	}
	if *stratum < 1 || *stratum > 15 {
		log.Fatalf("invalid -stratum %d: expected 1-15", *stratum)
	}
	if *rootDispersion < 0 || *rootDispersion > 65535*time.Second {
		log.Fatalf("invalid -root-dispersion %s: expected 0s-65535s", *rootDispersion)
	}
	if *logInterval < 0 {
		log.Fatalf("invalid -log-interval %s: expected zero or a positive duration", *logInterval)
	}
	ref := *referenceID
	if len(ref) != 4 || !isASCII(ref) {
		log.Fatalf("invalid -ref-id %q: expected exactly four ASCII characters", ref)
	}
	config.ListenAddress = *listen
	config.Stratum = byte(*stratum)
	copy(config.ReferenceID[:], ref)
	config.RootDispersion = durationToNTPShort(*rootDispersion)
	config.LogInterval = *logInterval
	if runtime.GOOS == "windows" && !*skipHostSetup {
		_, portText, err := net.SplitHostPort(config.ListenAddress)
		if err != nil {
			log.Fatalf("parse -listen %q for host setup: %v", config.ListenAddress, err)
		}
		port, err := strconv.Atoi(portText)
		if err != nil || port < 1 || port > 65535 {
			log.Fatalf("invalid UDP port in -listen %q", config.ListenAddress)
		}
		host.Port = port
		if err := prepareWindowsHost(host); err != nil {
			log.Fatalf("prepare Windows host: %v", err)
		}
		log.Printf("Windows host ready udp_port=%d firewall_remote=%s", host.Port, host.RemoteAddress)
	}

	server, err := newNTPServer(config)
	if err != nil {
		log.Fatal(err)
	}
	defer server.close()

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	log.Printf("started version=%s listen=%s stratum=%d ref_id=%s root_dispersion=%s", version, server.localAddr(), config.Stratum, ref, *rootDispersion)
	if err := server.serve(ctx); err != nil {
		log.Fatal(err)
	}
	stats := server.stats()
	log.Printf("stopped requests=%d responses=%d dropped=%d errors=%d uptime_end=%s",
		stats.Requests, stats.Responses, stats.Dropped, stats.Errors, time.Now().UTC().Format(time.RFC3339))
}

func isASCII(value string) bool {
	for index := 0; index < len(value); index++ {
		if value[index] < 0x20 || value[index] > 0x7e {
			return false
		}
	}
	return true
}

func logCommandLineParseFailure(parseError error, parseOutput string, disableFileLogging bool) {
	if !disableFileLogging {
		fallbackPath := startupFallbackLogPath()
		if writer, err := newRotatingFileWriter(fallbackPath, 1024*1024, 2); err == nil {
			logger := log.New(logOutput(os.Stderr, writer), "ntp-server ", log.Ldate|log.Ltime|log.LUTC|log.Lmsgprefix)
			logger.Printf("parse command line: %v; fallback_log=%s", parseError, fallbackPath)
			_ = writer.Close()
			return
		}
	}
	_, _ = fmt.Fprint(os.Stderr, parseOutput)
}
