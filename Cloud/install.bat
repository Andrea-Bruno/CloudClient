@echo off
:: Check if the script is running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

setlocal

:: Set the destination folder inside the default Program Files location
set "DEST=%ProgramFiles%\Cloud"

:: Create the destination folder if it doesn't exist
if not exist "%DEST%" (
    mkdir "%DEST%"
)

:: Copy all files from the current directory to the Cloud folder
xcopy "%~dp0*" "%DEST%\" /E /I /H /Y

:: Launch Cloud.exe from the new location
pushd "%DEST%"
start "" "Cloud.exe"
popd
