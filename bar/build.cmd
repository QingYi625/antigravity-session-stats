@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF
"%CSC%" /nologo /target:winexe /optimize+ /out:"%~dp0AntigravitySessionStatsBar.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:"%WPF%\UIAutomationClient.dll" /r:"%WPF%\UIAutomationTypes.dll" /r:"%WPF%\WindowsBase.dll" "%~dp0StatsBar.cs"
