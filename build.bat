@echo off
rem Build Space3VPN.exe bang csc.exe co san trong Windows (.NET Framework 4) - khong can cai them gi.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Khong tim thay csc.exe cua .NET Framework 4.
    exit /b 1
)
"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ /out:Space3VPN.exe /win32manifest:app.manifest /resource:space3.vpn,space3.vpn /r:System.Windows.Forms.dll /r:System.Drawing.dll Space3VPN.cs
if errorlevel 1 (
    echo BUILD THAT BAI
    if not defined CI pause
    exit /b 1
)
echo.
echo Xong: Space3VPN.exe
if not defined CI pause
