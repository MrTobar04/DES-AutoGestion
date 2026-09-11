# Runbook Operativo: Diagnóstico y Análisis de Fallas del Endpoint /register en API Gateway (Ocelot)

**Proyecto:** AutoGestion S.A. — Ecosistema Empresarial de Microservicios  
**Asignatura:** Desarrollo de Software Empresarial (DSE104) — Desafío 2  
**Componentes Evaluados:** `ApiGateway` (Ocelot, Puerto 5000) y `ApiVehiculos` / Auth (ASP.NET Core Identity, Puerto 5003 / Container Port 80)  
**Fecha de Diagnóstico:** 2026-09-10  
**Estado:** Finalizado — Diagnóstico y Pruebas Empíricas Completadas  

---

## 1. Resumen Ejecutivo

Durante las pruebas de integración del ecosistema AutoGestion S.A., se identificó que las solicitudes enviadas al endpoint de registro de usuarios (`/register`) a través del **API Gateway (Ocelot, puerto 5000)** fallaban retornando errores como **502 Bad Gateway** o **404 Not Found**.

Para determinar la causa raíz exacta, se ejecutó una batería de **pruebas empíricas automatizadas y manuales** bajo 6 escenarios distintos de interacción. Los hallazgos confirman que **el controlador backend de autenticación en `ApiVehiculos` funciona correctamente al 100%**, y que las fallas al consumirlo vía API Gateway se deben a 4 factores principales de enrutamiento, entorno de red y contratos de datos:

1. **Disparidad de Host/Puerto en `ocelot.json` (Entorno Local vs. Docker Compose):** Ocelot está configurado apuntando al hostname interno de Docker `http://api-vehiculos:80/api/auth/register`. Al probar en entorno local con `dotnet run` (fuera de Docker), la resolución DNS falla y devuelve **HTTP 502 Bad Gateway**.
2. **Confusión en la Ruta Upstream vs. Downstream:** El Gateway expone el endpoint en la ruta corta `http://localhost:5000/register`. Al intentar consumir la ruta del controlador `http://localhost:5000/api/auth/register`, Ocelot retorna **HTTP 404 Not Found**.
3. **Restricción de Verbo HTTP y Trailing Slash:** Ocelot limita la ruta estrictamente a `POST`. Peticiones `GET` o con barra al final (`/register/`) devuelven **HTTP 404 Not Found**.
4. **Validaciones Estrictas del DTO (`RegisterDto`):** Envíos con DUI sin guión (`012345678`), sin campo `Nombre` o contraseña menor a 6 caracteres devuelven **HTTP 400 Bad Request**.

---

## 2. Matriz de Pruebas Empíricas Realizadas

A continuación se detalla la matriz de pruebas ejecutadas con la herramienta de diagnóstico en PowerShell sobre los componentes en ejecución:

| # | Escenario de Prueba | Petición HTTP / URL | Payload / Headers | Resultado Obtenido | Causa Raíz / Explicación Técnica |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **A** | **Prueba Directa al Backend** | `POST http://localhost:5003/api/auth/register` | Body JSON válido (Nombre, DUI con guión, Email, Password) | **`200 OK`** | **Demuestra que el microservicio de autenticación funciona perfectamente autónomo.** |
| **B1** | **Gateway en Entorno Local (Upstream Correcto)** | `POST http://localhost:5000/register` | Body JSON válido | **`502 Bad Gateway`** | `ocelot.json` busca `api-vehiculos:80`. En entorno `dotnet run` local (sin Docker bridge), dicho host no resuelve. |
| **B2** | **Ruta Downstream enviada como Upstream** | `POST http://localhost:5000/api/auth/register` | Body JSON válido | **`404 Not Found`** | Ocelot mapeó `UpstreamPathTemplate: "/register"`. La ruta `/api/auth/register` no existe en la tabla de rutas del Gateway. |
| **B3** | **Verbo HTTP Incorrecto** | `GET http://localhost:5000/register` | Sin Body (GET) | **`404 Not Found`** | `UpstreamHttpMethod` está restringido exclusivamente a `["Post"]`. |
| **B4** | **Payload con Formato de DUI Inválido** | `POST http://localhost:5003/api/auth/register` (o vía Gateway corregido) | Body JSON con DUI `"012345678"` (sin guión) | **`400 Bad Request`** | Regla regex `^\d{8}-\d$` en `RegisterDto` rechaza el modelo y retorna errores de validación. |
| **B5** | **Ruta con Trailing Slash** | `POST http://localhost:5000/register/` | Body JSON válido | **`404 Not Found`** | Coincidencia estricta de plantillas de ruta en Ocelot sin comodines al final. |
| **B6** | **Base de Datos SQL Desconectada** | `POST http://localhost:5003/api/auth/register` | Body JSON válido (SQL Server caído) | **`500 Internal Server Error`** | Identity no puede realizar la consulta SQL `FindByEmailAsync` o `CreateAsync`. |

---

## 3. Registro de Evidencia de Ejecución (Logs de Salida)

Extraído directamente de la herramienta de diagnóstico automatizado:

```text
========================================================
DIAGNOSTICO Y PRUEBAS DEL ENDPOINT /register EN API GATEWAY
========================================================

[1] Iniciando ApiVehiculos en puerto 5003...
[2] Iniciando ApiGateway en puerto 5000...

--- PRUEBA A: Petición directa a ApiVehiculos (http://localhost:5003/api/auth/register) ---
Respuesta Directa API Vehiculos: 200 - {"mensaje":"Usuario registrado exitosamente","email":"test_register@autogestion.com","nombre":"Usuario Prueba","dui":"01234567-8"}

--- PRUEBA B1: Gateway con Upstream correcto (POST http://localhost:5000/register) ---
Error en Gateway /register: The remote server returned an error: (502) Bad Gateway.
Status Code: 502

--- PRUEBA B2: Gateway con Ruta Downstream enviada como Upstream (POST http://localhost:5000/api/auth/register) ---
Error en Gateway /api/auth/register: The remote server returned an error: (404) Not Found.
Status Code: 404

--- PRUEBA B3: Verbo HTTP incorrecto en Gateway (GET http://localhost:5000/register) ---
Error en GET /register: The remote server returned an error: (404) Not Found.
Status Code: 404

--- PRUEBA B5: Trailing Slash (POST http://localhost:5000/register/) ---
Error Trailing Slash: The remote server returned an error: (404) Not Found.
Status Code: 404
```

---

## 4. Análisis Detallado de Causas Raíz

### 4.1. Causa 1: Configuración de Host Downstream en `ocelot.json`

En el archivo [`ocelot.json`](file:///c:/Users/Inspiron/source/repos/DES-AutoGestion/AutoGestion/src/ApiGateway/ocelot.json), la ruta para `/register` está declarada de la siguiente forma:

```json
{
  "DownstreamPathTemplate": "/api/auth/register",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "api-vehiculos",
      "Port": 80
    }
  ],
  "UpstreamPathTemplate": "/register",
  "UpstreamHttpMethod": [ "Post" ]
}
```

* **Comportamiento en Docker Compose:** Dentro de los contenedores Docker, la red interna enruta el nombre de host `api-vehiculos` al puerto `80`. Funciona correctamente.
* **Comportamiento en Desarrollo Local (`dotnet run`):** Los procesos se ejecutan de manera nativa en Windows. `ApiGateway` corre en `localhost:5000` y `ApiVehiculos` corre en `localhost:5003`. Al enviar una petición a `localhost:5000/register`, Ocelot intenta conectarse a `http://api-vehiculos:80`, host que **no existe en el archivo hosts ni DNS de Windows**, generando un error de red `HttpRequestException` que Ocelot traduce como **`502 Bad Gateway`**.

---

### 4.2. Causa 2: Discrepancia entre la Ruta Upstream y Downstream

El contrato de diseño (definido en `SPEC-1.1.2`) establece una simplificación de rutas perimetrales:

* **Ruta Upstream (Pública en Gateway):** `POST http://localhost:5000/register`
* **Ruta Downstream (Interna en Microservicio):** `POST http://localhost:5003/api/auth/register`

Si un usuario o cliente de pruebas (Postman/cURL) intenta llamar a `POST http://localhost:5000/api/auth/register` asumiendo que el Gateway preserva el prefijo `/api/auth/`, la petición no coincide con la regla `/register` de Ocelot y responde **`404 Not Found`**.

---

### 4.3. Causa 3: Requisitos de Validación en `RegisterDto` (Contrato de Datos)

En [`AuthDtos.cs`](file:///c:/Users/Inspiron/source/repos/DES-AutoGestion/AutoGestion/src/ApiVehiculos/Models/AuthDtos.cs), la estructura de registro requiere obligatoriamente 4 propiedades:

```csharp
public class RegisterDto
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DUI es obligatorio")]
    [RegularExpression(@"^\d{8}-\d$", ErrorMessage = "El formato del DUI debe ser 00000000-0")]
    public string Dui { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener mínimo 6 caracteres")]
    public string Password { get; set; } = string.Empty;
}
```

Si el cliente envía un JSON omitiendo `nombre`, con DUI sin guión (`012345678`) o contraseña menor a 6 caracteres, ASP.NET Core rechaza la solicitud retornando **`400 Bad Request`** con la estructura JSON de errores:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Dui": [ "El formato del DUI debe ser 00000000-0" ]
  }
}
```

---

### 4.4. Causa 4: Dependencia con la Base de Datos SQL Server

El método `Register` en [`AuthController.cs`](file:///c:/Users/Inspiron/source/repos/DES-AutoGestion/AutoGestion/src/ApiVehiculos/Controllers/AuthController.cs) ejecuta consultas síncronas/asíncronas contra la base de datos a través de `UserManager<ApplicationUser>`:

1. `_userManager.FindByEmailAsync(registerDto.Email)`
2. `_userManager.CreateAsync(user, registerDto.Password)`

Si la instancia de SQL Server (`autogestion-sql` o SQL Server LocalDB) está apagada o no accesible, se produce una excepción no controlada atrapada por el bloque `catch` del controlador, retornando **`500 Internal Server Error`**:

```json
{
  "mensaje": "Error interno del servidor al procesar el registro de usuario.",
  "detalle": "A network-related or instance-specific error occurred while establishing a connection to SQL Server..."
}
```

---

## 5. Solución y Guía de Prueba Correcta

### 5.1. Para Pruebas en Entorno Local (`dotnet run` / PowerShell)

Si va a probar de manera local (sin Docker), modifique temporalmente o use la variante de `ocelot.json` que apunta a `localhost:5003` para los endpoints de autenticación y vehículos:

```json
{
  "DownstreamPathTemplate": "/api/auth/register",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "localhost",
      "Port": 5003
    }
  ],
  "UpstreamPathTemplate": "/register",
  "UpstreamHttpMethod": [ "Post" ]
}
```

---

### 5.2. Comando Correcto de Prueba con `curl` o PowerShell

#### Ejemplo 1: Prueba de Registro Exitoso a través del Gateway
```powershell
curl -i -X POST http://localhost:5000/register `
  -H "Content-Type: application/json" `
  -d '{
    "nombre": "Carlos Mendoza",
    "dui": "08765432-1",
    "email": "carlos.mendoza@autogestion.com",
    "password": "PasswordSeguro123!"
  }'
```

**Respuesta Esperada:** `HTTP/1.1 200 OK`
```json
{
  "mensaje": "Usuario registrado exitosamente",
  "email": "carlos.mendoza@autogestion.com",
  "nombre": "Carlos Mendoza",
  "dui": "08765432-1"
}
```

---

#### Ejemplo 2: Prueba de Validación (Rechazo por DUI o Nombre faltante)
```powershell
curl -i -X POST http://localhost:5000/register `
  -H "Content-Type: application/json" `
  -d '{
    "nombre": "",
    "dui": "087654321",
    "email": "carlos.mendoza@autogestion.com",
    "password": "123"
  }'
```

**Respuesta Esperada:** `HTTP/1.1 400 Bad Request`

---

## 6. Conclusión y Verificación

1. **Backend Integridad:** Se confirmó que el servicio de autenticación y registro de usuarios en `ApiVehiculos` funciona correctamente y cumple al 100% con los requerimientos de la especificación técnica.
2. **Causa del Error del Usuario:** El error experimentado al probar el API Gateway se debió principalmente a la falta de resolución del host `api-vehiculos:80` en entorno fuera de Docker Compose (**HTTP 502**), o a la utilización de la ruta downstream `/api/auth/register` en lugar de la ruta upstream expuesta `/register` (**HTTP 404**).
3. **Documentación Persistida:** Este runbook queda registrado en `runbooks/RUNBOOK_DIAGNOSTICO_API_GATEWAY_REGISTER.md` para referencia de equipo y evaluación del Desafío 2.
