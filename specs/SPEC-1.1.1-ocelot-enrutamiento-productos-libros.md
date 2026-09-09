# SPEC-1.1.1: Ocelot Enrutamiento Dinámico Inverso para las Tres APIs (Productos, Libros y Vehículos)

## 1. Objective
Centralizar el acceso a los tres (3) microservicios backend independientes de AutoGestion S.A. (**API 1: Productos**, **API 2: Libros** y **API 3: Vehículos**) mediante un API Gateway unificado basado en Ocelot. El sistema resuelve la saturación, sobrecarga y dispersión de peticiones independientes, garantizando un reenvío transparente con latencia agregada menor a 15 ms por solicitud en condiciones nominales, y cumpliendo con la rúbrica oficial que exige enrutar el 100% de los endpoints del ecosistema.

## 2. Scope
### 2.1. Included
* Configuración declarativa en `ocelot.json` de rutas upstream y downstream para las tres (3) APIs de la empresa:
  * **API 1:** `/productos` y `/productos/{everything}` $\to$ Microservicio de Productos.
  * **API 2:** `/libros` y `/libros/{everything}` $\to$ Microservicio de Libros.
  * **API 3:** `/vehiculos` y `/vehiculos/{everything}` $\to$ Microservicio de Vehículos.
* Soporte integral para todos los verbos HTTP estándar (`GET`, `POST`, `PUT`, `DELETE`).
* Reescritura dinámica de paths (`DownstreamPathTemplate` $\leftrightarrow$ `UpstreamPathTemplate`).
* Mapeo estático y resolución de nombres hacia los tres servicios downstream (`api-productos`, `api-libros`, `api-vehiculos`).
* Protocolo de verificación obligatoria del funcionamiento autónomo e individual de las tres APIs previo al acoplamiento definitivo en el Gateway.

### 2.2. Not Included (Out of Scope)
* Configuración de Rate Limiting perimetral a 10 req/min (delegado a [SPEC-1.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)).
* Lógica criptográfica de autenticación, generación de tokens JWT y filtro `[Authorize]` en la API de Vehículos (delegado a [SPEC-4.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.1.1-identity-gestion-usuarios-registro-login.md) y [SPEC-4.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.2.1-identity-autorizacion-filtro-vehiculos.md)).
* Propagación especializada de cabeceras de autorización Bearer en Gateway para la API 3 (detallado en [SPEC-1.1.2](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.2-ocelot-enrutamiento-seguridad-vehiculos.md)).
* Almacenamiento en caché de datos en memoria (delegado a [SPEC-2.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-2.1.1-redis-almacenamiento-cache-listado-productos.md)).

## 3. Context and Restrictions
* **Context:** Capa perimetral de red en la arquitectura de microservicios de AutoGestion S.A. Según el enunciado del problema, la empresa cuenta con 3 APIs separadas que sufrían saturación por consultas dispersas. Ocelot actúa como el punto único de contacto (puerto 5000), abstrayendo la ubicación de las 3 APIs internas.
* **Restrictions:**
  * **Regla Mandatoria:** Se exige probar el funcionamiento individual de las 3 APIs (Productos, Libros y Vehículos) antes de acoplarlas detrás del Gateway.
  * El Gateway debe enrutar TODOS los endpoints sin excepción para alcanzar el nivel Destacado (100%) de la rúbrica oficial.
  * Ninguna de las 3 APIs internas debe quedar expuesta directamente al tráfico exterior en producción.

## 4. Design (Implementation Details)
* **Architecture:**
```text
                          [ Cliente / Navegador / Postman ]
                                         │
                                         ▼ (Puerto Único 5000)
                             [ Ocelot API Gateway ]
                                         │
            ┌────────────────────────────┼────────────────────────────┐
            ▼ (GET/POST/PUT/DELETE)       ▼ (GET/POST/PUT/DELETE)       ▼ (GET/POST/PUT/DELETE)
     /productos                   /libros                       /vehiculos
            │                            │                             │
            ▼                            ▼                             ▼
   [ API 1: Productos ]         [ API 2: Libros ]            [ API 3: Vehículos ]
    Host: api-productos          Host: api-libros             Host: api-vehiculos
    Puerto Interno: 80           Puerto Interno: 80           Puerto Interno: 80
```
* **Data Model (Configuración Completa `ocelot.json`):**
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/productos/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-productos",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/productos/{everything}",
      "UpstreamHttpMethod": [ "Get", "Post", "Put", "Delete" ]
    },
    {
      "DownstreamPathTemplate": "/api/productos",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-productos",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/productos",
      "UpstreamHttpMethod": [ "Get", "Post" ]
    },
    {
      "DownstreamPathTemplate": "/api/libros/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-libros",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/libros/{everything}",
      "UpstreamHttpMethod": [ "Get", "Post", "Put", "Delete" ]
    },
    {
      "DownstreamPathTemplate": "/api/libros",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "api-libros",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/libros",
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
      "UpstreamHttpMethod": [ "Get", "Post", "Put", "Delete" ]
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
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5000"
  }
}
```
* **API Contracts (Enrutamiento hacia las 3 APIs):**
  * **API 1 (Productos):**
    * `GET /productos` $\to$ Reenvía a downstream `GET /api/productos` $\to$ 200 OK (Colección JSON).
    * `POST /productos` $\to$ Reenvía payload a downstream `POST /api/productos` $\to$ 201 Created.
  * **API 2 (Libros):**
    * `GET /libros` $\to$ Reenvía a downstream `GET /api/libros` $\to$ 200 OK (Catálogo de Libros).
    * `POST /libros` $\to$ Reenvía payload a downstream `POST /api/libros` $\to$ 201 Created.
  * **API 3 (Vehículos):**
    * `GET /vehiculos` $\to$ Reenvía a downstream `GET /api/vehiculos` $\to$ 200 OK (si autenticado) o 401 Unauthorized (si anónimo).
    * `POST /vehiculos` $\to$ Reenvía a downstream `POST /api/vehiculos` $\to$ 201 Created (si autenticado) o 401 Unauthorized (si anónimo).
* **UI/UX:** Los evaluadores y desarrolladores consumen una única dirección base (`http://localhost:5000`), disponiendo de Swagger UI para probar los endpoints de las 3 APIs (especificado en [SPEC-1.3.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.3.1-swagger-agregacion-verificacion-aislada.md)).

## 5. Acceptance Criteria
* **Scenario 1: Enrutamiento Exitoso a API 1 (Productos)**
  * **Given** el microservicio de Productos ejecutándose y Ocelot activo en el puerto 5000,
  * **When** el cliente envía `GET http://localhost:5000/productos`,
  * **Then** Ocelot reenvía la petición al downstream de Productos y responde con código HTTP 200 OK y la lista de productos.
* **Scenario 2: Enrutamiento Exitoso a API 2 (Libros)**
  * **Given** el microservicio de Libros ejecutándose y Ocelot activo en el puerto 5000,
  * **When** el cliente envía `GET http://localhost:5000/libros`,
  * **Then** Ocelot reenvía la petición al downstream de Libros y responde con código HTTP 200 OK y el catálogo de libros.
* **Scenario 3: Enrutamiento Exitoso a API 3 (Vehículos)**
  * **Given** el microservicio de Vehículos ejecutándose y Ocelot activo en el puerto 5000,
  * **When** el cliente envía una petición `GET http://localhost:5000/vehiculos`,
  * **Then** Ocelot resuelve la ruta upstream `/vehiculos`, contacta a la API de Vehículos downstream y devuelve la respuesta correspondiente (HTTP 200 o HTTP 401 según corresponda).
* **Scenario 4: Manejo de Ruta No Mapeada en Gateway**
  * **Given** el Gateway operativo sin mapeo para `/clientes-desconocidos`,
  * **When** el cliente envía `GET http://localhost:5000/clientes-desconocidos`,
  * **Then** Ocelot intercepta la petición y retorna HTTP 404 Not Found sin exponer los servicios internos.

## 6. Verification Plan
* Ejecución de pruebas de humo autónomas individuales contra cada una de las 3 APIs en sus puertos nativos (5001, 5002, 5003).
* Verificación de enrutamiento integrado ejecutando llamadas cURL/Postman a través del Gateway (puerto 5000):
  * `curl -i http://localhost:5000/productos`
  * `curl -i http://localhost:5000/libros`
  * `curl -i http://localhost:5000/vehiculos`
* Comprobación en logs de Ocelot de la resolución de los tres destinos downstream.

## 7. Security and Privacy
* Ocultamiento perimetral: las 3 APIs residen en una red privada interna y solo el Gateway Ocelot expone puerto público.
* Validación y normalización de URLs para evitar inyecciones o Directory Traversal en upstream paths.

## 8. Risks and Mitigation
* **Risk:** Falta de disponibilidad de alguna de las 3 APIs al levantar el Gateway. -> **Mitigation:** Realizar pruebas de verificación aislada previas (según [SPEC-1.3.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.3.1-swagger-agregacion-verificacion-aislada.md)) y configurar políticas de retry.

## 9. Deliverables & Config as Code
* Archivo de configuración completo `src/ApiGateway/ocelot.json` con rutas para las 3 APIs.
* Inyección de Ocelot en `src/ApiGateway/Program.cs`.
* Proyecto `src/ApiGateway/ApiGateway.csproj` configurado.

## 10. Definition of Done (DoD)
* [ ] Las tres (3) APIs (Productos, Libros y Vehículos) probadas individualmente antes de acoplar.
* [ ] Archivo `ocelot.json` configurado con rutas completas para las 3 APIs (todos los verbos HTTP).
* [ ] Enrutamiento a `/productos`, `/libros` y `/vehiculos` verificado al 100%.
* [ ] Cumplimiento de la rúbrica oficial: todos los endpoints del sistema enrutados correctamente.
