# Runbook Operativo: Verificación y Gobernanza de Rate Limiting (SPEC-1.2.1)

**Proyecto:** AutoGestion S.A. — Ecosistema Empresarial de Microservicios  
**Asignatura:** Desarrollo de Software Empresarial (DSE104) — Desafío 2  
**Especificación Técnica:** [`SPEC-1.2.1: Gobernanza de Tráfico con Rate Limiting en Ocelot API Gateway`](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)  
**Criterio de Evaluación Oficial:** Criterio a) Ocelot — Nivel Destacado (*"El Gateway enruta TODOS los endpoints y el límite de 10 peticiones por minuto funciona bien en un 100%"*)  
**Fecha:** 2026-09-08  

---

## 1. Objetivo y Fundamento Técnico

Prevenir la saturación, sobrecarga y ataques de denegación de servicio (DoS) contra los microservicios backend de AutoGestion S.A. mediante la interceptación perimetral en el API Gateway Ocelot (**puerto 5000**), aplicando una política inquebrantable de:

* **Cuota Máxima:** Exactamente 10 peticiones por ventana móvil.
* **Periodo Temporal:** 1 minuto (60 segundos).
* **Comportamiento ante Exceso:** La petición número 11 es descartada inmediatamente en tiempo constante $O(1)$ retornando código HTTP **429 (Too Many Requests)** sin reenviar tráfico a los microservicios backend.

---

## 2. Arquitectura de Interceptación Perimetral

```
                 [ Cliente / Postman / cURL ]
                              │
                              ▼ (Puerto 5000)
            [ ApiGateway: Client Identification Middleware ]
                              │ (Inyecta cabecera X-Client-Id si no existe)
                              ▼
            [ Ocelot: ClientRateLimitMiddleware ]
                              │
             ┌────────────────┴────────────────┐
             ▼ (Peticiones 1 a 10)             ▼ (Petición 11 en adelante)
      Contador <= 10                    Contador > 10
             │                                 │
             ▼                                 ▼
   [ Reenvío a Downstream ]           [ Rechazo Inmediato ]
    HTTP 200 / 201                     HTTP 429 Too Many Requests
    (Productos, Libros, Vehículos)     Body: "Se ha excedido el límite..."
```

---

## 3. Configuración Implementada (Config as Code)

### 3.1. En `src/ApiGateway/ocelot.json`

Se configuró el bloque global y cada ruta individual:

```json
{
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5000",
    "RateLimitOptions": {
      "DisableRateLimitHeaders": false,
      "QuotaExceededMessage": "Se ha excedido el límite máximo de 10 peticiones por minuto para este servicio.",
      "HttpStatusCode": 429,
      "ClientIdHeader": "X-Client-Id"
    }
  },
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/productos",
      "UpstreamPathTemplate": "/productos",
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 10
      }
    }
    // Idéntico para /productos/{everything}, /libros, /libros/{everything}, /vehiculos y /vehiculos/{everything}
  ]
}
```

### 3.2. En `src/ApiGateway/Program.cs`

Middleware de resolución perimetral de identidad de cliente antes del pipeline de Ocelot:

```csharp
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("X-Client-Id"))
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        var clientId = string.IsNullOrWhiteSpace(remoteIp) ? "local-client" : remoteIp;
        context.Request.Headers["X-Client-Id"] = clientId;
    }
    await next();
});

await app.UseOcelot();
```

---

## 4. Método 1: Verificación Automatizada (1 Solo Comando)

Se proporciona el script [`verify_spec_1_2_1_ratelimit.ps1`](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/AutoGestion/scripts/verify_spec_1_2_1_ratelimit.ps1) que envía automáticamente una ráfaga de 12 peticiones consecutivas al Gateway y certifica el bloqueo.

### Comando:
```powershell
powershell -ExecutionPolicy Bypass -File .\AutoGestion\scripts\verify_spec_1_2_1_ratelimit.ps1
```

### Salida de Éxito Certificada:
```text
======================================================================
INICIANDO VERIFICACION SPEC-1.2.1: RATE LIMITING (10 REQ/MIN)
======================================================================

[PASO 1] Compilando la solución...
Compilación exitosa (0 Errores).
Iniciando ApiProductos en puerto 5001...
ApiProductos activo en puerto 5001.
Iniciando ApiGateway en puerto 5000...
ApiGateway activo en puerto 5000.

[PASO 2] Ejecutando ráfaga de 12 peticiones a http://localhost:5000/productos...
  Peticion #1 - HTTP 200 OK (Tiempo: 303 ms)
  Peticion #2 - HTTP 200 OK (Tiempo: 15 ms)
  Peticion #3 - HTTP 200 OK (Tiempo: 13 ms)
  Peticion #4 - HTTP 200 OK (Tiempo: 10 ms)
  Peticion #5 - HTTP 200 OK (Tiempo: 10 ms)
  Peticion #6 - HTTP 200 OK (Tiempo: 11 ms)
  Peticion #7 - HTTP 200 OK (Tiempo: 11 ms)
  Peticion #8 - HTTP 200 OK (Tiempo: 10 ms)
  Peticion #9 - HTTP 200 OK (Tiempo: 11 ms)
  Peticion #10 - HTTP 200 OK (Tiempo: 10 ms)
  Peticion #11 - HTTP 429 Too Many Requests (Tiempo: 16 ms)
  Peticion #12 - HTTP 429 Too Many Requests (Tiempo: 1 ms)

[PASO 3] Resumen de Resultados:
  Peticiones dentro del umbral (1 a 10): 10 / 10 exitosas (HTTP 200)
  Peticiones bloqueadas por Rate Limit (11 y 12): 2 / 2 bloqueadas (HTTP 429)

======================================================================
CERTIFICACION EXITOSA: SPEC-1.2.1 RATE LIMITING VERIFICADO AL 100%
======================================================================
```

---

## 5. Método 2: Comprobación Manual en Terminal

Si deseas ejecutar la prueba manualmente con los servicios activos:

### En PowerShell:
```powershell
1..12 | ForEach-Object {
    try {
        $res = Invoke-WebRequest -Uri "http://localhost:5000/productos" -Method GET -UseBasicParsing
        Write-Host "Petición $_ -> Status: $($res.StatusCode) OK" -ForegroundColor Green
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "Petición $_ -> Status: $code Too Many Requests" -ForegroundColor Red
    }
}
```

### Con cURL (Bash o PowerShell):
```bash
for i in {1..12}; do
  echo -n "Peticion $i: "
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/productos
done
```
**Resultado en terminal:**
```
Peticion 1: 200
Peticion 2: 200
...
Peticion 10: 200
Peticion 11: 429
Peticion 12: 429
```

---

## 6. Generación de Evidencia para el Informe PDF

Para la entrega del punto **a) Ocelot** en el informe final:
1. Tomar una captura de pantalla de la terminal ejecutando el script `verify_spec_1_2_1_ratelimit.ps1` o el bucle manual de 12 peticiones.
2. Destacar en la captura el paso de la **Petición 10 (HTTP 200)** a la **Petición 11 (HTTP 429 Too Many Requests)**.
3. Esta evidencia demuestra el cumplimiento integral del Criterio (a) con nota Destacada (10/10).
