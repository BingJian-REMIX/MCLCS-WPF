@echo off
title Java 自动安装程序 - MCLB
color 0E
setlocal enabledelayedexpansion

:: 检查是否已安装 Java 21+
where java >nul 2>&1
if %errorlevel% equ 0 (
    for /f "tokens=3 delims=. " %%a in ('java -version 2^>^&1 ^| findstr /i "version"') do (
        set "fullver=%%a"
        set "fullver=!fullver:"=!"
    )
    for /f "delims=." %%b in ("!fullver!") do set "java_major=%%b"
    if !java_major! GEQ 21 (
        echo Java 21+ 已安装，无需操作
        pause
        exit /b 0
    )
    echo 当前 Java 版本: !fullver! (主版本 !java_major!)，需要 21+
    echo 正在尝试安装 Java 21...
)

echo 正在尝试安装 Java 21...

:: ==================== 方案一：Oracle JDK 21 ====================
:try_oracle
echo [1/2] 尝试安装 Oracle JDK 21...
set "ORACLE_URL=https://download.oracle.com/java/21/latest/jdk-21_windows-x64_bin.exe"
set "INSTALLER=%TEMP%\jdk-21_windows-x64_bin.exe"

powershell -Command "Invoke-WebRequest -Uri '%ORACLE_URL%' -Headers @{'Cookie'='oraclelicense=accept-securebackup-cookie'} -OutFile '%INSTALLER%'" 2>nul

if not exist "%INSTALLER%" (
    echo Oracle 下载失败，尝试备用源...
    goto :try_temurin
)

for %%A in ("%INSTALLER%") do set "size=%%~zA"
if %size% LSS 1000000 (
    echo Oracle 下载文件不完整，尝试备用源...
    del "%INSTALLER%" 2>nul
    goto :try_temurin
)

start /wait "" "%INSTALLER%" /s /L "%TEMP%\oracle-jdk-install.log"
del "%INSTALLER%" 2>nul

:: 验证安装
call :check_java_version 21
if %errorlevel% equ 0 (
    echo Oracle JDK 21 安装成功
    goto :success
)

:: ==================== 方案二：Temurin（Adoptium）21 ====================
:try_temurin
echo [2/2] 尝试安装 Eclipse Temurin 21...
set "TEMURIN_URL=https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse"
set "INSTALLER=%TEMP%\temurin21_jdk_x64.msi"

powershell -Command "Invoke-WebRequest -Uri '%TEMURIN_URL%' -OutFile '%INSTALLER%'" 2>nul

if not exist "%INSTALLER%" (
    echo Temurin 下载失败
    goto :failed
)

msiexec /i "%INSTALLER%" /quiet /norestart
del "%INSTALLER%" 2>nul

:: 验证安装
call :check_java_version 21
if %errorlevel% equ 0 (
    echo Temurin 21 安装成功
    goto :success
)

:failed
echo.
echo [错误] Java 21 安装失败
echo 请手动安装 Java 21 后重试
echo 下载地址: https://adoptium.net/download/
pause
exit /b 1

:success
echo.
echo 正在刷新环境变量...
:: 尝试设置 JAVA_HOME
set "JDK_VER="
for /f "usebackq tokens=3" %%i in (`reg query "HKLM\SOFTWARE\Eclipse Adoptium\JDK" /v CurrentVersion 2^>nul`) do set "JDK_VER=%%i"
if not defined JDK_VER (
    for /f "usebackq tokens=3" %%i in (`reg query "HKLM\SOFTWARE\JavaSoft\JDK" /v CurrentVersion 2^>nul`) do set "JDK_VER=%%i"
)

if defined JDK_VER (
    for /f "usebackq tokens=2*" %%i in (`reg query "HKLM\SOFTWARE\Eclipse Adoptium\JDK\%JDK_VER%" /v Path 2^>nul ^| find "Path"`) do (
        setx JAVA_HOME "%%j" /M >nul
        setx PATH "%%j\bin;!PATH!" /M >nul
        goto :refresh
    )
    for /f "usebackq tokens=2*" %%i in (`reg query "HKLM\SOFTWARE\JavaSoft\JDK\%JDK_VER%" /v JavaHome 2^>nul ^| find "JavaHome"`) do (
        setx JAVA_HOME "%%j" /M >nul
        setx PATH "%%j\bin;!PATH!" /M >nul
    )
)

:refresh
for /f "usebackq tokens=1,2,*" %%i in (`reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PATH 2^>nul ^| find /i "PATH"`) do (
    set "SysPath=%%k"
)
set "PATH=%SysPath%;%PATH%"

echo Java 21 安装完成！
echo 请关闭此窗口后重新打开启动器
pause
exit /b 0

:: ==================== 子程序：检查 Java 主版本号 ====================
:check_java_version
set "required=%~1"
where java >nul 2>&1
if %errorlevel% neq 0 exit /b 1
for /f "tokens=3 delims=. " %%a in ('java -version 2^>^&1 ^| findstr /i "version"') do (
    set "verStr=%%a"
    set "verStr=!verStr:"=!"
)
for /f "delims=." %%b in ("!verStr!") do set "major=%%b"
if !major! GEQ %required% exit /b 0
exit /b 1