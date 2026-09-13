param(
    [int]$Port = 8765
)

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $projectRoot 'tools\casual_pvp_server\server.py'
$databasePath = Join-Path $projectRoot 'tools\casual_pvp_server\data\casual_pvp.db'

if (-not (Test-Path -LiteralPath $serverPath)) {
    throw "Casual PVP server was not found: $serverPath"
}

function Stop-PreviousCasualPvpServer {
    $listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    foreach ($listener in $listeners) {
        $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)"
        if ($null -eq $processInfo -or $processInfo.CommandLine -notmatch 'casual_pvp_server[\\/]server\.py') {
            throw "Port $Port is occupied by a non-Casual-PVP process (PID $($listener.OwningProcess)). It was not stopped."
        }
        Write-Host "Stopping previous Casual PVP server (PID $($listener.OwningProcess))..."
        Stop-Process -Id $listener.OwningProcess -Force
    }
    if ($listeners.Count -gt 0) {
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            if (-not (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)) { break }
            Start-Sleep -Milliseconds 250
        }
        if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
            throw "The previous Casual PVP server did not stop in time."
        }
    }
}

Stop-PreviousCasualPvpServer
Write-Host "Starting Casual PVP local server: http://127.0.0.1:$Port"
Write-Host "Close this window or press Ctrl+C to stop the server."
& python $serverPath --host 127.0.0.1 --port $Port --database $databasePath
