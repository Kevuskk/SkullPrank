@echo off
setlocal EnableExtensions
title Skull Prank - EMERGENCY STOP

set "STOP=%TEMP%\skull_prank.stop"
set "PIDF=%TEMP%\skull_prank.pid"
set "REST=%TEMP%\skull_restore.ps1"

echo [*] Sending stop signal...
>"%STOP%" echo stop

rem Let the payload notice the flag and run its own cleanup
rem (un-swaps mouse, re-shows taskbar, clears Caps Lock).
ping -n 3 127.0.0.1 >nul

rem --- kill by PID, but only if the image is still Skull.exe ---
set "PID="
if exist "%PIDF%" for /f "usebackq delims=" %%p in ("%PIDF%") do set "PID=%%p"

set "IMG="
if defined PID for /f "tokens=1 delims=," %%a in ('tasklist /FI "PID eq %PID%" /FO CSV /NH 2^>nul') do set "IMG=%%~a"

if /i "%IMG%"=="Skull.exe" (
    echo [*] Killing payload PID %PID% ...
    taskkill /F /T /PID %PID% >nul 2>&1
) else (
    echo [*] Payload already stopped.
)

rem --- close any leftover prank windows ---
echo [*] Closing leftover prank windows...
taskkill /F /FI "WINDOWTITLE eq SYSTEM BREACH // 01*" >nul 2>&1
taskkill /F /FI "WINDOWTITLE eq SYSTEM BREACH // 02*" >nul 2>&1
rem NOTE: only matches an elevated cmd titled exactly like the fake-wipe window.
taskkill /F /FI "WINDOWTITLE eq Administrator: C:\Windows\system32\cmd.exe" >nul 2>&1

rem --- safety net: un-swap mouse + re-show taskbar even after a hard kill ---
echo [*] Restoring mouse / taskbar...
>"%REST%" echo Add-Type -Name U -Namespace W -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SwapMouseButton(bool b); [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern System.IntPtr FindWindow(string c, string t); [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr h, int n);'
>>"%REST%" echo [void][W.U]::SwapMouseButton($false)
>>"%REST%" echo $h=[W.U]::FindWindow('Shell_TrayWnd',$null)
>>"%REST%" echo if($h -ne [System.IntPtr]::Zero){[void][W.U]::ShowWindow($h,5)}
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%REST%" >nul 2>&1
del /q "%REST%" >nul 2>&1

rem --- clean temp artifacts ---
echo [*] Cleaning temp files...
del /q "%TEMP%\skull_ascii.txt" >nul 2>&1
del /q "%TEMP%\skull_1.bat" >nul 2>&1
del /q "%TEMP%\skull_2.bat" >nul 2>&1
del /q "%TEMP%\skull_wipe.bat" >nul 2>&1
del /q "%TEMP%\skull_wipe_*.txt" >nul 2>&1
del /q "%TEMP%\skull_prank.pid" >nul 2>&1
del /q "%TEMP%\skull_prank.stop" >nul 2>&1

echo.
echo [OK] Everything stopped. No persistence, nothing runs on boot.
echo      If anything still looks odd, a reboot resets all runtime state.
echo.
pause
