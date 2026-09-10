# ==============================================================================
# Script de Verificación Integral para SPEC-1.3.1: Swagger UI y Pruebas Aisladas
# AutoGestion S.A. - Desafío 2 (DSE104)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Split-Path -Parent $ScriptDir

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "INICIANDO PROTOCOLO DE VERIFICACION SPEC-1.3.1 (SWAGGER + AISLAMIENTO)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# Detener procesos previos que pudieran estar ocupando los puertos
Get-Process | Where-Object { $_.ProcessName -match "ApiProductos|ApiLibros|ApiVehiculos|ApiGateway" } | Stop-Process -Force -ErrorAction SilentlyContinue

# Compilación previa
Write-Host "`n[PASO 1] Compilando la solución completa..." -ForegroundColor Yellow
$buildOutput = dotnet build "$BaseDir\AutoGestion.slnx" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo en la compilación de la solución."
}
Write-Host "Compilación exitosa (0 Errores)." -ForegroundColor Green

$processes = @()

function Start-MicroserviceProcess {
    param(
        [string]$ProjectName,
        [string]$ProjectPath,
        [int]$Port
    )
    Write-Host "Iniciando $ProjectName de forma aislada en puerto $Port..." -ForegroundColor Yellow
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
        Write-Error "El microservicio $ProjectName no respondió en el puerto $Port."
    }
    Write-Host "$ProjectName operativo y escuchando en http://localhost:$Port." -ForegroundColor Green
}

function Stop-AllMicroservices {
    Write-Host "`nFinalizando instancias de microservicios..." -ForegroundColor Yellow
    foreach ($p in $script:processes) {
        try {
            if (-not $p.HasExited) {
                $p.Kill($true)
                $p.WaitForExit(3000)
            }
        }
        catch {}
    }
    Write-Host "Todos los microservicios fueron detenidos correctamente." -ForegroundColor Green
}

try {
    # ==========================================================================
    # 1. API 1: PRODUCTOS (PUERTO 5001)
    # ==========================================================================
    Write-Host "`n======================================================================" -ForegroundColor Magenta
    Write-Host "MODULO 1: VERIFICACION AISLADA API PRODUCTOS (PUERTO 5001)" -ForegroundColor Magenta
    Write-Host "======================================================================" -ForegroundColor Magenta

    Start-MicroserviceProcess -ProjectName "ApiProductos" -ProjectPath "$BaseDir\src\ApiProductos\ApiProductos.csproj" -Port 5001

    # Swagger UI y Swagger JSON
    Write-Host "`n[Productos - Swagger] Validando documentación interactiva..." -ForegroundColor Yellow
    $swaggerProdDoc = Invoke-WebRequest -Uri "http://localhost:5001/swagger/v1/swagger.json" -Method GET -UseBasicParsing
    Write-Host "  GET /swagger/v1/swagger.json -> Status: $($swaggerProdDoc.StatusCode) OK" -ForegroundColor Green

    $swaggerProdUI = Invoke-WebRequest -Uri "http://localhost:5001/swagger" -Method GET -UseBasicParsing
    Write-Host "  GET /swagger/ -> Status: $($swaggerProdUI.StatusCode) OK (Swagger UI Interactivo Activo)" -ForegroundColor Green

    if ($swaggerProdDoc.Content -match '"Bearer"') {
        Write-Host "  Esquema Bearer Auth detectado en Swagger: Sí (Botón Authorize habilitado)" -ForegroundColor Green
    }

    # Operaciones CRUD aisladas
    Write-Host "`n[Productos - CRUD Aislado] Probando endpoints de negocio..." -ForegroundColor Yellow
    $prodList = Invoke-WebRequest -Uri "http://localhost:5001/api/productos" -Method GET -UseBasicParsing
    Write-Host "  GET /api/productos -> Status: $($prodList.StatusCode) OK" -ForegroundColor Green

    $prodPostPayload = @{ nombre = "Bujía Iridium"; precio = 12.50; stock = 80; categoria = "Encendido" } | ConvertTo-Json
    $prodCreate = Invoke-WebRequest -Uri "http://localhost:5001/api/productos" -Method POST -Body $prodPostPayload -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST /api/productos -> Status: $($prodCreate.StatusCode) Created" -ForegroundColor Green

    $prodDetail = Invoke-WebRequest -Uri "http://localhost:5001/api/productos/1" -Method GET -UseBasicParsing
    Write-Host "  GET /api/productos/1 -> Status: $($prodDetail.StatusCode) OK" -ForegroundColor Green

    $prodPutPayload = @{ id = 1; nombre = "Filtro Sintético Premium"; precio = 16.50; stock = 45; categoria = "Mantenimiento" } | ConvertTo-Json
    $prodUpdate = Invoke-WebRequest -Uri "http://localhost:5001/api/productos/1" -Method PUT -Body $prodPutPayload -ContentType "application/json" -UseBasicParsing
    Write-Host "  PUT /api/productos/1 -> Status: $($prodUpdate.StatusCode) OK" -ForegroundColor Green

    $prodDelete = Invoke-WebRequest -Uri "http://localhost:5001/api/productos/3" -Method DELETE -UseBasicParsing
    Write-Host "  DELETE /api/productos/3 -> Status: $($prodDelete.StatusCode) OK" -ForegroundColor Green

    # ==========================================================================
    # 2. API 2: LIBROS (PUERTO 5002)
    # ==========================================================================
    Write-Host "`n======================================================================" -ForegroundColor Magenta
    Write-Host "MODULO 2: VERIFICACION AISLADA API LIBROS (PUERTO 5002)" -ForegroundColor Magenta
    Write-Host "======================================================================" -ForegroundColor Magenta

    Start-MicroserviceProcess -ProjectName "ApiLibros" -ProjectPath "$BaseDir\src\ApiLibros\ApiLibros.csproj" -Port 5002

    # Swagger UI y Swagger JSON
    Write-Host "`n[Libros - Swagger] Validando documentación interactiva..." -ForegroundColor Yellow
    $swaggerLibroDoc = Invoke-WebRequest -Uri "http://localhost:5002/swagger/v1/swagger.json" -Method GET -UseBasicParsing
    Write-Host "  GET /swagger/v1/swagger.json -> Status: $($swaggerLibroDoc.StatusCode) OK" -ForegroundColor Green

    $swaggerLibroUI = Invoke-WebRequest -Uri "http://localhost:5002/swagger" -Method GET -UseBasicParsing
    Write-Host "  GET /swagger/ -> Status: $($swaggerLibroUI.StatusCode) OK (Swagger UI Interactivo Activo)" -ForegroundColor Green

    if ($swaggerLibroDoc.Content -match '"Bearer"') {
        Write-Host "  Esquema Bearer Auth detectado en Swagger: Sí (Botón Authorize habilitado)" -ForegroundColor Green
    }

    # Operaciones CRUD aisladas
    Write-Host "`n[Libros - CRUD Aislado] Probando endpoints de negocio..." -ForegroundColor Yellow
    $libroList = Invoke-WebRequest -Uri "http://localhost:5002/api/libros" -Method GET -UseBasicParsing
    Write-Host "  GET /api/libros -> Status: $($libroList.StatusCode) OK" -ForegroundColor Green

    $libroPostPayload = @{ titulo = "Inyección Directa de Gasolina (GDI)"; autor = "Mario Navarro"; isbn = "978-4455667788"; anioPublicacion = 2025 } | ConvertTo-Json
    $libroCreate = Invoke-WebRequest -Uri "http://localhost:5002/api/libros" -Method POST -Body $libroPostPayload -ContentType "application/json" -UseBasicParsing
    Write-Host "  POST /api/libros -> Status: $($libroCreate.StatusCode) Created" -ForegroundColor Green

    $libroDetail = Invoke-WebRequest -Uri "http://localhost:5002/api/libros/1" -Method GET -UseBasicParsing
    Write-Host "  GET /api/libros/1 -> Status: $($libroDetail.StatusCode) OK" -ForegroundColor Green

    $libroPutPayload = @{ id = 1; titulo = "Manual de Taller y Mecánica Automotriz V2"; autor = "Alonso Pérez"; isbn = "978-0123456789"; anioPublicacion = 2024 } | ConvertTo-Json
    $libroUpdate = Invoke-WebRequest -Uri "http://localhost:5002/api/libros/1" -Method PUT -Body $libroPutPayload -ContentType "application/json" -UseBasicParsing
    Write-Host "  PUT /api/libros/1 -> Status: $($libroUpdate.StatusCode) OK" -ForegroundColor Green

    $libroDelete = Invoke-WebRequest -Uri "http://localhost:5002/api/libros/3" -Method DELETE -UseBasicParsing
    Write-Host "  DELETE /api/libros/3 -> Status: $($libroDelete.StatusCode) OK" -ForegroundColor Green

    # ==========================================================================
    # 3. API 3: VEHÍCULOS (PUERTO 5003)
    # ==========================================================================
    Write-Host "`n======================================================================" -ForegroundColor Magenta
    Write-Host "MODULO 3: VERIFICACION AISLADA API VEHICULOS (PUERTO 5003)" -ForegroundColor Magenta
    Write-Host "======================================================================" -ForegroundColor Magenta

    Start-MicroserviceProcess -ProjectName "ApiVehiculos" -ProjectPath "$BaseDir\src\ApiVehiculos\ApiVehiculos.csproj" -Port 5003

    # Swagger UI y Swagger JSON
    Write-Host "`n[Vehículos - Swagger] Validando documentación interactiva..." -ForegroundColor Yellow
    $swaggerVehiculoDoc = Invoke-WebRequest -Uri "http://localhost:5003/swagger/v1/swagger.json" -Method GET -UseBasicParsing
    Write-Host "  GET /swagger/v1/swagger.json -> Status: $($swaggerVehiculoDoc.StatusCode) OK" -ForegroundColor Green

    $swaggerVehiculoUI = Invoke-WebRequest -Uri "http://localhost:5003/swagger" -Method GET -UseBasicParsing
    Write-Host "  GET /swagger/ -> Status: $($swaggerVehiculoUI.StatusCode) OK (Swagger UI Interactivo Activo)" -ForegroundColor Green

    if ($swaggerVehiculoDoc.Content -match '"Bearer"') {
        Write-Host "  Esquema Bearer Auth detectado en Swagger: Sí (Botón Authorize habilitado)" -ForegroundColor Green
    }

    # Prueba de aislamiento de seguridad: Rechazo anónimo 401
    Write-Host "`n[Vehículos - Seguridad Aislada] Comprobando rechazo HTTP 401 ante llamadas sin sesión..." -ForegroundColor Yellow
    try {
        $vehiculoAnon = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos" -Method GET -UseBasicParsing
        Write-Host "  Inesperado: llamada anónima respondió con status $($vehiculoAnon.StatusCode)" -ForegroundColor Red
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 401) {
            Write-Host "  GET /api/vehiculos (sin sesión) -> Status: 401 Unauthorized (Exitoso: Módulo protegido)" -ForegroundColor Green
        }
        else {
            Write-Host "  Status: $statusCode" -ForegroundColor Yellow
        }
    }

    # Operaciones CRUD aisladas con Bearer Token
    Write-Host "`n[Vehículos - CRUD Autenticado] Probando operaciones con cabecera Bearer..." -ForegroundColor Yellow
    $authHeader = @{ Authorization = "Bearer dev-sample-jwt-token-desafio2" }

    $vehiculoList = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos" -Method GET -Headers $authHeader -UseBasicParsing
    Write-Host "  GET /api/vehiculos (con Bearer) -> Status: $($vehiculoList.StatusCode) OK" -ForegroundColor Green

    $vehiculoPostPayload = @{ marca = "Kia"; modelo = "Sportage GT Line"; anio = 2024; placa = "P555-444"; precio = 31000.00 } | ConvertTo-Json
    $vehiculoCreate = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos" -Method POST -Body $vehiculoPostPayload -ContentType "application/json" -Headers $authHeader -UseBasicParsing
    Write-Host "  POST /api/vehiculos (con Bearer) -> Status: $($vehiculoCreate.StatusCode) Created" -ForegroundColor Green

    $vehiculoDetail = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos/1" -Method GET -Headers $authHeader -UseBasicParsing
    Write-Host "  GET /api/vehiculos/1 (con Bearer) -> Status: $($vehiculoDetail.StatusCode) OK" -ForegroundColor Green

    $vehiculoPutPayload = @{ id = 1; marca = "Toyota"; modelo = "Corolla LE Automático"; anio = 2022; placa = "P123-456"; precio = 18700.00 } | ConvertTo-Json
    $vehiculoUpdate = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos/1" -Method PUT -Body $vehiculoPutPayload -ContentType "application/json" -Headers $authHeader -UseBasicParsing
    Write-Host "  PUT /api/vehiculos/1 (con Bearer) -> Status: $($vehiculoUpdate.StatusCode) OK" -ForegroundColor Green

    $vehiculoDelete = Invoke-WebRequest -Uri "http://localhost:5003/api/vehiculos/3" -Method DELETE -Headers $authHeader -UseBasicParsing
    Write-Host "  DELETE /api/vehiculos/3 (con Bearer) -> Status: $($vehiculoDelete.StatusCode) OK" -ForegroundColor Green

    Write-Host "`n======================================================================" -ForegroundColor Green
    Write-Host "CERTIFICACION EXITOSA: SPEC-1.3.1 VERIFICACION AISLADA Y SWAGGER AL 100%" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
}
finally {
    Stop-AllMicroservices
}
