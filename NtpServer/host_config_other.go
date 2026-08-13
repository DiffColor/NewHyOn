//go:build !windows

package main

import "fmt"

func prepareWindowsHost(hostOptions) error {
	return fmt.Errorf("automatic host setup is supported only on Windows")
}

func restoreWindowsHost(hostOptions) error {
	return fmt.Errorf("automatic host restore is supported only on Windows")
}
