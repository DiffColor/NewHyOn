package main

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sync"
)

type rotatingFileWriter struct {
	mu       sync.Mutex
	path     string
	maxBytes int64
	backups  int
	file     *os.File
	size     int64
}

func newRotatingFileWriter(path string, maxBytes int64, backups int) (*rotatingFileWriter, error) {
	if path == "" {
		return nil, nil
	}
	if maxBytes <= 0 {
		return nil, fmt.Errorf("log max size must be positive")
	}
	if backups < 1 {
		return nil, fmt.Errorf("log backups must be at least 1")
	}
	writer := &rotatingFileWriter{path: path, maxBytes: maxBytes, backups: backups}
	if err := writer.open(); err != nil {
		return nil, err
	}
	return writer, nil
}

func openOperationalLog(path string, maxMiB int64, backups int) (*rotatingFileWriter, string, error) {
	if path == "" {
		return nil, "", nil
	}
	var setupError error
	if maxMiB < 1 || maxMiB > 1024 || backups < 1 || backups > 100 {
		setupError = fmt.Errorf("-log-max-mib must be 1-1024 and -log-backups must be 1-100")
	} else {
		writer, err := newRotatingFileWriter(path, maxMiB*1024*1024, backups)
		if err == nil {
			return writer, "", nil
		}
		setupError = err
	}

	fallbackPath := startupFallbackLogPath()
	fallback, fallbackError := newRotatingFileWriter(fallbackPath, 1024*1024, 2)
	if fallbackError != nil {
		return nil, fallbackPath, fmt.Errorf("%v; open fallback log: %w", setupError, fallbackError)
	}
	return fallback, fallbackPath, setupError
}

func startupFallbackLogPath() string {
	return filepath.Join(os.TempDir(), "NtpServer-startup.log")
}

func (w *rotatingFileWriter) open() error {
	if directory := filepath.Dir(w.path); directory != "." {
		if err := os.MkdirAll(directory, 0o755); err != nil {
			return fmt.Errorf("create log directory: %w", err)
		}
	}
	file, err := os.OpenFile(w.path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0o644)
	if err != nil {
		return fmt.Errorf("open log file %s: %w", w.path, err)
	}
	info, err := file.Stat()
	if err != nil {
		_ = file.Close()
		return fmt.Errorf("stat log file %s: %w", w.path, err)
	}
	w.file = file
	w.size = info.Size()
	return nil
}

func (w *rotatingFileWriter) Write(data []byte) (int, error) {
	w.mu.Lock()
	defer w.mu.Unlock()
	if w.file == nil {
		if err := w.open(); err != nil {
			return 0, err
		}
	}
	if w.size > 0 && w.size+int64(len(data)) > w.maxBytes {
		if err := w.rotate(); err != nil {
			return 0, err
		}
	}
	written, err := w.file.Write(data)
	w.size += int64(written)
	return written, err
}

func (w *rotatingFileWriter) rotate() error {
	closeError := w.file.Close()
	w.file = nil
	recoverActive := func(rotationError error) error {
		if reopenError := w.open(); reopenError != nil {
			return fmt.Errorf("%v; reopen active log: %w", rotationError, reopenError)
		}
		return rotationError
	}
	if closeError != nil {
		return recoverActive(fmt.Errorf("close log for rotation: %w", closeError))
	}
	if err := os.Remove(fmt.Sprintf("%s.%d", w.path, w.backups)); err != nil && !os.IsNotExist(err) {
		return recoverActive(fmt.Errorf("remove oldest log backup: %w", err))
	}
	for index := w.backups - 1; index >= 1; index-- {
		oldPath := fmt.Sprintf("%s.%d", w.path, index)
		newPath := fmt.Sprintf("%s.%d", w.path, index+1)
		if err := os.Rename(oldPath, newPath); err != nil && !os.IsNotExist(err) {
			return recoverActive(fmt.Errorf("rotate log backup: %w", err))
		}
	}
	if err := os.Rename(w.path, w.path+".1"); err != nil && !os.IsNotExist(err) {
		return recoverActive(fmt.Errorf("rotate active log: %w", err))
	}
	return w.open()
}

func (w *rotatingFileWriter) Close() error {
	if w == nil {
		return nil
	}
	w.mu.Lock()
	defer w.mu.Unlock()
	if w.file == nil {
		return nil
	}
	err := w.file.Close()
	w.file = nil
	return err
}

type independentLogWriter struct {
	file   io.Writer
	stdout io.Writer
}

func (w independentLogWriter) Write(data []byte) (int, error) {
	if w.file == nil {
		return w.stdout.Write(data)
	}
	fileWritten, fileError := w.file.Write(data)
	stdoutWritten, stdoutError := len(data), error(nil)
	if w.stdout != nil {
		stdoutWritten, stdoutError = w.stdout.Write(data)
	}
	if fileError == nil && fileWritten == len(data) {
		return len(data), nil
	}
	if stdoutError == nil && stdoutWritten == len(data) {
		return len(data), nil
	}
	if fileError != nil {
		return fileWritten, fileError
	}
	return stdoutWritten, stdoutError
}

func logOutput(stdout io.Writer, fileWriter *rotatingFileWriter) io.Writer {
	if fileWriter == nil {
		return stdout
	}
	return independentLogWriter{file: fileWriter, stdout: stdout}
}
