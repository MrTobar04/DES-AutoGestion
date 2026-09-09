# SPEC-1.1.2: Ocelot Enrutamiento Perimetral, Propagación de Seguridad y Control de Acceso para la Tercera API (Vehículos)

## 1. Objective
Configurar en el API Gateway Ocelot el enrutamiento perimetral especializado, la propagación íntegra de cabeceras de autorización HTTP (`Authorization: Bearer <token>`) y el traslado fidedigno de códigos de estado de seguridad hacia y desde la **tercera API de AutoGestion S.A. (API 3: Vehículos)** y el subsistema de Autenticación (`/register` y `/login`), garantizando que las llamadas anónimas a la tercera API sean rechazadas con HTTP 401 (Unauthorized) y las peticiones autenticadas sean despachadas con latencia agregada menor a 10 ms.

## 2. Scope
### 2.1. Included
* Enrutamiento perimetral de los endpoints públicos de gestión de usuarios `/register` y `/login` a través del Gateway.
* Enrutamiento perimetral protegido hacia la **tercera API (Vehículos)** para todos los métodos HTTP (`GET`, `POST`, `PUT`, `DELETE`).
* Configuración de paso transparente (*pass-through*) de la cabecera `Authorization: Bearer <jwt_token>` hacia el downstream de la tercera API.
* Traslado directo y transparente del código de error HTTP 401 (*Unauthorized*) emitido por la tercera API cuando el cliente no posee sesión activa.
* Soporte para pruebas conjuntas e individuales de la tercera API detrás del Gateway.

### 2.2. Not Included (Out of Scope)
* Generación de tokens JWT y persistencia en base de datos con ASP.NET Core Identity (delegado a [SPEC-4.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.1.1-identity-gestion-usuarios-registro-login.md)).
* Decoración y lógica interna del filtro `[Authorize]` en el controlador `VehiculosController` de la tercera API (delegado a [SPEC-4.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.2.1-identity-autorizacion-filtro-vehiculos.md)).
* Rate Limiting perimetral (delegado a [SPEC-1.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)).
* Enrutamiento básico de las otras dos APIs (Productos y Libros, cubierto en [SPEC-1.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.1-ocelot-enrutamiento-productos-libros.md)).

## 3. Context and Restrictions
* **Context:** Representa el canal de seguridad y gobernanza de acceso del Gateway. A diferencia de las APIs de Productos y Libros (que son públicas), la **tercera API (Vehículos)** contiene información confidencial protegida por requerimiento directo del negocio: *"Solo la gente registrada puede ver los vehículos... devuelva error 401 si no ha iniciado sesión"*.
* **Restrictions:**
  * Ninguna cabecera `Authorization` debe ser filtrada, truncada o suprimida al transitar por el pipeline de Ocelot hacia la tercera API.
  * Si la tercera API de Vehículos responde con HTTP 401 Unauthorized, Ocelot debe trasladar dicho código 401 sin transformarlo en 500 o 404.
  * La tercera API de Vehículos debe ser verificada de manera aislada antes de habilitar su reenvío perimetral.

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
* **Data Model (Fragmento de Configuración en `ocelot.json`):**
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/auth/register",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-seguridad",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/register",
      "UpstreamHttpMethod": [ "Post" ]
    },
    {
      "DownstreamPathTemplate": "/api/auth/login",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-seguridad",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/login",
      "UpstreamHttpMethod": [ "Post" ]
    },
    {
      "DownstreamPathTemplate": "/api/vehiculos",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-vehiculos",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/vehiculos",
      "UpstreamHttpMethod": [ "Get", "Post" ]
    },
    {
      "DownstreamPathTemplate": "/api/vehiculos/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-vehiculos",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/vehiculos/{everything}",
      "UpstreamHttpMethod": [ "Get", "Put", "Delete" ]
    }
  ]
}
```
* **API Contracts:**
  * `POST /register`: Entrada `{ "email": "string", "password": "string" }` $\to$ Retorna HTTP 200/201 con confirmación de registro de usuario.
  * `POST /login`: Entrada `{ "email": "string", "password": "string" }` $\to$ Retorna HTTP 200 OK con `{ "token": "jwt_string", "expiration": "datetime" }`.
  * `GET /vehiculos` (Sin Token): $\to$ La tercera API rechaza con HTTP 401 Unauthorized $\to$ Ocelot retorna HTTP 401 al cliente.
  * `GET /vehiculos` (Con Cabecera `Authorization: Bearer <jwt_valido>`): $\to$ La tercera API valida los claims $\to$ Ocelot retorna HTTP 200 OK con el listado JSON de vehículos.
* **UI/UX:** En la interfaz interactiva (Swagger UI o Postman), el evaluador ejecuta primero `POST /login` a través del Gateway, copia el token resultante, lo asigna en el campo Bearer Token, y posteriormente ejecuta `GET /vehiculos` observando el paso exitoso de 401 Unauthorized a 200 OK.

## 5. Acceptance Criteria
* **Scenario 1: Rechazo Estricto HTTP 401 al Consumir la Tercera API sin Sesión (Rúbrica Oficial)**
  * **Given** el Gateway Ocelot operativo y la tercera API de Vehículos activa en downstream,
  * **When** un cliente envía una petición `GET http://localhost:5000/vehiculos` sin cabecera `Authorization`,
  * **Then** Ocelot reenvía la solicitud a la tercera API, recibe la respuesta de rechazo y entrega al cliente final el código de estado HTTP 401 Unauthorized.
* **Scenario 2: Acceso Exitoso a la Tercera API con Propagación de Token Válido**
  * **Given** un token JWT válido obtenido previamente mediante `POST http://localhost:5000/login`,
  * **When** el cliente envía `GET http://localhost:5000/vehiculos` adjuntando la cabecera `Authorization: Bearer <token_valido>`,
  * **Then** Ocelot propaga íntegramente la cabecera sin modificaciones hacia la tercera API downstream, y retorna el listado de vehículos con código HTTP 200 OK.
* **Scenario 3: Flujo de Registro y Login a través del Gateway**
  * **Given** un nuevo usuario corporativo,
  * **When** realiza `POST /register` seguido de `POST /login` a través del puerto del Gateway,
  * **Then** ambas operaciones responden satisfactoriamente con código HTTP 200 y entregan el token de acceso correspondiente.

## 6. Verification Plan
* Prueba en Postman / cURL:
  1. `curl -i http://localhost:5000/vehiculos` $\to$ Verificar presencia obligatoria de `HTTP/1.1 401 Unauthorized`.
  2. `curl -i -X POST http://localhost:5000/login -H "Content-Type: application/json" -d '{"email":"admin@auto.com","password":"Password123!"}'` $\to$ Extraer token.
  3. `curl -i -H "Authorization: Bearer <token>" http://localhost:5000/vehiculos` $\to$ Verificar respuesta `HTTP/1.1 200 OK`.
* Comprobación en logs de Ocelot de la transferencia de la cabecera `Authorization` hacia el contenedor de la tercera API `api-vehiculos`.

## 7. Security and Privacy
* Cero persistencia o logueo de credenciales de contraseñas ni tokens JWT completos en los registros planos de Ocelot.
* Ocelot garantiza que la tercera API mantenga el perímetro de seguridad cerrado a usuarios anónimos.

## 8. Risks and Mitigation
* **Risk:** Proxies inversos o balanceadores intermedios eliminando la cabecera `Authorization`. -> **Mitigation:** Configurar explícitamente en el pipeline de Ocelot que las cabeceras HTTP estándar sean preservadas íntegramente hacia los downstreams.

## 9. Deliverables & Config as Code
* Bloque de rutas para `/register`, `/login` y `/vehiculos` en `src/ApiGateway/ocelot.json`.
* Suite de pruebas de integración en `tests/ApiGateway.IntegrationTests/VehicleSecurityRoutingTests.cs`.
* Capturas de pantalla para el informe en PDF demostrando el rechazo 401 sin sesión y el éxito 200 con token para la tercera API.

## 10. Definition of Done (DoD)
* [ ] La tercera API de Vehículos debidamente enrutada en Ocelot Gateway.
* [ ] Endpoints `/register` y `/login` accesibles y funcionales a través del Gateway.
* [ ] Respuestas HTTP 401 Unauthorized verificadas al 100% en llamadas anónimas a la tercera API.
* [ ] Propagación de tokens Bearer JWT verificada recibiendo HTTP 200 OK con sesión activa.
