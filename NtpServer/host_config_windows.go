//go:build windows

package main

import (
	"errors"
	"fmt"
	"net"
	"os/exec"
	"strconv"
	"strings"
	"syscall"
	"time"
	"unsafe"
)

const (
	serviceStopped     = 1
	serviceRunning     = 4
	serviceStopControl = 1
	serviceQueryStatus = 0x0004
	serviceStart       = 0x0010
	serviceStop        = 0x0020
	scManagerConnect   = 0x0001
)

var (
	advapi32           = syscall.NewLazyDLL("advapi32.dll")
	openSCManagerW     = advapi32.NewProc("OpenSCManagerW")
	openServiceW       = advapi32.NewProc("OpenServiceW")
	closeServiceHandle = advapi32.NewProc("CloseServiceHandle")
	queryServiceStatus = advapi32.NewProc("QueryServiceStatus")
	controlService     = advapi32.NewProc("ControlService")
	startServiceW      = advapi32.NewProc("StartServiceW")
	shell32            = syscall.NewLazyDLL("shell32.dll")
	isUserAnAdmin      = shell32.NewProc("IsUserAnAdmin")
)

type serviceStatus struct {
	ServiceType             uint32
	CurrentState            uint32
	ControlsAccepted        uint32
	Win32ExitCode           uint32
	ServiceSpecificExitCode uint32
	CheckPoint              uint32
	WaitHint                uint32
}

type savedHostState struct {
	StartType  uint32
	WasRunning bool
}

const stateRegistryKey = `HKLM\SOFTWARE\TurtleLab\NtpServer`

func prepareWindowsHost(options hostOptions) error {
	if err := validateHostOptions(options); err != nil {
		return err
	}
	if admin, _, _ := isUserAnAdmin.Call(); admin == 0 {
		return fmt.Errorf("automatic host setup requires administrator privileges")
	}

	state, found, err := loadSavedHostState()
	if err != nil {
		return err
	}
	createdState := false
	if !found {
		startType, err := queryServiceStartType("W32Time")
		if err != nil {
			return fmt.Errorf("query W32Time startup type: %w", err)
		}
		running, err := isServiceRunning("W32Time")
		if err != nil {
			return fmt.Errorf("query W32Time status: %w", err)
		}
		state = savedHostState{StartType: startType, WasRunning: running}
		if err := saveHostState(state); err != nil {
			return fmt.Errorf("save original W32Time state: %w", err)
		}
		createdState = true
	}

	if err := stopAndDisableService("W32Time"); err != nil {
		if createdState {
			if rollbackError := rollbackCreatedHostState(state); rollbackError != nil {
				return fmt.Errorf("prepare W32Time: %v; rollback: %v", err, rollbackError)
			}
		}
		return err
	}
	if err := waitForUDPPortAvailable(options.Port, 10*time.Second); err != nil {
		if createdState {
			if rollbackError := rollbackCreatedHostState(state); rollbackError != nil {
				return fmt.Errorf("wait for UDP port: %v; rollback: %v", err, rollbackError)
			}
		}
		return err
	}
	if err := configureWindowsFirewall(options); err != nil {
		if createdState {
			if rollbackError := rollbackCreatedHostState(state); rollbackError != nil {
				return fmt.Errorf("configure firewall: %v; rollback: %v", err, rollbackError)
			}
		}
		return err
	}
	return nil
}

func restoreWindowsHost(options hostOptions) error {
	if err := validateHostOptions(options); err != nil {
		return err
	}
	if admin, _, _ := isUserAnAdmin.Call(); admin == 0 {
		return fmt.Errorf("host restore requires administrator privileges")
	}

	state, found, stateError := loadSavedHostState()
	if stateError != nil {
		return stateError
	}
	if !found {
		return deleteWindowsFirewall(options.RuleName)
	}
	serviceRestored, err := serviceStateMatches("W32Time", state)
	if err != nil {
		return fmt.Errorf("query current host service state: %w", err)
	}
	if !serviceRestored {
		if state.WasRunning {
			if err := waitForUDPPortAvailable(options.Port, 10*time.Second); err != nil {
				return fmt.Errorf("restore host: %w", err)
			}
		}
		if err := restoreServiceState("W32Time", state); err != nil {
			return fmt.Errorf("restore host service: %w", err)
		}
		if err := verifyServiceState("W32Time", state, 10*time.Second); err != nil {
			return fmt.Errorf("verify restored host service: %w", err)
		}
	}
	if err := deleteWindowsFirewall(options.RuleName); err != nil {
		return err
	}
	if err := deleteSavedHostState(); err != nil {
		return err
	}
	return nil
}

func rollbackCreatedHostState(state savedHostState) error {
	if err := restoreServiceState("W32Time", state); err != nil {
		return fmt.Errorf("restore service: %w", err)
	}
	if err := verifyServiceState("W32Time", state, 10*time.Second); err != nil {
		return fmt.Errorf("verify service: %w", err)
	}
	if err := deleteSavedHostState(); err != nil {
		return fmt.Errorf("remove saved state: %w", err)
	}
	return nil
}

func waitForUDPPortAvailable(port int, timeout time.Duration) error {
	deadline := time.Now().Add(timeout)
	address, err := net.ResolveUDPAddr("udp", ":"+strconv.Itoa(port))
	if err != nil {
		return fmt.Errorf("resolve UDP port %d: %w", port, err)
	}
	for {
		connection, err := net.ListenUDP("udp", address)
		if err == nil {
			return connection.Close()
		}
		if time.Now().After(deadline) {
			return fmt.Errorf("UDP %d remains occupied after %s: %w", port, timeout, err)
		}
		time.Sleep(250 * time.Millisecond)
	}
}

func configureWindowsFirewall(options hostOptions) error {
	show := exec.Command("netsh.exe", "advfirewall", "firewall", "show", "rule", "name="+options.RuleName)
	if err := show.Run(); err == nil {
		return runWindowsCommand("netsh.exe", "advfirewall", "firewall", "set", "rule", "name="+options.RuleName, "new",
			"dir=in", "action=allow", "enable=yes", "profile=any",
			"protocol=UDP", "localport="+strconv.Itoa(options.Port),
			"remoteip="+options.RemoteAddress)
	}
	return runWindowsCommand("netsh.exe", "advfirewall", "firewall", "add", "rule",
		"name="+options.RuleName,
		"dir=in", "action=allow", "enable=yes", "profile=any",
		"protocol=UDP", "localport="+strconv.Itoa(options.Port),
		"remoteip="+options.RemoteAddress)
}

func deleteWindowsFirewall(ruleName string) error {
	command := exec.Command("netsh.exe", "advfirewall", "firewall", "delete", "rule", "name="+ruleName)
	output, err := command.CombinedOutput()
	if err != nil {
		text := strings.ToLower(string(output))
		if strings.Contains(text, "no rules match") || strings.Contains(text, "규칙") && strings.Contains(text, "없") {
			return nil
		}
		return fmt.Errorf("delete firewall rule: %w: %s", err, strings.TrimSpace(string(output)))
	}
	return nil
}

func stopAndDisableService(name string) error {
	running, err := isServiceRunning(name)
	if err != nil {
		return fmt.Errorf("query %s: %w", name, err)
	}
	if running {
		if err := stopService(name); err != nil {
			return fmt.Errorf("stop %s: %w", name, err)
		}
	}
	if err := runWindowsCommand("sc.exe", "config", name, "start=", "disabled"); err != nil {
		return fmt.Errorf("disable %s: %w", name, err)
	}
	return nil
}

func restoreServiceState(name string, state savedHostState) error {
	startValue := map[uint32]string{2: "auto", 3: "demand", 4: "disabled"}[state.StartType]
	if startValue == "" {
		return fmt.Errorf("unsupported saved service start type %d", state.StartType)
	}
	if err := runWindowsCommand("sc.exe", "config", name, "start=", startValue); err != nil {
		return err
	}
	if state.WasRunning {
		return startService(name)
	}
	running, err := isServiceRunning(name)
	if err != nil {
		return err
	}
	if running {
		return stopService(name)
	}
	return nil
}

func verifyServiceState(name string, state savedHostState, timeout time.Duration) error {
	deadline := time.Now().Add(timeout)
	for {
		matches, err := serviceStateMatches(name, state)
		if err != nil {
			return err
		}
		if matches {
			return nil
		}
		if time.Now().After(deadline) {
			return fmt.Errorf("service state did not become running=%t start_type=%d within %s", state.WasRunning, state.StartType, timeout)
		}
		time.Sleep(200 * time.Millisecond)
	}
}

func serviceStateMatches(name string, state savedHostState) (bool, error) {
	running, err := isServiceRunning(name)
	if err != nil {
		return false, err
	}
	startType, err := queryServiceStartType(name)
	if err != nil {
		return false, err
	}
	return running == state.WasRunning && startType == state.StartType, nil
}

func queryServiceStartType(name string) (uint32, error) {
	output, err := exec.Command("reg.exe", "query", `HKLM\SYSTEM\CurrentControlSet\Services\`+name, "/v", "Start").CombinedOutput()
	if err != nil {
		return 0, fmt.Errorf("reg query: %w: %s", err, strings.TrimSpace(string(output)))
	}
	return parseRegistryDWORD(output)
}

func loadSavedHostState() (savedHostState, bool, error) {
	startOutput, err := exec.Command("reg.exe", "query", stateRegistryKey, "/v", "W32TimeStartType").CombinedOutput()
	if err != nil {
		return savedHostState{}, false, nil
	}
	startType, err := parseRegistryDWORD(startOutput)
	if err != nil {
		return savedHostState{}, false, err
	}
	runningOutput, err := exec.Command("reg.exe", "query", stateRegistryKey, "/v", "W32TimeWasRunning").CombinedOutput()
	if err != nil {
		return savedHostState{}, false, fmt.Errorf("read saved W32Time running state: %w", err)
	}
	running, err := parseRegistryDWORD(runningOutput)
	if err != nil {
		return savedHostState{}, false, err
	}
	return savedHostState{StartType: startType, WasRunning: running != 0}, true, nil
}

func saveHostState(state savedHostState) error {
	running := "0"
	if state.WasRunning {
		running = "1"
	}
	if err := runWindowsCommand("reg.exe", "add", stateRegistryKey, "/v", "W32TimeWasRunning", "/t", "REG_DWORD", "/d", running, "/f"); err != nil {
		return err
	}
	if err := runWindowsCommand("reg.exe", "add", stateRegistryKey, "/v", "W32TimeStartType", "/t", "REG_DWORD", "/d", strconv.FormatUint(uint64(state.StartType), 10), "/f"); err != nil {
		_ = deleteSavedHostState()
		return err
	}
	return nil
}

func deleteSavedHostState() error {
	command := exec.Command("reg.exe", "delete", stateRegistryKey, "/f")
	output, err := command.CombinedOutput()
	if err != nil {
		return fmt.Errorf("delete saved host state: %w: %s", err, strings.TrimSpace(string(output)))
	}
	return nil
}

func parseRegistryDWORD(output []byte) (uint32, error) {
	fields := strings.Fields(string(output))
	for index := len(fields) - 1; index >= 0; index-- {
		value := strings.TrimSpace(fields[index])
		if strings.HasPrefix(strings.ToLower(value), "0x") {
			parsed, err := strconv.ParseUint(value[2:], 16, 32)
			return uint32(parsed), err
		}
	}
	return 0, fmt.Errorf("REG_DWORD value not found in registry output")
}

func isServiceRunning(name string) (bool, error) {
	handle, err := openService(name, serviceQueryStatus)
	if err != nil {
		return false, err
	}
	defer closeServiceHandle.Call(handle)
	var status serviceStatus
	result, _, callError := queryServiceStatus.Call(handle, uintptr(unsafe.Pointer(&status)))
	if result == 0 {
		return false, callError
	}
	return status.CurrentState == serviceRunning, nil
}

func stopService(name string) error {
	handle, err := openService(name, serviceStop|serviceQueryStatus)
	if err != nil {
		return err
	}
	defer closeServiceHandle.Call(handle)
	var status serviceStatus
	result, _, callError := controlService.Call(handle, serviceStopControl, uintptr(unsafe.Pointer(&status)))
	if result == 0 {
		return callError
	}
	deadline := time.Now().Add(10 * time.Second)
	for time.Now().Before(deadline) {
		result, _, callError = queryServiceStatus.Call(handle, uintptr(unsafe.Pointer(&status)))
		if result == 0 {
			return callError
		}
		if status.CurrentState == serviceStopped {
			return nil
		}
		time.Sleep(200 * time.Millisecond)
	}
	return fmt.Errorf("service did not stop within 10 seconds")
}

func startService(name string) error {
	handle, err := openService(name, serviceStart|serviceQueryStatus)
	if err != nil {
		return err
	}
	defer closeServiceHandle.Call(handle)
	result, _, callError := startServiceW.Call(handle, 0, 0)
	if result == 0 {
		if errors.Is(callError, syscall.Errno(1056)) {
			return nil
		}
		return callError
	}
	return nil
}

func openService(name string, access uintptr) (uintptr, error) {
	manager, _, callError := openSCManagerW.Call(0, 0, scManagerConnect)
	if manager == 0 {
		return 0, callError
	}
	defer closeServiceHandle.Call(manager)
	namePointer, err := syscall.UTF16PtrFromString(name)
	if err != nil {
		return 0, err
	}
	handle, _, callError := openServiceW.Call(manager, uintptr(unsafe.Pointer(namePointer)), access)
	if handle == 0 {
		return 0, callError
	}
	return handle, nil
}

func runWindowsCommand(name string, arguments ...string) error {
	command := exec.Command(name, arguments...)
	output, err := command.CombinedOutput()
	if err != nil {
		return fmt.Errorf("%s %s: %w: %s", name, strings.Join(arguments, " "), err, strings.TrimSpace(string(output)))
	}
	return nil
}
