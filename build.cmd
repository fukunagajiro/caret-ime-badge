@echo off
setlocal
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set GAC=C:\Windows\Microsoft.NET\assembly\GAC_MSIL
if not exist build mkdir build
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:x64 /out:build\cursor-ime-mode.exe ^
  /win32manifest:app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:Accessibility.dll ^
  /r:"%GAC%\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll" ^
  /r:"%GAC%\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll" ^
  /r:"%GAC%\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" ^
  src\*.cs tests\*.cs
