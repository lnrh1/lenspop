@echo off
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF
if not exist "%WPF%\UIAutomationClient.dll" set WPF=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\WPF

if not exist dist mkdir dist
"%CSC%" -nologo -target:exe -main:LensPopTests.Harness -out:dist\test-harness.exe ^
  -win32manifest:dev\harness.manifest ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Web.Extensions.dll ^
  -r:"%WPF%\UIAutomationClient.dll" -r:"%WPF%\UIAutomationTypes.dll" -r:"%WPF%\WindowsBase.dll" ^
  src\Config.cs src\Providers.cs src\Thinking.cs src\Api.cs src\SearchEngines.cs ^
  src\Theme.cs src\PopupForm.cs src\ResultForm.cs src\ImageResultForm.cs src\SettingsForm.cs ^
  src\ImageTools.cs src\GrabTestForm.cs src\UiaGrab.cs src\MainForm.cs src\MouseHook.cs src\HotkeyHost.cs src\CaptureForm.cs src\ShellMenu.cs src\Program.cs ^
  dev\TestHarness.cs
if errorlevel 1 (
  echo [x] harness build failed
  exit /b 1
)
echo [ok] dist\test-harness.exe