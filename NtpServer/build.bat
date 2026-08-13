@echo off
setlocal EnableExtensions EnableDelayedExpansion

where go >nul 2>nul
if errorlevel 1 (
    echo Go 1.23 or later is required. 1>&2
    exit /b 1
)

set "PROJECT_DIR=%~dp0"
set "OUTPUT_DIR=%PROJECT_DIR%bin"
set "VERSION=%~1"
if not defined VERSION set "VERSION=1.0.0"

pushd "%PROJECT_DIR%" || exit /b 1

go test ./...
if errorlevel 1 goto :error

for %%T in (windows-amd64 windows-arm64 linux-amd64 linux-arm64 macos-amd64 macos-arm64) do (
    for /f "tokens=1,2 delims=-" %%O in ("%%T") do (
        set "TARGET_DIR=%OUTPUT_DIR%\%%T"
        if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"
        if errorlevel 1 goto :error

        set "OUTPUT_PATH=!TARGET_DIR!\NtpServer"
        if "%%O"=="windows" set "OUTPUT_PATH=!OUTPUT_PATH!.exe"

        echo Building %%O/%%P...
        set "CGO_ENABLED=0"
        set "GOOS=%%O"
        if "%%O"=="macos" set "GOOS=darwin"
        set "GOARCH=%%P"
        go build -buildvcs=false -trimpath -ldflags "-s -w -X main.version=%VERSION%" -o "!OUTPUT_PATH!" .
        if errorlevel 1 goto :error
    )
)

popd
echo Built Windows, Linux, and macOS binaries under "%OUTPUT_DIR%".
exit /b 0

:error
set "EXIT_CODE=%ERRORLEVEL%"
popd
if "%EXIT_CODE%"=="0" set "EXIT_CODE=1"
exit /b %EXIT_CODE%
