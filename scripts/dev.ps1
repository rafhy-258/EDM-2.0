<#
.SYNOPSIS
  Starts EDM 2.0 on this machine: the API (applying the database schema on start) and the web dev server.

.DESCRIPTION
  One command, run from the repository root in Windows PowerShell:

      .\scripts\dev.ps1

  PostgreSQL must already be running on this machine. Docker is not used.

  The database password is asked for when the script starts. It is kept in memory for this run only.
  It is never written to a file, a log or the console. To skip the prompt, set the environment
  variable ConnectionStrings__Edm in the same PowerShell window before running the script.

  The API log is written to artifacts\local\api.log (git-ignored).
  Stop everything with Ctrl+C. The API process tree is stopped when the script exits.

.PARAMETER PgHost
  PostgreSQL host. Default localhost.

.PARAMETER PgPort
  PostgreSQL port. Default 5432.

.PARAMETER PgUser
  PostgreSQL user that owns the EDM database. Default postgres.

.PARAMETER PgDatabase
  EDM database name. Created if it does not exist and psql can be found. Default edm.

.PARAMETER PsqlPath
  Full path to psql.exe. If omitted, the script looks on PATH and in C:\Program Files\PostgreSQL\*\bin.

.PARAMETER ApiPort
  Port for the API. Default 5000. The web dev server proxies /api to this port.
#>
param(
    [string]$PgHost = 'localhost',
    [int]$PgPort = 5432,
    [string]$PgUser = 'postgres',
    [string]$PgDatabase = 'edm',
    [string]$PsqlPath = '',
    [int]$ApiPort = 5000
)

# Native commands are checked through $LASTEXITCODE. With 'Stop', stderr text from dotnet or npm would end the script.
$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot 'backend'
$webDir = Join-Path $repoRoot 'web'
$logDir = Join-Path $repoRoot 'artifacts\local'
$apiProject = Join-Path $backendDir 'src\Host\Edm.Host\Edm.Host.csproj'

function Stop-Script([string]$message) {
    Write-Host ''
    Write-Host "ERROR: $message" -ForegroundColor Red
    exit 1
}

function Write-Step([string]$message) {
    Write-Host ''
    Write-Host "==> $message" -ForegroundColor Cyan
}

# ---- 1. Tools -------------------------------------------------------------------------------

Write-Step 'Checking tools'

if ($PgDatabase -notmatch '^[a-z_][a-z0-9_]{0,62}$') {
    Stop-Script "Database name '$PgDatabase' must use lower case letters, digits and underscores only."
}

# Run dotnet from backend\ so that global.json (SDK 10.0.x) is picked up.
Push-Location $backendDir
$sdkVersion = (& dotnet --version | Select-Object -First 1)
$sdkExit = $LASTEXITCODE
Pop-Location
if ($sdkExit -ne 0 -or -not $sdkVersion) {
    Stop-Script 'The .NET SDK was not found. Install the .NET 10 SDK, then open a new PowerShell window.'
}
if (-not $sdkVersion.StartsWith('10.')) {
    Stop-Script "This shell uses .NET SDK $sdkVersion. backend\global.json needs 10.0.x. Run 'dotnet --list-sdks' to check the installed SDKs."
}
Write-Host "  .NET SDK $sdkVersion"

$nodeVersion = (& node --version | Select-Object -First 1)
if ($LASTEXITCODE -ne 0 -or -not $nodeVersion) {
    Stop-Script 'Node.js was not found. Install Node.js 22 LTS, then open a new PowerShell window.'
}
Write-Host "  Node.js $nodeVersion"

# ---- 2. Web dependencies --------------------------------------------------------------------

if (-not (Test-Path (Join-Path $webDir 'node_modules'))) {
    Write-Step 'Installing web dependencies (npm ci, first run only)'
    Push-Location $webDir
    & npm ci
    $npmExit = $LASTEXITCODE
    Pop-Location
    if ($npmExit -ne 0) { Stop-Script 'npm ci failed. Read the output above.' }
}

# ---- 3. Database connection -----------------------------------------------------------------

Write-Step 'Checking PostgreSQL'

$connection = $env:ConnectionStrings__Edm
$connectionFromWindow = [bool]$connection
$password = $null

if (-not $connection) {
    $secure = Read-Host "PostgreSQL password for user $PgUser (input is hidden)" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    if ([string]::IsNullOrEmpty($password)) { Stop-Script 'No password was entered.' }
    # Values with ; or quotes would break the connection string. Use the environment variable instead.
    if ($password -match '[;"]') {
        Stop-Script 'This password contains ; or a double quote, which the connection string cannot carry safely. Set ConnectionStrings__Edm in this window instead, with the value quoted for Npgsql.'
    }
    $connection = "Host=$PgHost;Port=$PgPort;Database=$PgDatabase;Username=$PgUser;Password=$password"
}
else {
    Write-Host '  Using ConnectionStrings__Edm from this window.'
}

# The server must accept TCP connections before anything else is tried.
$tcp = New-Object System.Net.Sockets.TcpClient
try {
    $tcp.Connect($PgHost, $PgPort)
}
catch {
    Stop-Script "PostgreSQL is not accepting connections at ${PgHost}:$PgPort. Start the PostgreSQL service, then run this script again."
}
finally {
    $tcp.Close()
}
Write-Host "  PostgreSQL is accepting connections at ${PgHost}:$PgPort"

# Create the database when it is missing. This needs psql.exe. Without it, the database must exist already.
$psql = $PsqlPath
if (-not $psql) {
    $onPath = Get-Command psql -ErrorAction SilentlyContinue
    if ($onPath) { $psql = $onPath.Source }
}
if (-not $psql) {
    $candidate = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($candidate) { $psql = $candidate.FullName }
}

if ($psql -and $password) {
    $env:PGPASSWORD = $password
    try {
        $exists = (& $psql -h $PgHost -p $PgPort -U $PgUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$PgDatabase'")
        if ($LASTEXITCODE -ne 0) {
            Stop-Script "psql could not sign in as $PgUser. Check the password."
        }
        if ("$exists".Trim() -ne '1') {
            Write-Host "  Creating database $PgDatabase"
            & $psql -h $PgHost -p $PgPort -U $PgUser -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $PgDatabase"
            if ($LASTEXITCODE -ne 0) { Stop-Script "Could not create database $PgDatabase." }
        }
        else {
            Write-Host "  Database $PgDatabase exists"
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}
elseif (-not $psql) {
    Write-Host "  psql.exe not found. The database '$PgDatabase' must already exist."
    Write-Host "  If it does not, create it in pgAdmin, or run: CREATE DATABASE $PgDatabase;"
}

# ---- 4. Start the API -----------------------------------------------------------------------

Write-Step 'Starting the API (first build can take a few minutes)'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$apiLog = Join-Path $logDir 'api.log'
$apiErrLog = Join-Path $logDir 'api.err.log'

# Process-scoped settings. They are inherited by the API process and nothing is written to disk.
$env:ConnectionStrings__Edm = $connection
$env:Database__MigrateOnStartup = 'true'
$env:EDM_API_ORIGIN = "http://localhost:$ApiPort"

# The API has its own copy of the connection string now. Drop this script's copy of the password.
$password = $null

$apiUrl = "http://localhost:$ApiPort"
$api = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $apiProject, '--no-launch-profile', '--urls', $apiUrl) `
    -WorkingDirectory $backendDir `
    -RedirectStandardOutput $apiLog `
    -RedirectStandardError $apiErrLog `
    -PassThru -WindowStyle Hidden

$ready = $false
$deadline = (Get-Date).AddMinutes(5)
while ((Get-Date) -lt $deadline) {
    if ($api.HasExited) { break }
    try {
        $response = Invoke-WebRequest -Uri "$apiUrl/health/ready" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) { $ready = $true; break }
    }
    catch {
        # Not up yet. Keep waiting.
    }
    Start-Sleep -Seconds 2
}

function Stop-Api {
    if ($api -and -not $api.HasExited) {
        # /T stops the dotnet run child process as well.
        & taskkill /T /PID $api.Id /F | Out-Null
    }
}

if (-not $ready) {
    Stop-Api
    Stop-Script "The API did not become ready. See $apiLog and $apiErrLog."
}
Write-Host "  API ready at $apiUrl (health: $apiUrl/health/ready)"
Write-Host "  API log: $apiLog"

# ---- 5. Start the web dev server ------------------------------------------------------------

Write-Step 'Starting the web dev server'
Write-Host '  Open http://localhost:5173 in your browser. Press Ctrl+C here to stop everything.'
Write-Host ''

try {
    Push-Location $webDir
    & npm run dev
}
finally {
    Pop-Location
    Stop-Api
    # Remove only what this script set. A connection string the user set in this window is kept.
    if (-not $connectionFromWindow) { Remove-Item Env:ConnectionStrings__Edm -ErrorAction SilentlyContinue }
    Remove-Item Env:Database__MigrateOnStartup -ErrorAction SilentlyContinue
    Remove-Item Env:EDM_API_ORIGIN -ErrorAction SilentlyContinue
    Write-Host 'Stopped.'
}
