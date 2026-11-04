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
# The output is captured before the exit code is read. Piping a native command straight into Select-Object
# can stop it early and leave $LASTEXITCODE wrong (Windows PowerShell 5.1).
$sdkVersion = $null
Push-Location $backendDir
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdkOutput = & dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) { $sdkVersion = ($sdkOutput | Select-Object -First 1) }
}
Pop-Location
if (-not $sdkVersion) {
    Stop-Script 'The .NET SDK was not found. Install the .NET 10 SDK, then open a new PowerShell window.'
}
if (-not $sdkVersion.StartsWith('10.')) {
    Stop-Script "This shell uses .NET SDK $sdkVersion. backend\global.json needs 10.0.x. Run 'dotnet --list-sdks' to check the installed SDKs."
}
Write-Host "  .NET SDK $sdkVersion"

$nodeVersion = $null
if (Get-Command node -ErrorAction SilentlyContinue) {
    $nodeOutput = & node --version 2>&1
    if ($LASTEXITCODE -eq 0) { $nodeVersion = ($nodeOutput | Select-Object -First 1) }
}
if (-not $nodeVersion) {
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

$accountCount = -1
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
        # Count the sign-in accounts so a new database can get its first administrator. The table may not exist yet.
        $countText = (& $psql -h $PgHost -p $PgPort -U $PgUser -d $PgDatabase -tAc "SELECT CASE WHEN to_regclass('identity_users') IS NULL THEN 0 ELSE (SELECT count(*) FROM identity_users) END")
        if ($LASTEXITCODE -eq 0 -and "$countText".Trim() -match '^[0-9]+$') { $accountCount = [int]"$countText".Trim() }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}
elseif (-not $psql) {
    Write-Host "  psql.exe not found. The database '$PgDatabase' must already exist."
    Write-Host "  If it does not, create it in pgAdmin, or run: CREATE DATABASE $PgDatabase;"
}

# ---- 3b. First administrator (local sign-in) --------------------------------------------

# The API creates the first administrator at startup, but only on a database with no accounts (ADR-0006).
# The password is kept in this process's environment only until the API has started, and is never written to disk.
$bootstrapFromScript = $false
if ($env:Identity__Bootstrap__Username) {
    Write-Host '  First administrator: taken from Identity__Bootstrap__Username in this window.'
}
elseif ($accountCount -eq 0) {
    Write-Step 'Creating the first administrator account'
    Write-Host '  No sign-in accounts exist yet. This account signs in to the EDM web app.'
    Write-Host '  It gets the System Administrator and Auditor roles, so every screen can be checked.'
    $adminName = Read-Host '  Administrator username (3 to 64 characters: letters, digits, dot, underscore, hyphen) [admin]'
    if ([string]::IsNullOrWhiteSpace($adminName)) { $adminName = 'admin' }
    $secureAdmin = Read-Host '  Administrator password (at least 12 characters, typing is hidden)' -AsSecureString
    $bstrAdmin = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureAdmin)
    try {
        $adminPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstrAdmin)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstrAdmin)
    }
    if ([string]::IsNullOrEmpty($adminPassword) -or $adminPassword.Length -lt 12) {
        Stop-Script 'The administrator password must be at least 12 characters.'
    }
    $env:Identity__Bootstrap__Username = $adminName.Trim()
    $env:Identity__Bootstrap__Password = $adminPassword
    $adminPassword = $null
    $bootstrapFromScript = $true
}
elseif ($accountCount -lt 0) {
    Write-Host '  Could not check for sign-in accounts (psql not available).'
    Write-Host '  If none exist yet, set Identity__Bootstrap__Username and Identity__Bootstrap__Password first.'
}
else {
    Write-Host "  $accountCount sign-in account(s) exist. No administrator is created."
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

# The API process has its copy of the administrator password now. Drop this window's copy.
if ($bootstrapFromScript) {
    Remove-Item Env:Identity__Bootstrap__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Identity__Bootstrap__Username -ErrorAction SilentlyContinue
}

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
    Remove-Item Env:Identity__Bootstrap__Password -ErrorAction SilentlyContinue
    Write-Host 'Stopped.'
}
