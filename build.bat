@echo off
setlocal
cd /d "%~dp0"

title Compilando SysDiag

echo ==========================================================
echo   Compilacion de SysDiag
echo ==========================================================
echo.

set "SDK_DIR=%LOCALAPPDATA%\SysDiag\dotnet-sdk"
set "DOTNET_EXE=dotnet"
set "NEED_DOWNLOAD=0"

echo [1/3] Verificando el SDK de .NET...

if exist "%SDK_DIR%\dotnet.exe" (
    set "DOTNET_EXE=%SDK_DIR%\dotnet.exe"
    set "PATH=%SDK_DIR%;%PATH%"
    set "DOTNET_ROOT=%SDK_DIR%"
    echo       Usando la copia propia instalada antes en %SDK_DIR%
    goto sdk_ready
)

where dotnet >nul 2>&1
if errorlevel 1 (
    set "NEED_DOWNLOAD=1"
    goto do_download
)

set "SYS_VER="
set "SYS_MAJOR="
for /f "delims=" %%v in ('dotnet --version 2^>nul') do set "SYS_VER=%%v"
if defined SYS_VER for /f "tokens=1 delims=." %%m in ("%SYS_VER%") do set "SYS_MAJOR=%%m"

if not defined SYS_MAJOR set "NEED_DOWNLOAD=1"
if defined SYS_MAJOR if %SYS_MAJOR% LSS 8 set "NEED_DOWNLOAD=1"

if "%NEED_DOWNLOAD%"=="0" (
    echo       SDK del sistema encontrado: version %SYS_VER%
    goto sdk_ready
)

:do_download
echo       No se encontro un SDK de .NET 8 o superior en este equipo.
echo       Se descargara e instalara solo: sin permisos de
echo       administrador, en una carpeta propia de SysDiag.
echo.

where powershell >nul 2>&1
if errorlevel 1 (
    echo [x] Hace falta PowerShell para la descarga automatica.
    echo     Instala el SDK a mano desde https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

if not exist "%SDK_DIR%" mkdir "%SDK_DIR%" >nul 2>&1

echo       Descargando el instalador oficial de Microsoft...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; $ErrorActionPreference='Stop'; Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '%TEMP%\dotnet-install.ps1'"
if errorlevel 1 goto dl_error

echo       Instalando el SDK de .NET 8 ^(puede tardar unos minutos^)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\dotnet-install.ps1" -Channel 8.0 -InstallDir "%SDK_DIR%" -NoPath
if errorlevel 1 goto dl_error

if not exist "%SDK_DIR%\dotnet.exe" goto dl_error

set "DOTNET_EXE=%SDK_DIR%\dotnet.exe"
set "PATH=%SDK_DIR%;%PATH%"
set "DOTNET_ROOT=%SDK_DIR%"
echo       SDK instalado en %SDK_DIR%

:sdk_ready
echo.
"%DOTNET_EXE%" --version
if errorlevel 1 goto dl_error
echo.

echo [2/3] Restaurando dependencias ^(se descargan del repositorio oficial de NuGet^)...
"%DOTNET_EXE%" restore SysDiag.csproj
if errorlevel 1 goto build_error

echo.
echo [3/3] Compilando en un unico ejecutable autocontenido...
"%DOTNET_EXE%" publish SysDiag.csproj -c Release -o publish
if errorlevel 1 goto build_error

echo.
echo ==========================================================
echo   Listo. El ejecutable quedo en:
echo   %CD%\publish\SysDiag.exe
echo ==========================================================
echo.
echo Es autocontenido: no necesita nada mas instalado para
echo funcionar en otro Windows 10/11 de 64 bits.
echo.
pause
exit /b 0

:dl_error
echo.
echo [x] No se pudo descargar o instalar el SDK. Revisa tu conexion a internet.
echo     Tambien puedes instalarlo a mano desde https://dotnet.microsoft.com/download/dotnet/8.0
echo.
pause
exit /b 1

:build_error
echo.
echo [x] La compilacion fallo. Revisa los mensajes de arriba.
echo.
pause
exit /b 1
