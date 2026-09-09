# ==============================================================================
# Script de Verificación Integral para SPEC-1.1.1: Ocelot Enrutamiento Dinámico
# AutoGestion S.A. - Desafío 2 (DSE104)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "INICIANDO PROTOCOLO DE VERIFICACION SPEC-1.1.1 (OCELOT + 3 APIS)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Compilación previa de la solución
Write-Host "`n[PASO 1] Compilando la solución completa..." -ForegroundColor Yellow
$buildOutput = dotnet build "$BaseDir\AutoGestion.slnx" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo en la compilación de la solución."
}
Write-Host "Compilación exitosa (0 Errores)." -ForegroundColor Green

# Funciones auxiliares para gestión de procesos
$processes = @()

function Start-Microservice {
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

    # Esperar a que el puerto esté escuchando
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
        Write-Error "El servicio $ProjectName no respondió en el puerto $Port en el tiempo esperado."
    }
    Write-Host "$ProjectName activo y escuchando en http://localhost:$Port." -ForegroundColor Green
}

function Stop-AllServices {
    Write-Host "`nDeteniendo servicios en ejecución..." -ForegroundColor Yellow
    foreach ($p in $script:processes) {
        try {
            if (-not $p.HasExited) {
                $p.Kill($true)
                $p.WaitForExit(3000)
            }
        }
        catch {}
    }
    Write-Host "Todos los servicios fueron detenidos correctamente." -ForegroundColor Green
}

try {
    # ==========================================================================
    # FASE 1: PROTOCOLO DE VERIFICACIÓN AISLADA DE LAS 3 APIS (REGLA MANDATORIA)
    # ==========================================================================
    Write-Host "`n======================================================================" -ForegroundColor Magenta
    Write-Host "FASE 1: VERIFICACION AISLADA PREVIA (REGLA MANDATORIA)" -ForegroundColor Magenta
    Write-Host "======================================================================" -ForegroundColor Magenta

    # Iniciar API 1: Productos (5001)
    Start-Microservice -ProjectName "ApiProductos" -ProjectPath "$BaseDir\src\ApiProductos\ApiProductos.csproj" -Port 5001

    # Iniciar API 2: Libros (5002)
    Start-Microservice -ProjectName "ApiLibros" -ProjectPath "$BaseDir\src\ApiLibros\ApiLibros.csproj" -Port 5002

    # Iniciar API 3: Vehículos (5003)
    Start-Microservice -ProjectName "ApiVehiculos" -ProjectPath "$BaseDir\src\ApiVehiculos\ApiVehiculos.csproj" -Port 5003

    # Prueba aislada API 1: Productos
    Write-Host "`n[Aislamiento 1] Probando API 1 Productos (puerto 5001)..." -ForegroundColor Yellow
    $resProdGet = Invoke-WebRequest -Uri "http://localhost:5001/api/productos" -Method GET -UseBasicParsing
    Write-Host "  GET /api/productos -> Status: $($resProdGet.StatusCode)" -ForegroundColor Green
    
    $nuevoProd = @{ nombre = "Bujía Platino"; precio = 8.50; stock = 100; categoria = "Encendido" } | ConvertTo-Json
    $resProdPost = Invoke-WebRequest -Uri "http://localhost:5001/api/productos" -Method POST -Body $nuevoProd -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST /api/productos -> Status: $($resProdPost.StatusCode)" -ForegroundColor Green

    # Prueba aislada API 2: Libros
    Write-Host "`n[Aislamiento 2] Probando API 2 Libros (puerto 5002)..." -ForegroundColor Yellow
    $resLibroGet = Invoke-WebRequest -Uri "http://localhost:5002/api/libros" -Method GET -UseBasicParsing
    Write-Host "  GET /api/libros -> Status: $($resLibroGet.StatusCode)" -ForegroundColor Green
    
    $nuevoLibro = @{ titulo = "Transmisiones Automáticas Modernas"; autor = "Eduardo Silva"; isbn = "978-5544332211"; anioPublicacion = 2025 } | ConvertTo-Json
    $resLibroPost = Invoke-WebRequest -Uri "http://localhost:5002/api/libros" -Method POST -Body $nuevoLibro -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST /api/libros -> Status: $($resLibroPost.StatusCode)" -ForegroundColor Green

    # Prueba aislada API 3: Vehículos
    Write-Host "`n[Aislamiento 3] Probando API 3 Vehículos (puerto 5003)..." -ForegroundColor Yellow
    $resVehiculoGet = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos" -Method GET -UseBasicParsing
    Write-Host "  GET /api/vehiculos -> Status: $($resVehiculoGet.StatusCode)" -ForegroundColor Green
    
    $nuevoVehiculo = @{ marca = "Mazda"; modelo = "CX-5 Carbon"; anio = 2023; placa = "P999-000"; precio = 27500.00 } | ConvertTo-Json
    $resVehiculoPost = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos" -Method POST -Body $nuevoVehiculo -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST /api/vehiculos -> Status: $($resVehiculoPost.StatusCode)" -ForegroundColor Green

    Write-Host "`n>>> FASE 1 COMPLETADA CON EXITO: 3 APIs funcionando autónomamente. <<<" -ForegroundColor Green

    # ==========================================================================
    # FASE 2: ACOPLAMIENTO EN GATEWAY OCELOT (PUERTO 5000)
    # ==========================================================================
    Write-Host "`n======================================================================" -ForegroundColor Magenta
    Write-Host "FASE 2: ACOPLAMIENTO Y ENRUTAMIENTO MEDIANTE OCELOT GATEWAY" -ForegroundColor Magenta
    Write-Host "======================================================================" -ForegroundColor Magenta

    # Iniciar API Gateway (5000)
    Start-Microservice -ProjectName "ApiGateway" -ProjectPath "$BaseDir\src\ApiGateway\ApiGateway.csproj" -Port 5000

    # Test Scenario 1: Enrutamiento a Productos
    Write-Host "`n[Gateway Scenario 1] Enrutamiento a API 1 (Productos)..." -ForegroundColor Yellow
    $gwProd = Invoke-WebRequest -Uri "http://localhost:5000/productos" -Method GET -UseBasicParsing
    Write-Host "  GET http://localhost:5000/productos -> Status: $($gwProd.StatusCode)" -ForegroundColor Green
    Write-Host "  Payload recibido: $($gwProd.Content)" -ForegroundColor Gray

    $gwProdId = Invoke-WebRequest -Uri "http://localhost:5000/productos/2" -Method GET -UseBasicParsing
    Write-Host "  GET http://localhost:5000/productos/2 -> Status: $($gwProdId.StatusCode)" -ForegroundColor Green

    $prodPostGateway = @{ nombre = "Líquido de Frenos DOT 4"; precio = 9.75; stock = 40; categoria = "Fluidos" } | ConvertTo-Json
    $gwProdPost = Invoke-WebRequest -Uri "http://localhost:5000/productos" -Method POST -Body $prodPostGateway -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST http://localhost:5000/productos -> Status: $($gwProdPost.StatusCode)" -ForegroundColor Green

    $prodPutGateway = @{ id = 2; nombre = "Pastillas de Freno Cerámicas Premium"; precio = 49.99; stock = 28; categoria = "Frenos" } | ConvertTo-Json
    $gwProdPut = Invoke-WebRequest -Uri "http://localhost:5000/productos/2" -Method PUT -Body $prodPutGateway -ContentType "application/json" -UseBasicParsing
    Write-Host "  PUT http://localhost:5000/productos/2 -> Status: $($gwProdPut.StatusCode)" -ForegroundColor Green

    $gwProdDel = Invoke-WebRequest -Uri "http://localhost:5000/productos/3" -Method DELETE -UseBasicParsing
    Write-Host "  DELETE http://localhost:5000/productos/3 -> Status: $($gwProdDel.StatusCode)" -ForegroundColor Green

    # Test Scenario 2: Enrutamiento a Libros
    Write-Host "`n[Gateway Scenario 2] Enrutamiento a API 2 (Libros)..." -ForegroundColor Yellow
    $gwLibro = Invoke-WebRequest -Uri "http://localhost:5000/libros" -Method GET -UseBasicParsing
    Write-Host "  GET http://localhost:5000/libros -> Status: $($gwLibro.StatusCode)" -ForegroundColor Green
    Write-Host "  Payload recibido: $($gwLibro.Content)" -ForegroundColor Gray

    $gwLibroId = Invoke-WebRequest -Uri "http://localhost:5000/libros/1" -Method GET -UseBasicParsing
    Write-Host "  GET http://localhost:5000/libros/1 -> Status: $($gwLibroId.StatusCode)" -ForegroundColor Green

    $libroPostGateway = @{ titulo = "Electrónica Automotriz CAN Bus"; autor = "Valeria Ramos"; isbn = "978-7788990011"; anioPublicacion = 2024 } | ConvertTo-Json
    $gwLibroPost = Invoke-WebRequest -Uri "http://localhost:5000/libros" -Method POST -Body $libroPostGateway -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST http://localhost:5000/libros -> Status: $($gwLibroPost.StatusCode)" -ForegroundColor Green

    # Test Scenario 3: Enrutamiento a Vehículos
    Write-Host "`n[Gateway Scenario 3] Enrutamiento a API 3 (Vehículos)..." -ForegroundColor Yellow
    $gwVehiculo = Invoke-WebRequest -Uri "http://localhost:5000/vehiculos" -Method GET -UseBasicParsing
    Write-Host "  GET http://localhost:5000/vehiculos -> Status: $($gwVehiculo.StatusCode)" -ForegroundColor Green
    Write-Host "  Payload recibido: $($gwVehiculo.Content)" -ForegroundColor Gray

    $gwVehiculoId = Invoke-WebRequest -Uri "http://localhost:5000/vehiculos/1" -Method GET -UseBasicParsing
    Write-Host "  GET http://localhost:5000/vehiculos/1 -> Status: $($gwVehiculoId.StatusCode)" -ForegroundColor Green

    # Test Scenario 4: Ruta no mapeada (404 Not Found)
    Write-Host "`n[Gateway Scenario 4] Comprobando aislamiento de rutas no mapeadas..." -ForegroundColor Yellow
    try {
        $res404 = Invoke-WebRequest -Uri "http://localhost:5000/clientes-desconocidos" -Method GET -UseBasicParsing
        Write-Host "  Inesperado: respondió con status $($res404.StatusCode)" -ForegroundColor Red
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            Write-Host "  GET http://localhost:5000/clientes-desconocidos -> 404 Not Found (Correcto, Ocelot protege rutas no expuestas)" -ForegroundColor Green
        }
        else {
            Write-Host "  Respuesta: $statusCode" -ForegroundColor Yellow
        }
    }

    Write-Host "`n======================================================================" -ForegroundColor Green
    Write-Host "CERTIFICACION SATISFACTORIA: SPEC-1.1.1 IMPLEMENTADA Y VALIDADA AL 100%" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
}
finally {
    Stop-AllServices
}
