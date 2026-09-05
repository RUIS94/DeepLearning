# 一键启动前后端开发环境（各自独立终端窗口）
#
# 数据库模式切换：
#   .\dev.ps1                                 # 默认：连接线上 Supabase DB
#   .\dev.ps1 -Db LocalDocker                 # 本地 Docker（临时，容器删除即销毁）
#   .\dev.ps1 -Db LocalDocker -DockerDbMode Ephemeral   # 本地 Docker（纯内存 tmpfs，性能最好）
#   .\dev.ps1 -Db LocalDocker -DockerDbMode Persistent  # 本地 Docker（持久化命名卷，跨重启保留）
#
# 首次用一个全新的本地容器时，二选一把参考数据准备好：
#   .\dev.ps1 -Db LocalDocker -PullReference   # 推荐：直接从线上 Supabase 整表复制参考表
#                                              #（含手工改过、任何 .sql 里都没有的内容）
#   .\dev.ps1 -Db LocalDocker -Bootstrap       # 离线可用：只按仓库里的种子脚本重建
# 两个都给时先 Bootstrap 后 PullReference（先有一套完整的库，再用线上真值覆盖参考表）。
#
# 覆盖 launch profile（显式选择 http/https 或自定义）：
#   .\dev.ps1 -Db LocalDocker -LaunchProfile "https (LocalDocker)"
#
# 配套命令：
#   dotnet test --filter "Category!=LlmIntegration"
#   dotnet ef database update --project src/DeepLearning.Infrastructure --startup-project src/DeepLearning.Api
#   http://localhost:3000/
#
# 后端启动后 GET http://localhost:5255/health/db 会回报它到底连的是哪个库，
# 不确定当前跑在哪个库上时以它为准（DB_PROFILE 和连接串不一致时后端会直接拒绝启动）。

param(
    [ValidateSet("Supabase", "LocalDocker")]
    [string]$Db = "Supabase",

    [ValidateSet("Default", "Ephemeral", "Persistent")]
    [string]$DockerDbMode = "Default",

    # 对全新的本地容器跑一次 `sql bootstrap`：EF 迁移建表 + 按仓库里的种子脚本灌数据。
    # 不需要联网，但灌出来的是【脚本描述的状态】，不含线上后来手工改过的内容。
    [switch]$Bootstrap,

    # 跑 `db pull-reference`：EF 迁移建表 + 从线上 Supabase 把参考表和题库整表复制过来
    # （exam_types / assessment_dimensions / error_taxonomies / question_bank_categories /
    # generation_policy / prompt_templates / llm_provider_* / users / questions）。
    # 这份才和线上完全一致。需要 appsettings.Development.json 里有 ConnectionStrings:ReferenceSource。
    [switch]$PullReference,

    [string]$LaunchProfile
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# TcpClient 而不是 Test-NetConnection：后者在端口不通时要等完整的 TCP 超时，本地回环上
# 拒绝连接是立刻返回的。connect 失败会抛，所以 finally 里必须 Dispose，否则每轮轮询都漏一个 socket。
function Test-TcpPort {
    param([int]$Port, [string]$TargetHost = 'localhost')

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.Connect($TargetHost, $Port)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

if (($Bootstrap -or $PullReference) -and $Db -ne "LocalDocker") {
    throw "-Bootstrap / -PullReference 只能和 -Db LocalDocker 一起用（它们会往库里写数据，绝不能对着线上 Supabase 跑）。"
}

# 自动选择 launch profile（根据 Db 参数，可被 -LaunchProfile 显式覆盖）
if (-not $LaunchProfile) {
    $LaunchProfile = if ($Db -eq "LocalDocker") { "http (LocalDocker)" } else { "http (Supabase)" }
}

# LocalDocker 模式下：根据 DockerDbMode 选择 compose 文件组合，检查端口是否已监听
if ($Db -eq "LocalDocker") {
    $pgPort = 5433
    $composeArgs = @("compose", "-f", "docker-compose.yml")
    if ($DockerDbMode -eq "Ephemeral") {
        $composeArgs += @("-f", "docker-compose.ephemeral.yml")
        $modeLabel = "Ephemeral (tmpfs / 纯内存)"
    }
    elseif ($DockerDbMode -eq "Persistent") {
        $composeArgs += @("-f", "docker-compose.persistent.yml")
        $modeLabel = "Persistent (命名卷 / 跨重启保留)"
    }
    else {
        $modeLabel = "Default (down -v 即销毁)"
    }
    Write-Host "[dev] LocalDocker mode: $modeLabel" -ForegroundColor Cyan

    if (Test-TcpPort -Port $pgPort) {
        # 端口通不代表模式对：已经跑着的容器可能是另一个 DockerDbMode 起的。
        Write-Host "[dev] Postgres already listening on $pgPort — reusing it (注意：它未必是用当前 -DockerDbMode 起的)." -ForegroundColor Green
    }
    else {
        Write-Host "[dev] LocalDocker DB not running on port $pgPort — starting containers..." -ForegroundColor Yellow
        Push-Location $root
        try {
            & docker @composeArgs up -d
            $composeExit = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
        if ($composeExit -ne 0) {
            throw "docker compose up failed with exit code $composeExit"
        }

        Write-Host "[dev] Waiting for Postgres..." -ForegroundColor Cyan
        $deadline = (Get-Date).AddSeconds(60)
        while (-not (Test-TcpPort -Port $pgPort) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
        }
        if (-not (Test-TcpPort -Port $pgPort)) {
            throw "Postgres 在 60s 内没起来（port $pgPort）。先看 ``docker compose logs postgres``，别带着一个连不上的库继续启动后端。"
        }
        Write-Host "[dev] Postgres is up." -ForegroundColor Green
    }

    if ($Bootstrap -or $PullReference) {
        # 连接串显式传环境变量，不依赖 launch profile —— CLI 走的是同一个 Program.cs，
        # DB_PROFILE 和连接串必须成对出现且指向本机，否则启动期就会被拒。
        $env:DB_PROFILE = "LocalDocker"
        $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=$pgPort;Database=deeplearning;Username=postgres;Password=postgres"
        Push-Location $root
        try {
            if ($Bootstrap) {
                Write-Host "[dev] sql bootstrap: EF 迁移 + 仓库种子脚本..." -ForegroundColor Cyan
                & dotnet run --project src/DeepLearning.Api --launch-profile "$LaunchProfile" -- sql bootstrap
                if ($LASTEXITCODE -ne 0) { throw "sql bootstrap failed with exit code $LASTEXITCODE" }
            }

            if ($PullReference) {
                Write-Host "[dev] db pull-reference: 从线上 Supabase 复制参考表..." -ForegroundColor Cyan
                & dotnet run --project src/DeepLearning.Api --launch-profile "$LaunchProfile" -- db pull-reference
                if ($LASTEXITCODE -ne 0) { throw "db pull-reference failed with exit code $LASTEXITCODE" }
            }
        }
        finally {
            Remove-Item Env:\DB_PROFILE -ErrorAction SilentlyContinue
            Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
            Pop-Location
        }
    }
}

# `--launch-profile`（带连字符）才是 dotnet run 的真实参数名。写成 --launchProfile 不会报错，
# 会被当成应用参数原样透传，然后 dotnet run 静默回退到 launchSettings 里的第一个 profile
# —— 也就是 Supabase，本来想连本地库的一次运行会直接打在线上库上。
Write-Host "[dev] Launching backend with profile: $LaunchProfile" -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "cd '$root'; dotnet run --project src/DeepLearning.Api --launch-profile '$LaunchProfile'"
)

Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "cd '$root\src\DeepLearning.Web'; npm run dev"
)

Write-Host "[dev] Frontend: http://localhost:3000/" -ForegroundColor Green
Write-Host "[dev] Backend:  http://localhost:5255/  (连的哪个库: http://localhost:5255/health/db)" -ForegroundColor Green
Write-Host "[dev] 销毁 LocalDocker 数据：docker compose down -v（不加 -v 只会留下悬空匿名卷）" -ForegroundColor DarkGray
Write-Host "[dev] 销毁 Persistent 卷：docker compose -f docker-compose.yml -f docker-compose.persistent.yml down -v" -ForegroundColor DarkGray
