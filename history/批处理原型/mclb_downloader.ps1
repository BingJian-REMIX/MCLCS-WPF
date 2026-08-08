param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("mod","shader","resourcepack")]
    [string]$Type,

    [Parameter(Mandatory=$true)]
    [string]$SelectedVersion,

    [Parameter(Mandatory=$true)]
    [string]$VersionDirectory,

    [string]$SearchTerm
)

# 提取纯净游戏版本号
$PureGameVersion = ($SelectedVersion -split '-')[0]

# 识别加载器
$Loader = ""
if ($SelectedVersion -match "fabric") { $Loader = "fabric" }
elseif ($SelectedVersion -match "forge") { $Loader = "forge" }
elseif ($SelectedVersion -match "quilt") { $Loader = "quilt" }
elseif ($SelectedVersion -match "neoforge") { $Loader = "neoforge" }

# 确定目标子目录
$SubDirectory = switch ($Type) {
    "mod"           { "mods" }
    "shader"        { "shaderpacks" }
    "resourcepack"  { "resourcepacks" }
}
$OutputDirectory = Join-Path $VersionDirectory $SubDirectory
if (-not (Test-Path $OutputDirectory)) { 
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null 
}

# 构建 facets（版本 + 加载器过滤，仅 Mod 搜索时添加加载器）
$FacetString = '[["versions:' + $PureGameVersion + '"]]'
if ($Type -eq "mod" -and $Loader) {
    $FacetString += ',["categories:' + $Loader + '"]'
}
$EncodedFacets = [uri]::EscapeDataString($FacetString)

# 搜索
if (-not $SearchTerm) {
    $SearchTerm = Read-Host "请输入搜索关键词"
}
if (-not $SearchTerm) {
    Write-Host "未输入关键词，已取消" -ForegroundColor Red
    exit 1
}

$EncodedSearchTerm = [uri]::EscapeDataString($SearchTerm)
$ApiUrl = "https://api.modrinth.com/v2/search?query=$EncodedSearchTerm&facets=$EncodedFacets"

try {
    Write-Host "正在搜索..." -ForegroundColor Cyan
    $Response = Invoke-WebRequest -Uri $ApiUrl -UseBasicParsing -TimeoutSec 15
    $SearchData = $Response.Content | ConvertFrom-Json
} catch {
    Write-Host "搜索失败: $_" -ForegroundColor Red
    exit 1
}

if ($SearchData.hits.Count -eq 0) {
    Write-Host "未找到相关项目" -ForegroundColor Yellow
    exit 1
}

# 显示列表
Write-Host ""
for ($i = 0; $i -lt $SearchData.hits.Count; $i++) {
    $Hit = $SearchData.hits[$i]
    $Title = $Hit.title
    $Description = if ($Hit.description.Length -gt 80) { $Hit.description.Substring(0, 80) + "..." } else { $Hit.description }
    Write-Host ("[{0}] {1} (下载量: {2:N0})" -f ($i+1), $Title, $Hit.downloads) -ForegroundColor Green
    Write-Host "    简介: $Description"
}
Write-Host "[0] 取消"

# 等待输入
$Choice = Read-Host "请输入编号"
$ChoiceNumber = 0
if (-not [int]::TryParse($Choice, [ref]$ChoiceNumber) -or $ChoiceNumber -lt 0 -or $ChoiceNumber -gt $SearchData.hits.Count) {
    Write-Host "输入无效，已取消" -ForegroundColor Red
    exit 1
}
if ($ChoiceNumber -eq 0) {
    Write-Host "已取消" -ForegroundColor Yellow
    exit 0
}

$SelectedHit = $SearchData.hits[$ChoiceNumber - 1]
$ProjectId = $SelectedHit.project_id

# 获取版本列表
$VersionUrl = "https://api.modrinth.com/v2/project/$ProjectId/version"
try {
    $Versions = (Invoke-WebRequest -Uri $VersionUrl -UseBasicParsing).Content | ConvertFrom-Json
} catch {
    Write-Host "获取版本信息失败: $_" -ForegroundColor Red
    exit 1
}

# 选择最佳版本
$BestVersion = $null
foreach ($Ver in $Versions) {
    if ($Ver.game_versions -contains $PureGameVersion) {
        $BestVersion = $Ver
        break
    }
}
if (-not $BestVersion) { $BestVersion = $Versions[0] }
$File = $BestVersion.files[0]

# 下载
$DownloadPath = Join-Path $OutputDirectory $File.filename
Write-Host "正在下载 $($File.filename) ..." -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri $File.url -OutFile $DownloadPath -UseBasicParsing
    Write-Host "下载完成: $($File.filename)" -ForegroundColor Green
} catch {
    Write-Host "下载失败: $_" -ForegroundColor Red
    exit 1
}