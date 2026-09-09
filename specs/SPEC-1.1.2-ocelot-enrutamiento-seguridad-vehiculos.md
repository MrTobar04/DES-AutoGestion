# SPEC-1.1.2: Ocelot Enrutamiento Perimetral y Propagación de Seguridad para Vehículos y Autenticación

## 1. Objective
Configurar en el API Gateway Ocelot el enrutamiento perimetral para el microservicio de Vehículos y los endpoints de Autenticación (`/register` y `/login`), asegurando la propagación íntegra de cabeceras de autorización HTTP `Bearer Authorization` hacia los servicios protegidos, manteniendo la latencia de intermediación por debajo de 10 ms.

## 2. Scope
### 2.1. Included
* Enrutamiento de peticiones públicas de autenticación `/api/auth/register` y `/api/auth/login` a través del Gateway.
* Enrutamiento del recurso `/vehiculos` downstream hacia el microservicio de Vehículos.
* Configuración de paso transparente (pass-through) de cabeceras HTTP `Authorization: Bearer <token>` hacia downstream.
* Enrutamiento granular para operaciones de consulta (`GET /vehiculos`, `GET /vehiculos/{id}`) y mutación (`POST`, `PUT`, `DELETE`).

### 2.2. Not Included (Out of Scope)
* Generación de tokens JWT y validación del usuario en base de datos (delegado a [SPEC-4.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.1.1-identity-gestion-usuarios-registro-login.md)).
* Control de acceso y filtro `[Authorize]` en el controlador downstream (delegado a [SPEC-4.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.2.1-identity-autorizacion-filtro-vehiculos.md)).
* Limitación de tasa de peticiones (delegado a [SPEC-1.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)).

## 3. Context and Restrictions
* **Context:** Representa el canal perimetral para las operaciones protegidas y de identidad. El cliente consume `/auth/*` y `/vehiculos` exclusivamente a través del puerto del Gateway, el cual debe preservar las cabeceras de seguridad sin alteración.
* **Restrictions:**
  * Ninguna cabecera `Authorization` debe ser filtrada, truncada o modificada durante el paso por el pipeline de Ocelot.
  * Si el microservicio downstream responde con HTTP 401 Unauthorized, el Gateway debe trasladar dicho código de error directamente al cliente final.

## 4. Design (Implementation Details)
* **Architecture:**
  `Cliente` $\to$ `Ocelot (Puerto 5000)` $\to$ `Header Forwarding Pipeline` $\to$ `Microservicio Vehículos (Puerto 5003)` / `Servicio Auth (Puerto 5004)`.
* **Data Model (Fragmento `ocelot.json`):**
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
  * `POST /register`: Entrada `{ "email": "string", "password": "string" }` $\to$ Retorna HTTP 200/201 con confirmación de registro.
  * `POST /login`: Entrada `{ "email": "string", "password": "string" }` $\to$ Retorna HTTP 200 con `{ "token": "jwt_token_string", "expiration": "datetime" }`.
  * `GET /vehiculos`: Requiere cabecera `Authorization: Bearer <jwt_token>` $\to$ Retorna HTTP 200 con listado de vehículos o HTTP 401 si no hay token.
* **UI/UX:** Integración transparente en Postman / Swagger UI. Al recibir el token de `/login`, el usuario lo ingresa en el botón "Authorize" de la interfaz gráfica y todas las llamadas a `/vehiculos` se ejecutan sin fricción.

## 5. Acceptance Criteria
* **Scenario 1: Propagación de Token Válido y Consulta Autorizada**
  * **Given** un token JWT válido generado mediante `/login`,
  * **When** el cliente envía `GET http://localhost:5000/vehiculos` con la cabecera `Authorization: Bearer <token_valido>`,
  * **Then** Ocelot reenvía la cabecera íntegra al microservicio downstream de Vehículos y retorna el listado con código HTTP 200 OK.
* **Scenario 2: Rechazo Perimetral y Traslado de HTTP 401**
  * **Given** una solicitud anónima sin cabecera `Authorization`,
  * **When** el cliente envía `GET http://localhost:5000/vehiculos`,
  * **Then** el downstream rechaza la petición y Ocelot transfiere el código HTTP 401 Unauthorized de forma inmediata al cliente.

## 6. Verification Plan
* Prueba de invocación de `/login` vía Gateway verificando obtención de payload con token.
* Prueba de invocación a `/vehiculos` sin credenciales a través del Gateway validando respuesta HTTP 401.
* Prueba de invocación a `/vehiculos` con token JWT inyectado verificando respuesta HTTP 200.

## 7. Security and Privacy
* Los tokens JWT no son persistidos ni almacenados en logs de Ocelot para evitar filtración de credenciales.
* Ocelot actúa como conducto seguro manteniendo la integridad de las cabeceras criptográficas.

## 8. Risks and Mitigation
* **Risk:** Modificación o eliminación involuntaria de la cabecera `Authorization` en proxies intermediarios. -> **Mitigation:** Configurar explícitamente en el pipeline de Ocelot la preservación de cabeceras estándar sin reglas restrictivas de stripping.

## 9. Deliverables & Config as Code
* Bloque de rutas para `/register`, `/login` y `/vehiculos` dentro de `src/ApiGateway/ocelot.json`.
* Pruebas de integración en `tests/ApiGateway.IntegrationTests/VehicleRoutingTests.cs`.

## 10. Definition of Done (DoD)
* [ ] Rutas de `/register` y `/login` funcionales a través del Gateway.
* [ ] Ruta `/vehiculos` funcional y protegida ante accesos anónimos (retorna 401).
* [ ] Propagación de cabecera `Authorization` verificada al 100% en downstream.
