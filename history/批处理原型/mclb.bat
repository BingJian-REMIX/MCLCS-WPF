@echo off
setlocal enabledelayedexpansion

:: ==================== 路径配置 ====================
set "MCLB_DIR=%~dp0"
set "MC_DIR=%MCLB_DIR%..\.minecraft"
for %%i in ("%MC_DIR%") do set "MC_DIR=%%~fi"
set "VERSIONS_DIR=%MC_DIR%\versions"
set "ASSETS_DIR=%MC_DIR%\assets"
set "LIBRARIES_DIR=%MC_DIR%\libraries"

:: 默认配置
set "DEFAULT_MEM=2048"
set "DEFAULT_USERNAME=Player"

:: 确保目录存在
if not exist "%MC_DIR%" mkdir "%MC_DIR%"
if not exist "%VERSIONS_DIR%" mkdir "%VERSIONS_DIR%"
if not exist "%ASSETS_DIR%" mkdir "%ASSETS_DIR%"
if not exist "%LIBRARIES_DIR%" mkdir "%LIBRARIES_DIR%"

:: ==================== 检查 Java ====================
:check_java
where java >nul 2>&1
if %errorlevel% neq 0 (
    cls
    echo [提示] 未检测到 Java 运行环境，正在自动安装...
    call "%MCLB_DIR%install_java.bat"
    if %errorlevel% neq 0 (
        color 0C
        echo.
        echo [错误] Java 安装失败，请手动安装后重试
        echo 下载地址: https://adoptium.net/download/
        pause >nul
        exit /b 1
    )
    call :refresh_env
)
goto :main_menu

:refresh_env
for /f "usebackq tokens=1,2,*" %%i in (`reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PATH 2^>nul ^| find /i "PATH"`) do (
    set "SysPath=%%k"
)
set "PATH=%SysPath%;%PATH%"
goto :eof

:: ==================== 主菜单 ====================
:main_menu
title MCLB Minecraft 启动器
color 07
cls
echo.
echo ════════════════════════════════════════════════════════════════
echo                     MCLB Minecraft 启动器 v1.0
echo ════════════════════════════════════════════════════════════════
echo.
echo [1] 启动游戏
echo [2] 安装新版本 (Vanilla/Fabric/Forge)
echo [3] 下载 Mod / 光影 / 材质包
echo [Q] 退出
echo.
set /p choice=请选择:

if /i "%choice%"=="1" goto :launch_game
if /i "%choice%"=="2" goto :install_version
if /i "%choice%"=="3" goto :download_menu
if /i "%choice%"=="q" exit /b
goto :main_menu

:: ==================== 下载菜单 ====================
:download_menu
cls
echo.
echo ════════════════════════════════════════════════════════════════
echo                      下载中心
echo ════════════════════════════════════════════════════════════════
echo.
echo [1] 下载 Mod
echo [2] 下载光影包
echo [3] 下载材质包
echo [Q] 返回主菜单
echo.
set /p dl_choice=请选择:

if /i "%dl_choice%"=="1" goto :download_mod
if /i "%dl_choice%"=="2" goto :download_shader
if /i "%dl_choice%"=="3" goto :download_resourcepack
if /i "%dl_choice%"=="q" goto :main_menu
goto :download_menu

:: ==================== 选择版本子程序 ====================
:select_version
set "SELECTED_VER="
set "SELECTED_VER_DIR="
cls
echo.
echo ==================== 已安装版本列表 ====================
set ver_count=0
for /d %%d in ("%VERSIONS_DIR%\*") do (
    set "ver_dir=%%d"
    set "ver_json=%%d\%%~nxd.json"
    if exist "!ver_json!" (
        for /f "usebackq tokens=*" %%i in (`powershell -Command "try { (Get-Content '!ver_json!' -Raw | ConvertFrom-Json).id } catch { '' }" 2^>nul`) do set "ver_id=%%i"
        if "!ver_id!"=="" set "ver_id=%%~nxd"
    ) else (
        set "ver_id=%%~nxd"
    )
    set /a ver_count+=1
    set "ver_name_!ver_count!=!ver_id!"
    set "ver_dir_!ver_count!=%%d"
    echo [!ver_count!] !ver_id!
)
if %ver_count%==0 (
    cls
    echo.
    echo 未安装任何版本，请先选择 [2] 安装新版本
    pause >nul
    set "SELECTED_VER="
    goto :eof
)
echo.
echo [0] 取消
echo.
set /p ver_choice=请选择版本编号:
if "%ver_choice%"=="0" goto :eof
if %ver_choice% lss 1 goto :select_version
if %ver_choice% gtr %ver_count% goto :select_version
call set "SELECTED_VER=%%ver_name_%ver_choice%%%"
call set "SELECTED_VER_DIR=%%ver_dir_%ver_choice%%%"
goto :eof

:: ==================== 启动游戏 ====================
:launch_game
call :select_version
if "%SELECTED_VER%"=="" goto :main_menu

set "VERSION_DIR=%SELECTED_VER_DIR%"

if not exist "%VERSION_DIR%\crash-reports" mkdir "%VERSION_DIR%\crash-reports"

:: 生成时间戳
for /f "tokens=2-4 delims=/ " %%a in ('date /t') do set "d=%%c-%%a-%%b"
for /f "tokens=1-2 delims=: " %%a in ('time /t') do set "t=%%a-%%b"
set "timestamp=!d!_!t!"
set "timestamp=!timestamp: =0!"

:: 归档旧崩溃报告
if exist "%VERSION_DIR%\crash-reports\*.txt" (
    set "archive_dir=%VERSION_DIR%\crash-reports\archive_!timestamp!"
    mkdir "!archive_dir!" 2>nul
    move "%VERSION_DIR%\crash-reports\*.txt" "!archive_dir!\" >nul
)

:: 检测 natives
if exist "%VERSION_DIR%\natives" (
    set "NATIVES=%VERSION_DIR%\natives"
) else if exist "%VERSION_DIR%\%SELECTED_VER%-natives-windows" (
    set "NATIVES=%VERSION_DIR%\%SELECTED_VER%-natives-windows"
) else if exist "%VERSION_DIR%\%SELECTED_VER%-natives" (
    set "NATIVES=%VERSION_DIR%\%SELECTED_VER%-natives"
) else (
    echo [警告] 未找到 natives，将尝试使用版本目录
    set "NATIVES=%VERSION_DIR%"
)
if not exist "!NATIVES!" (
    echo [错误] natives 目录不存在: !NATIVES!
    pause
    goto :main_menu
)

:: 内存和用户名
set /p mem=分配内存(MB) [%DEFAULT_MEM%]:
if "%mem%"=="" set mem=%DEFAULT_MEM%
set /p username=用户名 [%DEFAULT_USERNAME%]:
if "%username%"=="" set username=%DEFAULT_USERNAME%

:: 获取 assetIndex
powershell -Command "$json = Get-Content '%VERSION_DIR%\*.json' -Raw | ConvertFrom-Json; if ($json.assetIndex) { Write-Host $json.assetIndex.id }" > "%TEMP%\asset_idx.txt" 2>nul
set /p ASSET_INDEX=<"%TEMP%\asset_idx.txt"
del "%TEMP%\asset_idx.txt" 2>nul
if "%ASSET_INDEX%"=="" set "ASSET_INDEX=legacy"

:: 调用 PowerShell 启动
powershell -ExecutionPolicy Bypass -File "%MCLB_DIR%mclb_launcher.ps1" "%VERSION_DIR%" "%SELECTED_VER%" "%LIBRARIES_DIR%" "%NATIVES%" "%VERSION_DIR%" "%ASSETS_DIR%" "%ASSET_INDEX%" "%mem%" "%username%"
set "EXIT_CODE=%errorlevel%"

:: 检查崩溃报告
dir /b "%VERSION_DIR%\crash-reports\crash-*.txt" 2>nul | findstr "." >nul
if %errorlevel% equ 0 (set "CRASH_EXISTS=1") else (set "CRASH_EXISTS=0")

if %EXIT_CODE% neq 0 (
    echo.
    if !CRASH_EXISTS! equ 1 (
        echo [错误] 游戏异常退出 (退出码: %EXIT_CODE%)
        echo 检测到崩溃报告，正在分析...
        call "%MCLB_DIR%crash_analyzer.bat" "%SELECTED_VER%"
    ) else (
        echo 游戏已退出 (退出码: %EXIT_CODE%)
        echo 如果遇到问题，请查看日志
    )
) else (
    echo.
    echo 游戏正常退出。
)
goto :main_menu

:: ==================== 安装新版本 ====================
:install_version
set "ver_type="
set "mc_version="
cls
echo ════════════════════════════════════════════════════════════════
echo                     安装 Minecraft 版本
echo ════════════════════════════════════════════════════════════════
echo.
echo 支持的类型:
echo   [1] 原版 (Vanilla)
echo   [2] Fabric
echo   [3] Forge
echo   [Q] 取消
echo.
set /p ver_type=请选择加载器类型:
if /i "%ver_type%"=="q" goto :main_menu
if "%ver_type%x"=="x" (
    cls
    echo 请重新输入。
    pause >nul
    goto :install_version
)
set /p mc_version=请输入 Minecraft 版本号 (如 1.20.1, 1.21.1):
if /i "%mc_version%"=="q" goto :main_menu
if "%mc_version%x"=="x" (
    cls
    echo 请重新输入。
    pause >nul
    goto :install_version
)

:: 构建版本名称
if "%ver_type%"=="1" set "FULL_NAME=%mc_version%"
if "%ver_type%"=="2" set "FULL_NAME=%mc_version%-fabric"
if "%ver_type%"=="3" set "FULL_NAME=%mc_version%-forge"

set "VERSION_DIR=%VERSIONS_DIR%\%FULL_NAME%"
if exist "%VERSION_DIR%" (
    echo 版本已存在: %FULL_NAME%
    pause >nul
    goto :main_menu
)

mkdir "%VERSION_DIR%"
echo 正在安装 %FULL_NAME% ...

if "%ver_type%"=="1" call :install_vanilla "%mc_version%" "%FULL_NAME%"
if "%ver_type%"=="2" call :install_fabric "%mc_version%" "%FULL_NAME%"
if "%ver_type%"=="3" call :install_forge "%mc_version%" "%FULL_NAME%"

if exist "%VERSION_DIR%\%FULL_NAME%.jar" (
    echo 安装成功！版本目录: %VERSION_DIR%
) else (
    echo 安装失败，请检查网络或手动安装
    rmdir "%VERSION_DIR%" 2>nul
)
pause >nul
goto :main_menu

:: ==================== 安装原版 ====================
:install_vanilla
set "MC_VER=%~1"
set "FULL=%~2"
cls
echo 正在下载原版 %MC_VER% ...

:: 下载 version.json
powershell -Command "$manifest=Invoke-WebRequest -Uri 'https://piston-meta.mojang.com/mc/game/version_manifest_v2.json' -UseBasicParsing|ConvertFrom-Json;$ver=$manifest.versions|Where-Object{$_.id -eq '%MC_VER%'}|Select-Object -First 1;if(-not $ver){Write-Host '版本不存在';exit 1};$verJson=Invoke-WebRequest -Uri $ver.url -UseBasicParsing|ConvertFrom-Json;$verJson|ConvertTo-Json -Depth 100|Out-File '%VERSION_DIR%\%FULL%.json' -Encoding UTF8;Write-Host 'JSON 下载完成'"
if errorlevel 1 (echo JSON 下载失败 & pause & goto :eof)

:: 下载核心 JAR
powershell -Command "$json=Get-Content '%VERSION_DIR%\%FULL%.json' -Raw|ConvertFrom-Json;$clientUrl=$json.downloads.client.url;Invoke-WebRequest -Uri $clientUrl -OutFile '%VERSION_DIR%\%FULL%.jar';Write-Host '核心 JAR 下载完成'"
if errorlevel 1 (echo 核心 JAR 下载失败 & pause & goto :eof)

:: 下载 Libraries
echo 正在下载依赖库...
set "DL_LIBS_PS=%TEMP%\mclb_dl_libs.ps1"
> "%DL_LIBS_PS%" echo $json = Get-Content '%VERSION_DIR%\%FULL%.json' -Raw ^| ConvertFrom-Json
>> "%DL_LIBS_PS%" echo $libDir = '%LIBRARIES_DIR%'
>> "%DL_LIBS_PS%" echo $total = $json.libraries.Count
>> "%DL_LIBS_PS%" echo $i = 0
>> "%DL_LIBS_PS%" echo foreach ($lib in $json.libraries) {
>> "%DL_LIBS_PS%" echo     $i++
>> "%DL_LIBS_PS%" echo     # 下载 artifact
>> "%DL_LIBS_PS%" echo     if ($lib.downloads.artifact.url) {
>> "%DL_LIBS_PS%" echo         $url = $lib.downloads.artifact.url
>> "%DL_LIBS_PS%" echo         $path = $lib.downloads.artifact.path
>> "%DL_LIBS_PS%" echo         $fullPath = Join-Path $libDir $path
>> "%DL_LIBS_PS%" echo         if (-not (Test-Path $fullPath)) {
>> "%DL_LIBS_PS%" echo             Write-Host "[$i/$total] 下载: $path"
>> "%DL_LIBS_PS%" echo             $dir = Split-Path $fullPath
>> "%DL_LIBS_PS%" echo             if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force ^| Out-Null }
>> "%DL_LIBS_PS%" echo             try { Invoke-WebRequest -Uri $url -OutFile $fullPath -UseBasicParsing } catch { Write-Host "  失败: $_" -ForegroundColor Yellow }
>> "%DL_LIBS_PS%" echo         }
>> "%DL_LIBS_PS%" echo     }
>> "%DL_LIBS_PS%" echo     # 下载 natives (Windows)
>> "%DL_LIBS_PS%" echo     if ($lib.natives.windows -and $lib.downloads.classifiers) {
>> "%DL_LIBS_PS%" echo         $classifier = $lib.natives.windows
>> "%DL_LIBS_PS%" echo         if ($lib.downloads.classifiers[$classifier].url) {
>> "%DL_LIBS_PS%" echo             $url = $lib.downloads.classifiers[$classifier].url
>> "%DL_LIBS_PS%" echo             $path = $lib.downloads.classifiers[$classifier].path
>> "%DL_LIBS_PS%" echo             $fullPath = Join-Path $libDir $path
>> "%DL_LIBS_PS%" echo             if (-not (Test-Path $fullPath)) {
>> "%DL_LIBS_PS%" echo                 Write-Host "[$i/$total] Natives: $path"
>> "%DL_LIBS_PS%" echo                 $dir = Split-Path $fullPath
>> "%DL_LIBS_PS%" echo                 if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force ^| Out-Null }
>> "%DL_LIBS_PS%" echo                 try { Invoke-WebRequest -Uri $url -OutFile $fullPath -UseBasicParsing } catch { Write-Host "  失败: $_" -ForegroundColor Yellow }
>> "%DL_LIBS_PS%" echo             }
>> "%DL_LIBS_PS%" echo         }
>> "%DL_LIBS_PS%" echo     }
>> "%DL_LIBS_PS%" echo }
>> "%DL_LIBS_PS%" echo Write-Host "库文件处理完毕"
powershell -ExecutionPolicy Bypass -File "%DL_LIBS_PS%"
del "%DL_LIBS_PS%" 2>nul

:: 下载 asset index
echo 正在下载资源索引...
powershell -Command "$json = Get-Content '%VERSION_DIR%\%FULL%.json' -Raw | ConvertFrom-Json; $assetId = $json.assetIndex.id; $assetUrl = $json.assetIndex.url; $indexDir = '%ASSETS_DIR%\indexes'; if (-not (Test-Path $indexDir)) { New-Item -ItemType Directory -Path $indexDir -Force ^| Out-Null }; $indexPath = Join-Path $indexDir \"$assetId.json\"; if (-not (Test-Path $indexPath)) { Invoke-WebRequest -Uri $assetUrl -OutFile $indexPath -UseBasicParsing; Write-Host \"资源索引 $assetId.json 下载完成\" }"

:: 下载 Assets
echo 正在下载资源文件（可能需要较长时间）...
set "DL_ASSETS_PS=%TEMP%\mclb_dl_assets.ps1"
> "%DL_ASSETS_PS%" echo $json = Get-Content '%VERSION_DIR%\%FULL%.json' -Raw ^| ConvertFrom-Json
>> "%DL_ASSETS_PS%" echo $assetId = $json.assetIndex.id
>> "%DL_ASSETS_PS%" echo $indexPath = Join-Path '%ASSETS_DIR%\indexes' "$assetId.json"
>> "%DL_ASSETS_PS%" echo if (-not (Test-Path $indexPath)) { Write-Host '缺少资源索引'; exit 1 }
>> "%DL_ASSETS_PS%" echo $index = Get-Content $indexPath -Raw ^| ConvertFrom-Json
>> "%DL_ASSETS_PS%" echo $objects = $index.objects
>> "%DL_ASSETS_PS%" echo $total = $objects.Keys.Count
>> "%DL_ASSETS_PS%" echo $i = 0
>> "%DL_ASSETS_PS%" echo foreach ($key in $objects.Keys) {
>> "%DL_ASSETS_PS%" echo     $i++
>> "%DL_ASSETS_PS%" echo     $hash = $objects[$key].hash
>> "%DL_ASSETS_PS%" echo     $twoChars = $hash.Substring(0,2)
>> "%DL_ASSETS_PS%" echo     $objectDir = Join-Path '%ASSETS_DIR%\objects' $twoChars
>> "%DL_ASSETS_PS%" echo     $objectPath = Join-Path $objectDir $hash
>> "%DL_ASSETS_PS%" echo     if (-not (Test-Path $objectPath)) {
>> "%DL_ASSETS_PS%" echo         Write-Host "[$i/$total] 下载: $key"
>> "%DL_ASSETS_PS%" echo         if (-not (Test-Path $objectDir)) { New-Item -ItemType Directory -Path $objectDir -Force ^| Out-Null }
>> "%DL_ASSETS_PS%" echo         $url = "https://resources.download.minecraft.net/$twoChars/$hash"
>> "%DL_ASSETS_PS%" echo         try { Invoke-WebRequest -Uri $url -OutFile $objectPath -UseBasicParsing } catch { Write-Host "  失败: $_" -ForegroundColor Yellow }
>> "%DL_ASSETS_PS%" echo     }
>> "%DL_ASSETS_PS%" echo }
>> "%DL_ASSETS_PS%" echo Write-Host "资源文件下载完毕"
powershell -ExecutionPolicy Bypass -File "%DL_ASSETS_PS%"
del "%DL_ASSETS_PS%" 2>nul

echo 原版 %MC_VER% 安装完成！
goto :eof

:: ==================== 安装 Fabric ====================
:install_fabric
set "MC_VER=%~1"
set "FULL=%~2"
echo 正在安装 Fabric %MC_VER% ...

:: 1. 确保原版 JSON 存在
set "VANILLA_DIR=%VERSIONS_DIR%\%MC_VER%"
if not exist "%VANILLA_DIR%\%MC_VER%.json" (
    echo 步骤1: 下载原版 JSON...
    mkdir "%VANILLA_DIR%" 2>nul
    powershell -Command "$manifest=Invoke-WebRequest -Uri 'https://piston-meta.mojang.com/mc/game/version_manifest_v2.json' -UseBasicParsing|ConvertFrom-Json;$ver=$manifest.versions|Where-Object{$_.id -eq '%MC_VER%'}|Select-Object -First 1;if(-not $ver){exit 1};$verJson=Invoke-WebRequest -Uri $ver.url -UseBasicParsing|ConvertFrom-Json;$verJson|ConvertTo-Json -Depth 100|Out-File '%VANILLA_DIR%\%MC_VER%.json' -Encoding UTF8"
    if errorlevel 1 (echo 原版 JSON 下载失败 & pause & goto :eof)
)

:: 2. 获取 Fabric Loader 信息
echo 步骤2: 获取 Fabric Loader 信息...
powershell -Command "$loader=Invoke-WebRequest -Uri 'https://meta.fabricmc.net/v2/versions/loader/%MC_VER%' -UseBasicParsing|ConvertFrom-Json;$loader|ConvertTo-Json -Depth 10|Out-File '%TEMP%\fabric_loader.json' -Encoding UTF8"
if not exist "%TEMP%\fabric_loader.json" (echo 获取失败 & pause & goto :eof)

:: 3. 构建完整的 Fabric 版本 JSON
set "FABRIC_DIR=%VERSIONS_DIR%\%FULL%"
mkdir "%FABRIC_DIR%" 2>nul
set "PS_MERGE=%TEMP%\merge_fabric.ps1"
> "%PS_MERGE%" echo $vanilla = Get-Content '%VANILLA_DIR%\%MC_VER%.json' -Raw ^| ConvertFrom-Json
>> "%PS_MERGE%" echo $loader = Get-Content '%TEMP%\fabric_loader.json' -Raw ^| ConvertFrom-Json
>> "%PS_MERGE%" echo $fabric = $vanilla
>> "%PS_MERGE%" echo $fabric.id = '%FULL%'
>> "%PS_MERGE%" echo $fabric.mainClass = $loader.loader.mainClass
>> "%PS_MERGE%" echo $fabric.arguments = $loader.loader.arguments
>> "%PS_MERGE%" echo $fabric.libraries = $vanilla.libraries + $loader.loader.libraries
>> "%PS_MERGE%" echo $fabric ^| ConvertTo-Json -Depth 100 ^| Out-File '%FABRIC_DIR%\%FULL%.json' -Encoding UTF8
>> "%PS_MERGE%" echo Write-Host 'Fabric JSON 生成完毕'
powershell -ExecutionPolicy Bypass -File "%PS_MERGE%"
del "%PS_MERGE%" 2>nul

:: 4. 下载所有 libraries
echo 步骤3: 下载依赖库...
set "DL_LIBS=%TEMP%\dl_fabric_libs.ps1"
> "%DL_LIBS%" echo $json = Get-Content '%FABRIC_DIR%\%FULL%.json' -Raw ^| ConvertFrom-Json
>> "%DL_LIBS%" echo $libDir = '%LIBRARIES_DIR%'
>> "%DL_LIBS%" echo foreach ($lib in $json.libraries) {
>> "%DL_LIBS%" echo     if ($lib.downloads.artifact.url) {
>> "%DL_LIBS%" echo         $url = $lib.downloads.artifact.url
>> "%DL_LIBS%" echo         $path = $lib.downloads.artifact.path
>> "%DL_LIBS%" echo         $full = Join-Path $libDir $path
>> "%DL_LIBS%" echo         if (-not (Test-Path $full)) {
>> "%DL_LIBS%" echo             $dir = Split-Path $full
>> "%DL_LIBS%" echo             if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force ^| Out-Null }
>> "%DL_LIBS%" echo             try { Invoke-WebRequest -Uri $url -OutFile $full -UseBasicParsing } catch { Write-Host '  失败: ' + $url }
>> "%DL_LIBS%" echo         }
>> "%DL_LIBS%" echo     }
>> "%DL_LIBS%" echo     if ($lib.natives.windows -and $lib.downloads.classifiers) {
>> "%DL_LIBS%" echo         $classifier = $lib.natives.windows
>> "%DL_LIBS%" echo         if ($lib.downloads.classifiers[$classifier].url) {
>> "%DL_LIBS%" echo             $url = $lib.downloads.classifiers[$classifier].url
>> "%DL_LIBS%" echo             $path = $lib.downloads.classifiers[$classifier].path
>> "%DL_LIBS%" echo             $full = Join-Path $libDir $path
>> "%DL_LIBS%" echo             if (-not (Test-Path $full)) {
>> "%DL_LIBS%" echo                 $dir = Split-Path $full
>> "%DL_LIBS%" echo                 if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force ^| Out-Null }
>> "%DL_LIBS%" echo                 try { Invoke-WebRequest -Uri $url -OutFile $full -UseBasicParsing } catch { Write-Host '  失败: ' + $url }
>> "%DL_LIBS%" echo             }
>> "%DL_LIBS%" echo         }
>> "%DL_LIBS%" echo     }
>> "%DL_LIBS%" echo }
powershell -ExecutionPolicy Bypass -File "%DL_LIBS%"
del "%DL_LIBS%" 2>nul

:: 5. 下载原版核心 JAR
if not exist "%VANILLA_DIR%\%MC_VER%.jar" (
    echo 下载原版核心 JAR...
    powershell -Command "$json=Get-Content '%VANILLA_DIR%\%MC_VER%.json' -Raw|ConvertFrom-Json;Invoke-WebRequest -Uri $json.downloads.client.url -OutFile '%VANILLA_DIR%\%MC_VER%.jar'"
)

:: 6. 创建 .fabric 标记文件夹
mkdir "%FABRIC_DIR%\.fabric" 2>nul

:: 7. 创建 mods 文件夹并下载 Fabric API
mkdir "%FABRIC_DIR%\mods" 2>nul
echo 下载 Fabric API...
powershell -Command "$apiUrl='https://api.modrinth.com/v2/project/P7dR8mSH/version';$versions=Invoke-WebRequest -Uri $apiUrl -UseBasicParsing|ConvertFrom-Json;$best=$versions|Where-Object{$_.game_versions -contains '%MC_VER%'}|Select-Object -First 1;if($best){$file=$best.files[0];Invoke-WebRequest -Uri $file.url -OutFile '%FABRIC_DIR%\mods\$($file.filename)' -UseBasicParsing;Write-Host 'Fabric API 下载完成'}else{Write-Host '未找到兼容的 Fabric API'}"

echo Fabric %FULL% 安装完成！
goto :eof

:: ==================== 安装 Forge ====================
:install_forge
set "MC_VER=%~1"
set "FULL=%~2"
echo 正在安装 Forge %MC_VER% ...

:: 1. 确保原版 JSON 和核心 jar 存在
set "VANILLA_DIR=%VERSIONS_DIR%\%MC_VER%"
if not exist "%VANILLA_DIR%\%MC_VER%.json" (
    echo 步骤1: 下载原版 JSON 和核心文件...
    mkdir "%VANILLA_DIR%" 2>nul
    call :install_vanilla "%MC_VER%" "%MC_VER%"
    if not exist "%VANILLA_DIR%\%MC_VER%.jar" (
        echo [错误] 原版安装失败，无法继续
        pause
        goto :eof
    )
)

:: 2. 获取 Forge 版本列表
echo 步骤2: 获取 Forge 版本列表...
set "FORGE_LIST_PS=%TEMP%\mclb_forge_list.ps1"
> "%FORGE_LIST_PS%" echo $mcVer = '%MC_VER%'
>> "%FORGE_LIST_PS%" echo try {
>> "%FORGE_LIST_PS%" echo     $url = "https://bmclapi2.bangbang93.com/forge/minecraft/$mcVer"
>> "%FORGE_LIST_PS%" echo     $json = Invoke-WebRequest -Uri $url -UseBasicParsing ^| ConvertFrom-Json
>> "%FORGE_LIST_PS%" echo     $i = 0
>> "%FORGE_LIST_PS%" echo     foreach ($item in $json) {
>> "%FORGE_LIST_PS%" echo         $i++
>> "%FORGE_LIST_PS%" echo         Write-Host "[$i] $($item.version)"
>> "%FORGE_LIST_PS%" echo     }
>> "%FORGE_LIST_PS%" echo } catch {
>> "%FORGE_LIST_PS%" echo     Write-Host "获取 Forge 版本列表失败: $_"
>> "%FORGE_LIST_PS%" echo     exit 1
>> "%FORGE_LIST_PS%" echo }
powershell -ExecutionPolicy Bypass -File "%FORGE_LIST_PS%"
del "%FORGE_LIST_PS%" 2>nul

echo.
echo [0] 取消
set /p forge_choice=请选择 Forge 版本编号:
if "%forge_choice%"=="0" goto :eof
if "%forge_choice%"=="" goto :eof

:: 3. 下载 installer
echo 步骤3: 下载 Forge 安装器...
set "FORGE_DOWNLOAD_PS=%TEMP%\mclb_forge_dl.ps1"
> "%FORGE_DOWNLOAD_PS%" echo $mcVer = '%MC_VER%'
>> "%FORGE_DOWNLOAD_PS%" echo $choice = %forge_choice%
>> "%FORGE_DOWNLOAD_PS%" echo try {
>> "%FORGE_DOWNLOAD_PS%" echo     $url = "https://bmclapi2.bangbang93.com/forge/minecraft/$mcVer"
>> "%FORGE_DOWNLOAD_PS%" echo     $json = Invoke-WebRequest -Uri $url -UseBasicParsing ^| ConvertFrom-Json
>> "%FORGE_DOWNLOAD_PS%" echo     $selected = $json[$choice - 1]
>> "%FORGE_DOWNLOAD_PS%" echo     $installerUrl = $null
>> "%FORGE_DOWNLOAD_PS%" echo     foreach ($file in $selected.files) {
>> "%FORGE_DOWNLOAD_PS%" echo         if ($file.category -eq 'installer') {
>> "%FORGE_DOWNLOAD_PS%" echo             $installerUrl = "https://bmclapi2.bangbang93.com" + $file.url
>> "%FORGE_DOWNLOAD_PS%" echo             break
>> "%FORGE_DOWNLOAD_PS%" echo         }
>> "%FORGE_DOWNLOAD_PS%" echo     }
>> "%FORGE_DOWNLOAD_PS%" echo     if ($installerUrl) {
>> "%FORGE_DOWNLOAD_PS%" echo         Invoke-WebRequest -Uri $installerUrl -OutFile '%TEMP%\forge-installer.jar' -UseBasicParsing
>> "%FORGE_DOWNLOAD_PS%" echo         Write-Host "下载完成"
>> "%FORGE_DOWNLOAD_PS%" echo     } else {
>> "%FORGE_DOWNLOAD_PS%" echo         Write-Host "未找到 installer 文件"
>> "%FORGE_DOWNLOAD_PS%" echo         exit 1
>> "%FORGE_DOWNLOAD_PS%" echo     }
>> "%FORGE_DOWNLOAD_PS%" echo } catch {
>> "%FORGE_DOWNLOAD_PS%" echo     Write-Host "下载失败: $_"
>> "%FORGE_DOWNLOAD_PS%" echo     exit 1
>> "%FORGE_DOWNLOAD_PS%" echo }
powershell -ExecutionPolicy Bypass -File "%FORGE_DOWNLOAD_PS%"
del "%FORGE_DOWNLOAD_PS%" 2>nul

if not exist "%TEMP%\forge-installer.jar" (
    echo [错误] 下载失败
    pause
    goto :eof
)

:: 4. 运行安装器
echo 步骤4: 运行 Forge 安装器...
java -jar "%TEMP%\forge-installer.jar" --installClient "%MC_DIR%"
set "INSTALL_ERROR=%errorlevel%"
del "%TEMP%\forge-installer.jar" 2>nul

if %INSTALL_ERROR% neq 0 (
    echo [错误] Forge 安装器运行失败 (退出码: %INSTALL_ERROR%)
    pause
    goto :eof
)

:: 5. 查找生成的 Forge 版本文件夹
echo 步骤5: 处理安装结果...
set "FORGE_DIR="
for /d %%d in ("%VERSIONS_DIR%\forge-%MC_VER%*") do (
    set "FORGE_DIR=%%d"
    goto :found_forge_dir
)

:found_forge_dir
if not defined FORGE_DIR (
    echo [错误] 未找到 Forge 版本文件夹
    pause
    goto :eof
)

echo 找到 Forge 文件夹: %FORGE_DIR%

:: 6. 补全所有依赖库
echo 步骤6: 下载 Forge 依赖库...
set "FORGE_LIBS_PS=%TEMP%\mclb_forge_libs.ps1"
> "%FORGE_LIBS_PS%" echo $forgeDir = '%FORGE_DIR%'
>> "%FORGE_LIBS_PS%" echo $libDir = '%LIBRARIES_DIR%'
>> "%FORGE_LIBS_PS%" echo $forgeJson = Get-ChildItem -Path $forgeDir -Filter *.json ^| Select-Object -First 1
>> "%FORGE_LIBS_PS%" echo if ($forgeJson) {
>> "%FORGE_LIBS_PS%" echo     $json = Get-Content $forgeJson.FullName -Raw ^| ConvertFrom-Json
>> "%FORGE_LIBS_PS%" echo     $total = $json.libraries.Count
>> "%FORGE_LIBS_PS%" echo     $i = 0
>> "%FORGE_LIBS_PS%" echo     foreach ($lib in $json.libraries) {
>> "%FORGE_LIBS_PS%" echo         $i++
>> "%FORGE_LIBS_PS%" echo         if ($lib.name) {
>> "%FORGE_LIBS_PS%" echo             $parts = $lib.name -split ':'
>> "%FORGE_LIBS_PS%" echo             if ($parts.Count -ge 3) {
>> "%FORGE_LIBS_PS%" echo                 $group = $parts[0]
>> "%FORGE_LIBS_PS%" echo                 $name = $parts[1]
>> "%FORGE_LIBS_PS%" echo                 $version = $parts[2]
>> "%FORGE_LIBS_PS%" echo                 $groupDir = $group -replace '\.', '/'
>> "%FORGE_LIBS_PS%" echo                 $fileName = "$name-$version.jar"
>> "%FORGE_LIBS_PS%" echo                 $fullPath = Join-Path $libDir "$groupDir/$name/$version/$fileName"
>> "%FORGE_LIBS_PS%" echo                 if (!(Test-Path $fullPath)) {
>> "%FORGE_LIBS_PS%" echo                     Write-Host "[$i/$total] 下载: $fileName"
>> "%FORGE_LIBS_PS%" echo                     $dir = Split-Path $fullPath
>> "%FORGE_LIBS_PS%" echo                     if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force ^| Out-Null }
>> "%FORGE_LIBS_PS%" echo                     $url = $null
>> "%FORGE_LIBS_PS%" echo                     if ($lib.downloads.artifact.url) {
>> "%FORGE_LIBS_PS%" echo                         $url = $lib.downloads.artifact.url
>> "%FORGE_LIBS_PS%" echo                     } else {
>> "%FORGE_LIBS_PS%" echo                         $url = "https://bmclapi2.bangbang93.com/maven/$groupDir/$name/$version/$fileName"
>> "%FORGE_LIBS_PS%" echo                     }
>> "%FORGE_LIBS_PS%" echo                     try {
>> "%FORGE_LIBS_PS%" echo                         Invoke-WebRequest -Uri $url -OutFile $fullPath -UseBasicParsing
>> "%FORGE_LIBS_PS%" echo                     } catch {
>> "%FORGE_LIBS_PS%" echo                         Write-Host "  下载失败: $fileName"
>> "%FORGE_LIBS_PS%" echo                     }
>> "%FORGE_LIBS_PS%" echo                 }
>> "%FORGE_LIBS_PS%" echo             }
>> "%FORGE_LIBS_PS%" echo         }
>> "%FORGE_LIBS_PS%" echo     }
>> "%FORGE_LIBS_PS%" echo     Write-Host "Forge 依赖库下载完成"
>> "%FORGE_LIBS_PS%" echo }
powershell -ExecutionPolicy Bypass -File "%FORGE_LIBS_PS%"
del "%FORGE_LIBS_PS%" 2>nul

echo Forge 安装完成！版本文件夹: %FORGE_DIR%
echo 可以使用 [1] 启动游戏并选择该版本
goto :eof

:: ==================== 下载 Mod ====================
:download_mod
call :select_version
if "%SELECTED_VER%"=="" goto :download_menu
set "VERSION_DIR=%SELECTED_VER_DIR%"
powershell -ExecutionPolicy Bypass -File "%MCLB_DIR%mclb_downloader.ps1" -Type mod -SelectedVersion "%SELECTED_VER%" -VersionDirectory "%VERSION_DIR%"
pause >nul
goto :download_menu

:: ==================== 下载光影包 ====================
:download_shader
call :select_version
if "%SELECTED_VER%"=="" goto :download_menu
set "VERSION_DIR=%SELECTED_VER_DIR%"
powershell -ExecutionPolicy Bypass -File "%MCLB_DIR%mclb_downloader.ps1" -Type shader -SelectedVersion "%SELECTED_VER%" -VersionDirectory "%VERSION_DIR%"
pause >nul
goto :download_menu

:: ==================== 下载材质包 ====================
:download_resourcepack
call :select_version
if "%SELECTED_VER%"=="" goto :download_menu
set "VERSION_DIR=%SELECTED_VER_DIR%"
powershell -ExecutionPolicy Bypass -File "%MCLB_DIR%mclb_downloader.ps1" -Type resourcepack -SelectedVersion "%SELECTED_VER%" -VersionDirectory "%VERSION_DIR%"
pause >nul
goto :download_menu