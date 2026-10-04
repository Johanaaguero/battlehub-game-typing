param(
    [string]$MySqlHost = 'localhost',
    [int]$MySqlPort = 3306,
    [string]$MySqlDatabase = 'typing_battle',
    [string]$MySqlUser = 'typing',
    [string]$MatchmakingUrl = 'http://localhost:5211'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$databaseSecret = Read-Host 'Contraseña del usuario MySQL de Typing' -AsSecureString
$machineSecret = Read-Host 'Client Secret de BattleHub Typing Service (M2M)' -AsSecureString
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $MySqlHost
$connection['Port'] = $MySqlPort
$connection['Database'] = $MySqlDatabase
$connection['User ID'] = $MySqlUser
$connection['Password'] = [System.Net.NetworkCredential]::new('', $databaseSecret).Password
$integrationValues = @{
    'Auth__Mode' = 'Auth0'
    'Auth__Domain' = 'dev-jaii1peslxnejq0y.us.auth0.com'
    'Auth__Audience' = 'https://api.battlehub.local/typing'
    'Auth__RequiredPermission' = ''
    'Cors__AllowedOrigins__0' = 'http://localhost:4000'
    'Cors__AllowedOrigins__1' = 'http://localhost:4001'
    'ConnectionStrings__Typing' = $connection.ConnectionString
    'Database__MigrateOnStartup' = 'true'
    'Matchmaking__BaseUrl' = $MatchmakingUrl
    'Matchmaking__Auth0__Domain' = 'dev-jaii1peslxnejq0y.us.auth0.com'
    'Matchmaking__Auth0__Audience' = 'https://api.battlehub.local/profile'
    'Matchmaking__Auth0__ClientId' = '8BWcE4T8HxhJrxU1CtgmNkjDOpkrN4Su'
    'Matchmaking__Auth0__ClientSecret' = [System.Net.NetworkCredential]::new('', $machineSecret).Password
}
$previousValues = @{}
Push-Location $repoRoot
try {
    foreach ($key in $integrationValues.Keys) {
        $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $integrationValues[$key], 'Process')
    }
    dotnet run --project src/backend/TypingBattle.Api --launch-profile http
    if ($LASTEXITCODE -ne 0) { throw 'La API de Typing terminó con error.' }
} finally {
    foreach ($key in $previousValues.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previousValues[$key], 'Process')
    }
    $integrationValues.Clear()
    $connection.Clear()
    $databaseSecret.Dispose()
    $machineSecret.Dispose()
    Pop-Location
}
