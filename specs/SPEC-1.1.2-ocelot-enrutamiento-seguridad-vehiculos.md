# SPEC-1.1.2: Ocelot Enrutamiento Perimetral, Contratos de Autenticación (/register, /login) y Control de Acceso para la Tercera API (Vehículos)

## 1. Objective
Configurar en el API Gateway Ocelot el enrutamiento perimetral especializado, la definición autónoma de los contratos de entrada y salida para los endpoints de autenticación (`/register` y `/login`) incorporando los campos `Nombre` y `DUI` (formato salvadoreño `00000000-0`), la propagación íntegra de cabeceras de autorización HTTP (`Authorization: Bearer <token>`) y el traslado fidedigno de códigos de estado de seguridad hacia y desde la **tercera API de AutoGestion S.A. (API 3: Vehículos)** y el subsistema de Autenticación, garantizando que las llamadas anónimas a la tercera API sean rechazadas con HTTP 401 (Unauthorized) y las peticiones autenticadas sean despachadas con latencia agregada menor a 10 ms sin bloqueos de dependencia con módulos posteriores.

## 2. Scope
### 2.1. Included
* Definición completa y autónoma de los contratos de datos (DTOs) y especificación de endpoints para `/register` y `/login`.
* Validación de parámetros de entrada: `Nombre` obligatorio, `DUI` obligatorio con formato `00000000-0`, `Email` con formato válido y `Password` con longitud mínima.
* Enrutamiento perimetral de los endpoints públicos de gestión de usuarios `/register` y `/login` a través del Gateway.
* Enrutamiento perimetral protegido hacia la **tercera API (Vehículos)** para todos los métodos HTTP (`GET`, `POST`, `PUT`, `DELETE`).
* Configuración de paso transparente (*pass-through*) de la cabecera `Authorization: Bearer <jwt_token>` hacia el downstream de la tercera API.
* Traslado directo y transparente del código de error HTTP 401 (*Unauthorized*) emitido por la tercera API cuando el cliente no posee sesión activa.
* Soporte para pruebas conjuntas e individuales de la tercera API detrás del Gateway.

### 2.2. Not Included (Out of Scope)
* Implementación interna del motor de persistencia relacional Identity / migraciones de base de datos profunda (especificado en módulo de seguridad).
* Rate Limiting perimetral (delegado a [SPEC-1.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)).
* Enrutamiento básico de las otras dos APIs (Productos y Libros, cubierto en [SPEC-1.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.1-ocelot-enrutamiento-productos-libros.md)).

## 3. Context and Restrictions
* **Context:** Representa el canal de seguridad y gobernanza de acceso del Gateway. A diferencia de las APIs de Productos y Libros (que son públicas), la **tercera API (Vehículos)** contiene información confidencial protegida por requerimiento directo del negocio: *"Solo la gente registrada puede ver los vehículos... devuelva error 401 si no ha iniciado sesión"*. Para desbloquear la ejecución temprana del Gateway y pruebas perimetrales, este spec define explícitamente los contratos de entrada/salida de autenticación.
* **Restrictions:**
  * El campo `Nombre` es estrictamente obligatorio en el registro de usuarios.
  * El campo `DUI` debe cumplir estrictamente con la expresión regular `^\d{8}-\d$` (formato salvadoreño `00000000-0`).
  * Ninguna cabecera `Authorization` debe ser filtrada, truncada o suprimida al transitar por el pipeline de Ocelot hacia la tercera API.
  * Si la tercera API de Vehículos responde con HTTP 401 Unauthorized, Ocelot debe trasladar dicho código 401 sin transformarlo en 500 o 404.
  * La tercera API de Vehículos debe ser verificable de manera aislada antes de habilitar su reenvío perimetral.

## 4. Design (Implementation Details)
* **Architecture:**
```text
  [ Cliente / Swagger / Postman ]
                 │
                 ▼
      [ Ocelot API Gateway ] (Puerto 5000)
                 │
   ┌─────────────┴─────────────────────────────────────┐
   ▼ (POST /register, POST /login)                     ▼ (GET/POST/PUT/DELETE /vehiculos)
   │                                                   │ [Propagación Header: Bearer Token]
   ▼                                                   ▼
[ Servicio Auth / Identity ]               [ Tercera API: Vehículos ] (Puerto 5003)
   Retorna Token JWT                                   │
                                           ┌───────────┴───────────┐
                                           ▼ (Sin Token)           ▼ (Con Token Válido)
                                      HTTP 401               HTTP 200 OK
                                           │                       │
                                           └───────────┬───────────┘
                                                       ▼
                                          Traslado Transparente por Ocelot
```

* **Data Model (DTOs y Contratos de Entrada/Salida):**
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

public class LoginDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public string Email { get; set; } = string.Empty;
}
```

* **Data Model (Fragmento de Configuración en `ocelot.json`):**
```json
{
  "Routes": [
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
      "UpstreamHttpMethod": [ "Post" ],
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 10
      }
    },
    {
      "DownstreamPathTemplate": "/api/auth/login",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5003
        }
      ],
      "UpstreamPathTemplate": "/login",
      "UpstreamHttpMethod": [ "Post" ],
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 10
      }
    },
    {
      "DownstreamPathTemplate": "/api/vehiculos",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5003
        }
      ],
      "UpstreamPathTemplate": "/vehiculos",
      "UpstreamHttpMethod": [ "Get", "Post", "Put", "Delete" ],
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 10
      }
    },
    {
      "DownstreamPathTemplate": "/api/vehiculos/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5003
        }
      ],
      "UpstreamPathTemplate": "/vehiculos/{everything}",
      "UpstreamHttpMethod": [ "Get", "Put", "Delete" ],
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 10
      }
    }
  ]
}
```

* **API Contracts:**
  * `POST /register` (o `/api/auth/register`):
    * **Entrada (`RegisterDto`):**
      ```json
      {
        "nombre": "Juan Pérez",
        "dui": "01234567-8",
        "email": "usuario@correo.com",
        "password": "PasswordSeguro123!"
      }
      ```
    * **Salida:** HTTP 200/201 OK (`"Usuario registrado exitosamente"`) o HTTP 400 Bad Request con lista de errores de validación (`"El nombre es obligatorio"`, `"El formato del DUI debe ser 00000000-0"`).
  * `POST /login` (o `/api/auth/login`):
    * **Entrada (`LoginDto`):**
      ```json
      {
        "email": "usuario@correo.com",
        "password": "PasswordSeguro123!"
      }
      ```
    * **Salida:** HTTP 200 OK con `AuthResponseDto` (`{ "token": "ey...", "expiration": "2026-09-09T22:00:00Z", "email": "usuario@correo.com" }`) o HTTP 401 Unauthorized (`"Credenciales inválidas"`).
  * `GET /vehiculos` (Sin Token):
    * **Salida:** HTTP 401 Unauthorized traslado transparente por Ocelot.
  * `GET /vehiculos` (Con Cabecera `Authorization: Bearer <jwt_valido>`):
    * **Salida:** HTTP 200 OK con el listado JSON de vehículos.
* **UI/UX:** En la interfaz interactiva (Swagger UI o Postman), el evaluador puede ejecutar el ciclo completo: registro con `POST /register` proveyendo Nombre y DUI válidos, autenticación con `POST /login` para recibir el token JWT, asignación en la cabecera Bearer Token, y consumo exitoso de `GET /vehiculos` desbloqueando el paso de 401 a 200 OK.

## 5. Acceptance Criteria
* **Scenario 1: Rechazo Estricto HTTP 401 al Consumir la Tercera API sin Sesión (Rúbrica Oficial)**
  * **Given** el Gateway Ocelot operativo y la tercera API de Vehículos activa en downstream,
  * **When** un cliente envía una petición `GET http://localhost:5000/vehiculos` sin cabecera `Authorization`,
  * **Then** Ocelot reenvía la solicitud a la tercera API, recibe la respuesta de rechazo y entrega al cliente final el código de estado HTTP 401 Unauthorized.
* **Scenario 2: Acceso Exitoso a la Tercera API con Propagación de Token Válido**
  * **Given** un token JWT válido obtenido previamente mediante `POST http://localhost:5000/login`,
  * **When** el cliente envía `GET http://localhost:5000/vehiculos` adjuntando la cabecera `Authorization: Bearer <token_valido>`,
  * **Then** Ocelot propaga íntegramente la cabecera sin modificaciones hacia la tercera API downstream, y retorna el listado de vehículos con código HTTP 200 OK.
* **Scenario 3: Registro Exitoso a través del Gateway con Nombre y DUI Válido**
  * **Given** un nuevo usuario corporativo con Nombre `"Juan Pérez"`, DUI `"01234567-8"`, Email y Password válidos,
  * **When** envía `POST http://localhost:5000/register` con dicho payload,
  * **Then** la solicitud es enrutada downstream al servicio de autenticación y responde con código HTTP 200/201 confirmando el registro.
* **Scenario 4: Rechazo por DUI Inválido o Nombre Faltante en Registro**
  * **Given** un payload de registro con DUI sin guión (e.g. `"012345678"`) o Nombre vacío,
  * **When** envía `POST http://localhost:5000/register`,
  * **Then** el servicio rechaza la petición retornando HTTP 400 Bad Request con el detalle del error de validación.
* **Scenario 5: Autenticación Exitosa en /login a través del Gateway**
  * **Given** credenciales de un usuario registrado,
  * **When** realiza `POST http://localhost:5000/login` a través del puerto del Gateway,
  * **Then** la operación responde satisfactoriamente con código HTTP 200 OK y entrega el token JWT con su fecha de expiración.

## 6. Verification Plan
* Prueba en Postman / cURL:
  1. `curl -i http://localhost:5000/vehiculos` $\to$ Verificar presencia obligatoria de `HTTP/1.1 401 Unauthorized`.
  2. `curl -i -X POST http://localhost:5000/register -H "Content-Type: application/json" -d '{"nombre":"Juan Perez","dui":"01234567-8","email":"admin@auto.com","password":"Password123!"}'` $\to$ Verificar HTTP 200/201.
  3. `curl -i -X POST http://localhost:5000/register -H "Content-Type: application/json" -d '{"nombre":"","dui":"012345678","email":"admin@auto.com","password":"Password123!"}'` $\to$ Verificar HTTP 400 Bad Request.
  4. `curl -i -X POST http://localhost:5000/login -H "Content-Type: application/json" -d '{"email":"admin@auto.com","password":"Password123!"}'` $\to$ Extraer token JWT.
  5. `curl -i -H "Authorization: Bearer <token>" http://localhost:5000/vehiculos` $\to$ Verificar respuesta `HTTP/1.1 200 OK`.
* Comprobación en logs de Ocelot de la transferencia de la cabecera `Authorization` hacia el contenedor de la tercera API `api-vehiculos`.

## 7. Security and Privacy
* Cero persistencia o logueo de credenciales de contraseñas ni tokens JWT completos en los registros planos de Ocelot.
* Ocelot garantiza que la tercera API mantenga el perímetro de seguridad cerrado a usuarios anónimos.

## 8. Risks and Mitigation
* **Risk:** Proxies inversos o balanceadores intermedios eliminando la cabecera `Authorization`. -> **Mitigation:** Configurar explícitamente en el pipeline de Ocelot que las cabeceras HTTP estándar sean preservadas íntegramente hacia los downstreams.

## 9. Deliverables & Config as Code
* Bloque de rutas para `/register`, `/login` y `/vehiculos` en `src/ApiGateway/ocelot.json`.
* DTOs de contrato `RegisterDto`, `LoginDto` y `AuthResponseDto`.
* Suite de pruebas de integración en `tests/ApiGateway.IntegrationTests/VehicleSecurityRoutingTests.cs`.
* Capturas de pantalla para el informe en PDF demostrando el rechazo 401 sin sesión y el éxito 200 con token para la tercera API.

## 10. Definition of Done (DoD)
* [x] La tercera API de Vehículos debidamente enrutada en Ocelot Gateway.
* [x] Contratos de `/register` (con Nombre y DUI `00000000-0`) y `/login` definidos de forma autónoma.
* [x] Endpoints `/register` y `/login` accesibles y funcionales a través del Gateway.
* [x] Respuestas HTTP 401 Unauthorized verificadas al 100% en llamadas anónimas a la tercera API.
* [x] Propagación de tokens Bearer JWT verificada recibiendo HTTP 200 OK con sesión activa.
