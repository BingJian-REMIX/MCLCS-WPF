param(
    [string]$versionDir,
    [string]$selectedVer,
    [string]$libDir,
    [string]$nativesDir,
    [string]$mcDir,
    [string]$assetsDir,
    [string]$assetIndex,
    [string]$mem,
    [string]$username
)

# ---------- 智能 Java 查找 ----------
function Get-JavaVersion($exe) {
    try {
        $output = & $exe -version 2>&1 | Out-String
        if ($output -match 'version\s+"(\d+)') { return [int]$Matches[1] }
        return 0
    } catch { return 0 }
}

function Get-BestJava {
    $mcRuntime = Join-Path $env:APPDATA '.minecraft\runtime'
    if (Test-Path $mcRuntime) {
        Get-ChildItem $mcRuntime -Directory | Sort-Object Name -Descending | ForEach-Object {
            $java = Join-Path $_.FullName 'bin\java.exe'
            if (Test-Path $java) {
                $v = Get-JavaVersion $java
                if ($v -ge 21) { return $java }
            }
        }
    }
    $sysJava = (Get-Command java -ErrorAction SilentlyContinue).Source
    if ($sysJava) { return $sysJava }
    return 'java'
}

$javaPath = Get-BestJava
$javaVer = Get-JavaVersion $javaPath
Write-Host "Selected Java: $javaPath (version $javaVer)" -ForegroundColor Cyan
if ($javaVer -lt 21) {
    Write-Host "WARNING: Minecraft 1.21+ requires Java 21. Your Java is version $javaVer." -ForegroundColor Yellow
}

# ---------- 读取 JSON ----------
$jsonPath = Join-Path $versionDir "$selectedVer.json"
$json = Get-Content $jsonPath -Raw | ConvertFrom-Json
$mainClass = $json.mainClass.Trim()

# ---------- 确保 natives 目录存在 ----------
if (-not (Test-Path $nativesDir)) { New-Item -ItemType Directory -Path $nativesDir -Force | Out-Null }

# ---------- 变量替换表 ----------
$vars = @{
    'auth_player_name'  = $username
    'version_name'      = $selectedVer
    'game_directory'    = $mcDir
    'assets_root'       = $assetsDir
    'assets_index_name' = $assetIndex
    'auth_uuid'         = '00000000-0000-0000-0000-000000000000'
    'auth_access_token' = 'offline-token'
    'user_type'         = 'mojang'
    'version_type'      = 'release'
    'natives_directory' = $nativesDir
    'launcher_name'     = 'MCLB'
    'launcher_version'  = '1.0'
    'clientid'          = 'offline'
    'auth_xuid'         = 'offline'
    'resolution_width'  = ''
    'resolution_height' = ''
    'quickPlayPath'              = ''
    'quickPlaySingleplayer'      = ''
    'quickPlayMultiplayer'       = ''
    'quickPlayRealms'            = ''
}

# ---------- 构建类路径 ----------
$libs = [System.Collections.Generic.List[string]]::new()
foreach ($lib in $json.libraries) {
    if ($lib.rules) {
        $allow = $true
        foreach ($rule in $lib.rules) {
            if ($rule.os) {
                if ($rule.os.name -eq 'windows' -and $rule.action -eq 'disallow') { $allow = $false }
                if ($rule.os.name -ne 'windows' -and $rule.action -eq 'allow') { $allow = $false }
            }
        }
        if (-not $allow) { continue }
    }
    if ($lib.name) {
        $parts = $lib.name -split ':'
        if ($parts.Count -ge 3) {
            $group = $parts[0]; $name = $parts[1]; $ver = $parts[2]
            $groupDir = $group -replace '\.', '/'
            $fileName = "$name-$ver.jar"
            $fullPath = Join-Path $libDir "$groupDir/$name/$ver/$fileName"
            if (Test-Path $fullPath) { $libs.Add($fullPath) }
        }
    }
}
$coreJar = Join-Path $versionDir "$selectedVer.jar"
if (Test-Path $coreJar) { $libs.Add($coreJar) }
$realClasspath = $libs -join ';'
$vars['classpath'] = $realClasspath

# ---------- 获取参数模板 ----------
if ($json.arguments) {
    $jvmArgs = $json.arguments.jvm
    $gameArgs = $json.arguments.game
} else {
    $jvmArgs = @('-Djava.library.path=${natives_directory}')
    $gameArgs = $json.minecraftArguments -split ' '
}

$jvmArgs = @($jvmArgs | Where-Object {
    if ($_ -is [string]) { $_ -notmatch '^-Xmx' }
    else { $true }
})
$jvmArgs = @("-Xmx${mem}M") + $jvmArgs

$extraJvm = @(
    '-Dfile.encoding=COMPAT',
    '-Dstderr.encoding=UTF-8',
    '-Dstdout.encoding=UTF-8',
    '-Dlog4j2.formatMsgNoLookups=true',
    "-Dorg.lwjgl.librarypath=$nativesDir"
)
$jvmArgs += $extraJvm

# ---------- 规则过滤与变量展开 ----------
function Test-Rules($rules) {
    foreach ($rule in $rules) {
        if ($rule.os) {
            $isWindows = $rule.os.name -eq 'windows'
            if ($rule.action -eq 'allow' -and -not $isWindows) { return $false }
            if ($rule.action -eq 'disallow' -and $isWindows) { return $false }
        }
    }
    return $true
}

function Expand-Vars($argList, $v) {
    $argList | ForEach-Object {
        if ($_ -is [string]) {
            $r = $_
            $r = [regex]::Replace($r, '\$\{(\w+)\}', {
                param($m)
                if ($v.ContainsKey($m.Groups[1].Value)) { $v[$m.Groups[1].Value] }
                else { '' }
            })
            if ([string]::IsNullOrWhiteSpace($r)) { return }
            $r
        } elseif ($_ -is [System.Management.Automation.PSCustomObject] -and $_.rules) {
            if (Test-Rules $_.rules) {
                Expand-Vars $_.value $v
            }
        } else { $_ }
    }
}

function Clean-GameArgs($argsList) {
    $result = @()
    $i = 0
    while ($i -lt $argsList.Count) {
        $arg = $argsList[$i]
        if ($arg -match '^--\S+') {
            $hasValue = $false
            if (($i + 1) -lt $argsList.Count) {
                $next = $argsList[$i + 1]
                if ($next -notmatch '^--' -and -not [string]::IsNullOrWhiteSpace($next)) {
                    $result += $arg
                    $result += $next
                    $hasValue = $true
                }
            }
            if (-not $hasValue) {
                if (($i + 1) -lt $argsList.Count -and [string]::IsNullOrWhiteSpace($argsList[$i + 1])) {
                    $i++
                }
            }
        } else {
            if (-not [string]::IsNullOrWhiteSpace($arg)) { $result += $arg }
        }
        $i++
    }
    return $result
}

function Quote-If-Needed($s) {
    if ($s -match '\s') { return '"' + $s + '"' }
    return $s
}

$expandedJvm = Expand-Vars $jvmArgs $vars | ForEach-Object { Quote-If-Needed $_ }
$expandedGame = Clean-GameArgs (Expand-Vars $gameArgs $vars) | ForEach-Object { Quote-If-Needed $_ }

$cpPart = '-cp "' + $realClasspath + '"'
$commandLine = "`"$javaPath`" $($expandedJvm -join ' ') $cpPart $mainClass $($expandedGame -join ' ')"

$tempBat = [System.IO.Path]::GetTempFileName() + '.bat'
[System.IO.File]::WriteAllText($tempBat, $commandLine, [System.Text.Encoding]::ASCII)
cmd /c $tempBat
$exitCode = $LASTEXITCODE
Remove-Item $tempBat -Force
exit $exitCode