package main

import (
	"fmt"
	"strings"
	"unicode"
)

const defaultFirewallRuleName = "NtpServer"

type hostOptions struct {
	Port          int
	RemoteAddress string
	RuleName      string
}

func defaultHostOptions() hostOptions {
	return hostOptions{
		Port:          123,
		RemoteAddress: "Any",
		RuleName:      defaultFirewallRuleName,
	}
}

func validateRemoteAddress(value string) error {
	if strings.TrimSpace(value) == "" {
		return fmt.Errorf("firewall remote address must not be empty")
	}
	for _, character := range value {
		if unicode.IsControl(character) || unicode.IsSpace(character) || character == '"' || character == '\'' {
			return fmt.Errorf("firewall remote address contains an unsafe character")
		}
	}
	return nil
}

func validateHostOptions(options hostOptions) error {
	if options.Port < 1 || options.Port > 65535 {
		return fmt.Errorf("host UDP port must be between 1 and 65535")
	}
	if options.RuleName == "" {
		return fmt.Errorf("firewall rule name must not be empty")
	}
	if err := validateRemoteAddress(options.RemoteAddress); err != nil {
		return err
	}
	return nil
}
