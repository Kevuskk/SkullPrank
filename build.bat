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

rem Add /define:PRANK_REBOOT to let the fake BSOD actually restart the machine.
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
 /r:System.dll ^
 /r:System.Core.dll ^
 /r:System.Drawing.dll ^
 /r:System.Windows.Forms.dll ^
 /resource:"%ROOT%assets\skel.gif",skel.gif ^
 /out:"%ROOT%Skull.exe" ^
 "%ROOT%src\Program.cs"

if errorlevel 1 (
  echo [!] BUILD FAILED
  exit /b 1
)

echo [OK] Built %ROOT%Skull.exe
