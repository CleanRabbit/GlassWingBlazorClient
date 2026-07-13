<#
.SYNOPSIS
    Runs the Playwright E2E suite against an isolated "Testing" environment — a separate
    glasswing_test Mongo database and a dedicated API/client port pair (5223/5011) — so a run
    never touches, and is never polluted by, the interactive dev environment (5123/5001).

.DESCRIPTION
    Mirrors the "wipe the database before and after every run" idea from Task33_Design.md's
    Option 1, but scoped to just the glasswing_test database inside the existing Mongo container
    rather than the whole `docker compose down -v` volume — so it never touches manually-curated
    dev data (DevPlayer's accumulated state, the 21 easter-egg rats, etc.) sitting in the `glasswing`
    database.

    Lifecycle: drop glasswing_test -> start Testing-profile API+client -> wait for both to be
    ready -> bootstrap the DevPlayer's starter rat over HTTP (DevBypass authenticates any request
    as DevPlayer, no login needed) -> seed the fixed-name fixture rats a few RatDetailTests need
    (seed-test-fixtures.js) -> run dotnet test -> stop both processes -> drop glasswing_test again.

.PARAMETER BackendRepoPath
    Path to the GlassWing backend repo's API project. Defaults to this machine's sibling-repo
    layout: ..\GlassWing (backend repo root) \GlassWing (the API project folder, same name).
#>
param(
    [string]$BackendRepoPath = (Join-Path $PSScriptRoot "..\GlassWing\GlassWing")
)

$ErrorActionPreference = "Stop"

$ClientProjectPath = Join-Path $PSScriptRoot "GlassWingClient"
$E2EProjectPath     = Join-Path $PSScriptRoot "GlassWingClient.E2ETests"
$ApiUrl             = "http://localhost:5223"
$ClientUrl          = "http://localhost:5011"
$TestDbConnection   = "mongodb://localhost:27017/glasswing_test?replicaSet=rs0"

function Reset-TestDatabase {
    Write-Host "Dropping glasswing_test database..."
    docker exec glasswing-mongo mongosh $TestDbConnection --quiet --eval "db.dropDatabase()" | Out-Null
}

function Wait-ForHttpReady([string]$Url, [int]$TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    throw "Timed out waiting for $Url to become ready"
}

function Stop-ProcessOnPort([int]$Port) {
    $owningProcessIds = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $owningProcessIds) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
}

$apiProcess = $null
$clientProcess = $null
$testExitCode = 1

try {
    Reset-TestDatabase

    # --artifacts-path fully redirects build output (bin+obj) away from the project's default
    # bin\Debug\net10.0 — without this, this profile's build tries to overwrite the DLLs the
    # *interactive* dev API/client (also bin\Debug\net10.0, launched separately via `dotnet run`)
    # already has open, and the copy step fails with a file-lock error (confirmed: `dotnet run`
    # has no -o/--output option at all, unlike `dotnet build` — --artifacts-path is the one flag
    # that works here, verified it fully relocates output including appsettings.Testing.json).
    Write-Host "Starting Testing-profile API ($ApiUrl)..."
    $apiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--launch-profile", "Testing", "--artifacts-path", (Join-Path $BackendRepoPath "artifacts-testing") `
        -WorkingDirectory $BackendRepoPath -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $PSScriptRoot "e2e-api.log") `
        -RedirectStandardError (Join-Path $PSScriptRoot "e2e-api.err.log")

    Write-Host "Starting Testing-profile client ($ClientUrl)..."
    $clientProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--launch-profile", "Testing", "--artifacts-path", (Join-Path $ClientProjectPath "artifacts-testing") `
        -WorkingDirectory $ClientProjectPath -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $PSScriptRoot "e2e-client.log") `
        -RedirectStandardError (Join-Path $PSScriptRoot "e2e-client.err.log")

    Write-Host "Waiting for API..."
    Wait-ForHttpReady "$ApiUrl/api/game/settings"
    Write-Host "Waiting for client..."
    Wait-ForHttpReady $ClientUrl

    Write-Host "Bootstrapping DevPlayer's starter rat..."
    Invoke-RestMethod -Uri "$ApiUrl/api/rats/starter" -Method Post -ContentType "application/json" | Out-Null

    Write-Host "Seeding fixture rats (Dandelion/Robin/Cider)..."
    Get-Content (Join-Path $PSScriptRoot "seed-test-fixtures.js") -Raw |
        docker exec -i glasswing-mongo mongosh $TestDbConnection --quiet | Out-Null

    Write-Host "Running E2E test suite..."
    dotnet test $E2EProjectPath
    $testExitCode = $LASTEXITCODE
}
finally {
    Write-Host "Stopping Testing-profile API and client..."
    Stop-ProcessOnPort -Port 5223
    Stop-ProcessOnPort -Port 5011
    if ($apiProcess -and -not $apiProcess.HasExited)    { Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue }
    if ($clientProcess -and -not $clientProcess.HasExited) { Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue }

    Reset-TestDatabase
}

exit $testExitCode
