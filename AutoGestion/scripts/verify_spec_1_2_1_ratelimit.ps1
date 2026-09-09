# ==============================================================================
# Script de Verificación Integral para SPEC-1.2.1: Rate Limiting en Ocelot
# AutoGestion S.A. - Desafío 2 (DSE104)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "INICIANDO VERIFICACION SPEC-1.2.1: RATE LIMITING (10 REQ/MIN)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# Detener procesos previos que pudieran estar ocupando los puertos
Get-Process | Where-Object { $_.ProcessName -match "ApiProductos|ApiLibros|ApiVehiculos|ApiGateway" } | Stop-Process -Force -ErrorAction SilentlyContinue

# Compilación previa
Write-Host "`n[PASO 1] Compilando la solución..." -ForegroundColor Yellow
$buildOutput = dotnet build "$BaseDir\AutoGestion.slnx" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo en la compilación de la solución."
}
Write-Host "Compilación exitosa (0 Errores)." -ForegroundColor Green

$processes = @()

function Start-ServiceProcess {
    param(
        [string]$ProjectName,
        [string]$ProjectPath,
        [int]$Port
    )
    Write-Host "Iniciando $ProjectName en puerto $Port..." -ForegroundColor Yellow
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = "dotnet"
    $pinfo.Arguments = "run --no-build --project `"$ProjectPath`""
    $pinfo.UseShellExecute = $false
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.CreateNoWindow = $true
    $proc = [System.Diagnostics.Process]::Start($pinfo)
    $script:processes += $proc

    $retries = 30
    $started = $false
    while ($retries -gt 0) {
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $tcp.Connect("127.0.0.1", $Port)
            $tcp.Close()
            $started = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 500
            $retries--
        }
    }

    if (-not $started) {
        Write-Error "El servicio $ProjectName no respondió en el puerto $Port."
    }
    Write-Host "$ProjectName activo en puerto $Port." -ForegroundColor Green
}

function Stop-AllProcesses {
    Write-Host "`nDeteniendo servicios..." -ForegroundColor Yellow
    foreach ($p in $script:processes) {
        try {
            if (-not $p.HasExited) {
                $p.Kill($true)
                $p.WaitForExit(3000)
            }
        }
        catch {}
    }
    Write-Host "Servicios detenidos." -ForegroundColor Green
}

try {
    # 1. Iniciar Microservicio de Productos (5001)
    Start-ServiceProcess -ProjectName "ApiProductos" -ProjectPath "$BaseDir\src\ApiProductos\ApiProductos.csproj" -Port 5001

    # 2. Iniciar API Gateway (5000)
    Start-ServiceProcess -ProjectName "ApiGateway" -ProjectPath "$BaseDir\src\ApiGateway\ApiGateway.csproj" -Port 5000

    Write-Host "`n[PASO 2] Ejecutando ráfaga de 12 peticiones a http://localhost:5000/productos..." -ForegroundColor Yellow

    $totalRequests = 12
    $successful = 0
    $rateLimited = 0

    for ($i = 1; $i -le $totalRequests; $i++) {
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $response = Invoke-WebRequest -Uri "http://localhost:5000/productos" -Method GET -UseBasicParsing
            $sw.Stop()

            if ($response.StatusCode -eq 200) {
                $successful++
                Write-Host "  Peticion #$($i) - HTTP $($response.StatusCode) OK (Tiempo: $($sw.ElapsedMilliseconds) ms)" -ForegroundColor Green
            }
            else {
                Write-Host "  Peticion #$($i) - HTTP $($response.StatusCode)" -ForegroundColor Yellow
            }
        }
        catch {
            $sw.Stop()
            $statusCode = $_.Exception.Response.StatusCode.value__
            if ($statusCode -eq 429) {
                $rateLimited++
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
                Write-Host "  Peticion #$($i) - HTTP 429 Too Many Requests (Tiempo: $($sw.ElapsedMilliseconds) ms)" -ForegroundColor Red
                Write-Host "    Mensaje: $body" -ForegroundColor Magenta
            }
            else {
                Write-Host "  Peticion #$($i) - Excepcion con status $statusCode" -ForegroundColor Yellow
            }
        }
    }

    Write-Host "`n[PASO 3] Resumen de Resultados:" -ForegroundColor Yellow
    Write-Host "  Peticiones dentro del umbral (1 a 10): $successful / 10 exitosas (HTTP 200)" -ForegroundColor Green
    Write-Host "  Peticiones bloqueadas por Rate Limit (11 y 12): $rateLimited / 2 bloqueadas (HTTP 429)" -ForegroundColor Red

    if ($successful -eq 10 -and $rateLimited -eq 2) {
        Write-Host "`n======================================================================" -ForegroundColor Green
        Write-Host "CERTIFICACION EXITOSA: SPEC-1.2.1 RATE LIMITING VERIFICADO AL 100%" -ForegroundColor Green
        Write-Host "======================================================================" -ForegroundColor Green
    }
    else {
        Write-Error "El Rate Limiting no se comportó según lo esperado."
    }
}
finally {
    Stop-AllProcesses
}
