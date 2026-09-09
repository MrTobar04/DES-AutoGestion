# SPEC-1.1.1: Ocelot Enrutamiento Dinámico Inverso para APIs de Productos y Libros

## 1. Objective
Centralizar el acceso a los microservicios backend de AutoGestion S.A. mediante un API Gateway basado en Ocelot, unificando la puerta de entrada para los recursos `/productos` y `/libros`. El sistema resuelve la saturación y dispersión de peticiones independientes, garantizando un reenvío transparente con latencia agregada menor a 15 ms por solicitud en condiciones nominales.

## 2. Scope
### 2.1. Included
* Configuración declarativa de rutas upstream y downstream en `ocelot.json` para `/productos` y `/libros`.
* Soporte para todos los verbos HTTP estándar (`GET`, `POST`, `PUT`, `DELETE`).
* Reescritura dinámica de rutas y balanceo estático apuntando a los microservicios correspondientes.
* Verificación previa del funcionamiento autónomo de las APIs antes de su integración al Gateway.

### 2.2. Not Included (Out of Scope)
* Configuración de Rate Limiting (delegado a [SPEC-1.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)).
* Enrutamiento y propagación de seguridad para Vehículos (delegado a [SPEC-1.1.2](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.2-ocelot-enrutamiento-seguridad-vehiculos.md)).
* Almacenamiento en caché de respuestas en memoria (delegado a [SPEC-2.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-2.1.1-redis-almacenamiento-cache-listado-productos.md)).

## 3. Context and Restrictions
* **Context:** Capa de enrutamiento perimetral en la arquitectura de microservicios de AutoGestion S.A. Los clientes externos interactúan únicamente con el puerto del Gateway (ej. `http://localhost:5000`), desacoplándose de las direcciones IP y puertos directos de los servicios internos.
* **Restrictions:**
  * Las APIs internas de Productos y Libros deben ser completamente operativas de forma aislada antes de habilitar el enrutamiento perimetral.
  * No se permite la exposición pública directa de los puertos de las APIs internas una vez desplegado el Gateway.

## 4. Design (Implementation Details)
* **Architecture:**
  `Cliente HTTP` $\to$ `Ocelot API Gateway (Puerto 5000)` $\to$ `Pipeline Ocelot (Route Resolution)` $\to$ `Downstream Service (Productos: Puerto 5001 / Libros: Puerto 5002)`.
* **Data Model (Configuración `ocelot.json`):**
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
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5000"
  }
}
```
* **API Contracts:**
  * `GET /productos` $\to$ Reenvía a downstream `GET /api/productos`, retornando colección JSON con HTTP 200 OK.
  * `GET /productos/{id}` $\to$ Reenvía a downstream `GET /api/productos/{id}`, retornando entidad con HTTP 200 OK o 404 NotFound.
  * `POST /productos` $\to$ Reenvía payload JSON a downstream `POST /api/productos`, retornando HTTP 201 Created.
  * `GET /libros` $\to$ Reenvía a downstream `GET /api/libros`, retornando colección JSON con HTTP 200 OK.
  * `POST /libros` $\to$ Reenvía payload JSON a downstream `POST /api/libros`, retornando HTTP 201 Created.
* **UI/UX:** El Gateway actúa a nivel de transporte HTTP. Los desarrolladores y auditores dispondrán de una interfaz interactiva de Swagger UI unificada para visualización de catálogo (especificada en [SPEC-1.3.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.3.1-swagger-agregacion-verificacion-aislada.md)).

## 5. Acceptance Criteria
* **Scenario 1: Enrutamiento Nominal a Catálogo de Productos**
  * **Given** el microservicio de Productos ejecutándose en downstream y el Gateway operativo en `http://localhost:5000`,
  * **When** el cliente envía una petición `GET http://localhost:5000/productos`,
  * **Then** Ocelot resuelve la ruta upstream `/productos`, reenvía la petición al downstream `/api/productos` y retorna el listado de productos con código HTTP 200 OK.
* **Scenario 2: Enrutamiento Nominal a Catálogo de Libros**
  * **Given** el microservicio de Libros ejecutándose en downstream y el Gateway operativo en `http://localhost:5000`,
  * **When** el cliente envía una petición `GET http://localhost:5000/libros`,
  * **Then** Ocelot resuelve la ruta upstream `/libros`, reenvía la solicitud al downstream `/api/libros` y retorna el listado de libros con código HTTP 200 OK.
* **Scenario 3: Manejo de Ruta No Mapeada en Gateway**
  * **Given** el Gateway operativo sin configuración para la ruta `/servicios-desconocidos`,
  * **When** el cliente envía `GET http://localhost:5000/servicios-desconocidos`,
  * **Then** Ocelot intercepta la petición y responde con código HTTP 404 (Not Found) sin comprometer los servicios backend.

## 6. Verification Plan
* Pruebas de integración de caja negra mediante cURL o Postman invocando los endpoints upstream en el puerto 5000.
* Comprobación de logs de consola de Ocelot confirmando la resolución de `DownstreamPathTemplate`.
* Verificación aislada previa ejecutando llamadas directas a los puertos downstream antes de encender el Gateway.

## 7. Security and Privacy
* Aislamiento de topología de red: los microservicios internos no exponen puertos fuera de la red local o red Docker.
* Normalización de URLs de entrada para prevenir ataques de Directory Traversal en paths upstream.

## 8. Risks and Mitigation
* **Risk:** Falla en la resolución DNS de los hosts downstream cuando se ejecutan en diferentes entornos (localhost vs Docker). -> **Mitigation:** Utilizar variables de configuración o archivos de entorno por ambiente (`ocelot.Development.json`, `ocelot.Docker.json`).

## 9. Deliverables & Config as Code
* Archivo de configuración `src/ApiGateway/ocelot.json`.
* Configuración de inyección en `src/ApiGateway/Program.cs` (`builder.Services.AddOcelot()`, `app.UseOcelot().Wait()`).
* Archivo de configuración de solución `ApiGateway.csproj` con dependencia `Ocelot`.

## 10. Definition of Done (DoD)
* [ ] Archivo `ocelot.json` configurado con rutas completas para `/productos` y `/libros` (todos los verbos).
* [ ] Pruebas autónomas previas ejecutadas con éxito en cada microservicio individual.
* [ ] Pruebas de reenvío validadas al 100% recibiendo HTTP 200/201 en todas las operaciones.
* [ ] Código compilando sin advertencias ni dependencias no autorizadas.
