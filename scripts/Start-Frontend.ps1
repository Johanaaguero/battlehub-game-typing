param([string]$ApiUrl = 'http://localhost:5015')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$previousApi = $env:TYPING_API_URL
$previousCi = $env:CI
Push-Location (Join-Path $repoRoot 'frontend')
try {
    $env:TYPING_API_URL = $ApiUrl
    $env:CI = '1'
    if (-not (Test-Path 'node_modules')) {
        npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias.' }
    }
    npm.cmd start
    if ($LASTEXITCODE -ne 0) { throw 'El frontend terminó con error.' }
} finally {
    $env:TYPING_API_URL = $previousApi
    $env:CI = $previousCi
    Pop-Location
}
