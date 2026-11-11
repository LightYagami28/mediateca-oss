# Smoke test del server locale: catalogo, /health, API con token, WebDAV con Basic auth.
# Uso: pwsh -File tools/smoke.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$tdHome = Join-Path $env:TEMP 'teledisk-smoke'
if (Test-Path $tdHome) { Remove-Item $tdHome -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $tdHome 'dav') | Out-Null
$env:TELEDISK_HOME = $tdHome
$env:TELEDISK_API_TOKEN = 'token-di-prova'

Set-Content (Join-Path $tdHome 'dav/Interstellar.2014.mkv') 'film'
Set-Content (Join-Path $tdHome 'dav/Dune.2021.mkv') 'film'

$cli = Join-Path $root 'src/Teledisk.Cli/bin/Release/net11.0/Teledisk.Cli.exe'
if (-not (Test-Path $cli)) { throw "pubblica prima la CLI: dotnet publish src\Teledisk.Cli -c Release" }

& $cli db | Out-Null
& $cli scan (Join-Path $tdHome 'dav') | Out-Null
& $cli settings set port 8099 | Out-Null
& $cli settings set webdav true | Out-Null
& $cli settings password 'segretissima' | Out-Null

Write-Host '--- database e catalogo ---'
& $cli db
& $cli scan (Join-Path $tdHome 'dav')

$log = Join-Path $tdHome 'server.log'
$server = Start-Process -FilePath $cli -ArgumentList 'serve' -RedirectStandardOutput $log -WindowStyle Hidden -PassThru
try {
    $client = [System.Net.Http.HttpClient]::new()
    $client.BaseAddress = [Uri]'http://127.0.0.1:8099/'

    $ready = $false
    foreach ($attempt in 1..20) {
        try { $null = $client.GetAsync('health').GetAwaiter().GetResult(); $ready = $true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { Write-Host "server non pronto, log:"; Get-Content $log -ErrorAction SilentlyContinue; throw 'server non in ascolto' }

    Write-Host '--- /health ---'
    $health = $client.GetAsync('health').GetAwaiter().GetResult()
    Write-Host ("status {0}: {1}" -f [int]$health.StatusCode, $health.Content.ReadAsStringAsync().GetAwaiter().GetResult())

    Write-Host '--- /api/items con token ---'
    $withToken = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, 'api/items?q=Interstellar')
    $null = $withToken.Headers.Add('Authorization', 'Bearer token-di-prova')
    $items = $client.SendAsync($withToken).GetAwaiter().GetResult()
    Write-Host ("status {0}: {1}" -f [int]$items.StatusCode, $items.Content.ReadAsStringAsync().GetAwaiter().GetResult())

    Write-Host '--- /api/items senza token (atteso 401) ---'
    $anon = [System.Net.Http.HttpClient]::new()
    $anon.BaseAddress = [Uri]'http://127.0.0.1:8099/'
    $denied = $anon.GetAsync('api/items').GetAwaiter().GetResult()
    Write-Host ("status {0}" -f [int]$denied.StatusCode)

    Write-Host '--- WebDAV PROPFIND senza Basic (atteso 401) ---'
    $propfind = [System.Net.Http.HttpMethod]::new('PROPFIND')
    $request = [System.Net.Http.HttpRequestMessage]::new($propfind, 'dav/')
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    Write-Host ("status {0}" -f [int]$response.StatusCode)

    Write-Host '--- WebDAV PROPFIND con Basic (atteso 207) ---'
    $credentials = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('teledisk:segretissima'))
    $request = [System.Net.Http.HttpRequestMessage]::new($propfind, 'dav/')
    $null = $request.Headers.Add('Authorization', "Basic $credentials")
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Write-Host ("status {0}, DAV {1}, xml {2} byte" -f [int]$response.StatusCode, ($response.Headers.GetValues('DAV') -join ','), $body.Length)
    if ($body.Length -gt 0) { Write-Host $body.Substring(0, [Math]::Min(300, $body.Length)) }
}
finally {
    if (-not $server.HasExited) { $server.Kill() }
    Write-Host '--- log del server ---'
    Get-Content $log -ErrorAction SilentlyContinue | Select-Object -First 6
    Remove-Item Env:TELEDISK_HOME -ErrorAction SilentlyContinue
    Remove-Item Env:TELEDISK_API_TOKEN -ErrorAction SilentlyContinue
}
