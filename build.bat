@echo off
setlocal
set "ROOT=%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [!] csc.exe not found. Install .NET Framework 4.x.
  exit /b 1
)

if not exist "%ROOT%assets\skel.gif" (
  echo [!] assets\skel.gif is missing.
  exit /b 1
)

echo Building Skull.exe ...

rem Restart is ON by default. Add /define:PRANK_SAFE to disable it.
rem Add /define:PRANK_LOOP to repeat the finale instead of exiting.
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
 /r:System.dll ^
 /r:System.Core.dll ^
 /r:System.Drawing.dll ^
 /r:System.Windows.Forms.dll ^
 /resource:"%ROOT%assets\skel.gif",skel.gif ^
 /resource:"%ROOT%assets\sounds\shutdown.wav",shutdown.wav ^
 /resource:"%ROOT%assets\sounds\fakeencrypted.wav",fakeencrypted.wav ^
 /resource:"%ROOT%assets\sounds\glitch.wav",glitch.wav ^
 /win32icon:"%ROOT%assets\skull.ico" ^
 /out:"%ROOT%Skull.exe" ^
 "%ROOT%src\Program.cs"

if errorlevel 1 (
  echo [!] BUILD FAILED
  exit /b 1
)

echo [OK] Built %ROOT%Skull.exe
