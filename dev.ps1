# 一键启动前后端开发环境（各自独立终端窗口）
# .\dev.ps1
# http://localhost:3000/

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "cd '$root'; dotnet run --project src/DeepLearning.Api"
)

Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "cd '$root\src\DeepLearning.Web'; npm run dev"
)
