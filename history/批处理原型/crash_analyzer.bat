@echo off
title Minecraft 崩溃分析器 - MCLB
color 0C
setlocal enabledelayedexpansion

:: 接收版本参数
set "SELECTED_VER=%~1"
set "MCLB_DIR=%~dp0"
set "MC_DIR=%MCLB_DIR%..\.minecraft"
for %%i in ("%MC_DIR%") do set "MC_DIR=%%~fi"
set "VERSION_DIR=%MC_DIR%\versions\%SELECTED_VER%"

if "%SELECTED_VER%"=="" (
    echo 用法: crash_analyzer.bat [版本名]
    pause >nul
    exit /b 1
)

cls
echo.
echo ════════════════════════════════════════════════════════════════
echo                      崩溃原因分析
echo ════════════════════════════════════════════════════════════════
echo.

:: 查找崩溃报告
set "CRASH_FILE="
if exist "%VERSION_DIR%\crash-reports" (
    pushd "%VERSION_DIR%\crash-reports"
    for /f "delims=" %%i in ('dir /b /o-d *.txt 2^>nul') do (
        set "CRASH_FILE=%VERSION_DIR%\crash-reports\%%i"
        goto :crash_found
    )
    popd
)
:crash_found

if defined CRASH_FILE (
    echo [√] 找到崩溃报告
    echo.
    echo ========== 错误摘要 ==========
    findstr /i "Exception Error" "%CRASH_FILE%" 2>nul | findstr /v "at " | more
    echo.
    echo ========== 可能原因 ==========
    
    :: 内存不足
    findstr /i "OutOfMemoryError" "%CRASH_FILE%" >nul
    if %errorlevel% equ 0 (
        echo [内存不足] 请增加分配的内存
    )
    
    :: Java 版本问题
    findstr /i "UnsupportedClassVersionError" "%CRASH_FILE%" >nul
    if %errorlevel% equ 0 (
        echo [Java版本] 请使用 Java 17 或更高版本
    )
    
    :: 显卡/OpenGL 问题
    findstr /i "GL error OpenGL" "%CRASH_FILE%" >nul
    if %errorlevel% equ 0 (
        echo [显卡问题] 请更新显卡驱动或关闭光影
    )
    
    :: 模组问题
    findstr /i "Mod.*failed.*load" "%CRASH_FILE%" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [模组问题] 检测到模组加载失败，请检查模组兼容性
    )
    
    :: 缺失依赖
    findstr /i "NoClassDefFoundError ClassNotFoundException" "%CRASH_FILE%" >nul
    if %errorlevel% equ 0 (
        echo [缺失依赖] 请检查是否缺少前置模组或 API
    )

) else if exist "%MCLB_DIR%logs\latest.log" (
    echo [i] 未找到崩溃报告，分析最新日志...
    echo.
    echo ========== 日志中的错误摘要 ==========
    findstr /i "Exception Error Fatal" "%MCLB_DIR%logs\latest.log" 2>nul | findstr /v "at " | more
    echo.
    echo 注意：如果没有明显致命错误，游戏可能正常退出。
) else (
    echo [×] 未找到崩溃报告或日志文件
)

echo.
echo ════════════════════════════════════════════════════════════════
echo 如需详细分析，请查看以下文件：
if defined CRASH_FILE echo   崩溃报告: %CRASH_FILE%
if exist "%MCLB_DIR%logs\latest.log" echo   最新日志: %MCLB_DIR%logs\latest.log
echo.
pause
exit /b 0