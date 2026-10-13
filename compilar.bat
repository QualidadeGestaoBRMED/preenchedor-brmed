@echo off
rem Compila o Preenchedor usando o compilador que ja vem no Windows (.NET Framework 4).
rem Nao precisa instalar Visual Studio nem .NET SDK.
setlocal
cd /d "%~dp0"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo Nao encontrei o compilador do .NET Framework 4 neste computador.
  pause
  exit /b 1
)
set "WPF=%FW%\WPF"
if not exist saida mkdir saida
echo Compilando...
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /out:saida\PreenchedorBRMED.exe /win32manifest:app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\UIAutomationClient.dll" /r:"%WPF%\UIAutomationTypes.dll" *.cs
if errorlevel 1 (
  echo.
  echo A compilacao falhou. Veja as mensagens acima.
  pause
  exit /b 1
)
echo.
echo Pronto! O programa esta em: saida\PreenchedorBRMED.exe
pause
