# SPEC-1.2.1: Gobernanza de Tráfico con Rate Limiting en Ocelot API Gateway

## 1. Objective
Implementar una política estricta de mitigación de saturación y prevención de abusos (Rate Limiting) perimetral en el API Gateway Ocelot, restringiendo el tráfico a un límite máximo cuantitativo de 10 peticiones por minuto por cliente/IP. Toda petición excedente debe ser rechazada inmediatamente en $O(1)$ con el código de estado HTTP 429 (Too Many Requests).

## 2. Scope
### 2.1. Included
* Configuración perimetral de `RateLimitOptions` en `ocelot.json` para todas las rutas upstream.
* Definición de ventana temporal de 1 minuto (`Period: "1m"`, `PeriodTimespan: 60`).
* Configuración de límite estricto de 10 peticiones (`Limit: 10`).
* Personalización de la respuesta de rechazo HTTP 429 con cabecera `Retry-After`.

### 2.2. Not Included (Out of Scope)
* Rate limiting por usuario autenticado mediante claims JWT (se restringe a nivel de IP/Cliente perimetral según Guía 7).
* Caché de respuestas (delegado a [SPEC-2.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-2.1.1-redis-almacenamiento-cache-listado-productos.md)).

## 3. Context and Restrictions
* **Context:** Módulo perimetral de resiliencia del API Gateway. Intercepta el ciclo de vida de la petición antes de que el router downstream intente despacharla a los microservicios backend, ahorrando recursos de red y CPU.
* **Restrictions:**
  * El límite es inquebrantable: exactamente 10 peticiones dentro de un periodo móvil de 60 segundos.
  * La petición número 11 debe retornar indefectiblemente el código HTTP 429.

## 4. Design (Implementation Details)
* **Architecture:**
  `Solicitud Cliente` $\to$ `Ocelot RateLimitingMiddleware` $\to$ `{ Contador <= 10: Pasa a Downstream }` / `{ Contador > 10: Retorna 429 Too Many Requests }`.
* **Data Model (Configuración Declarativa `ocelot.json`):**
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
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [ { "Host": "api-productos", "Port": 80 } ],
      "UpstreamPathTemplate": "/productos",
      "UpstreamHttpMethod": [ "Get", "Post" ],
      "RateLimitOptions": {
        "ClientWhitelist": [],
        "EnableRateLimiting": true,
        "Period": "1m",
        "PeriodTimespan": 60,
        "Limit": 10
      }
    },
    {
      "DownstreamPathTemplate": "/api/libros",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [ { "Host": "api-libros", "Port": 80 } ],
      "UpstreamPathTemplate": "/libros",
      "UpstreamHttpMethod": [ "Get", "Post" ],
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
  * Solicitud nominal (1-10 en 60s): Retorna respuesta normal del microservicio (HTTP 200/201).
  * Solicitud en exceso (>= 11 en 60s):
    * **HTTP Status:** `429 Too Many Requests`.
    * **Response Headers:** `Retry-After: 60`.
    * **Response Body:** `"Se ha excedido el límite máximo de 10 peticiones por minuto para este servicio."`
* **UI/UX:** Cuando se prueba desde herramientas de cliente (Swagger o Postman), la interfaz debe mostrar visualmente el código de estado `429 Too Many Requests` y el mensaje de alerta para informar al usuario de la restricción activa.

## 5. Acceptance Criteria
* **Scenario 1: Tráfico dentro del Umbral Permitido (Happy Path)**
  * **Given** un cliente que no ha enviado peticiones en el último minuto,
  * **When** envía una ráfaga secuencial de 10 peticiones válidas `GET /productos` en 30 segundos,
  * **Then** las 10 peticiones son procesadas y aprobadas exitosamente retornando HTTP 200 OK.
* **Scenario 2: Bloqueo Automático al Superar la Cuota (Rate Limit Exceeded)**
  * **Given** un cliente que ha consumido sus 10 peticiones permitidas en el minuto en curso,
  * **When** envía la petición número 11 en el segundo 45,
  * **Then** Ocelot bloquea la petición perimetralmente, no contacta al downstream de productos y retorna inmediatamente HTTP 429 Too Many Requests con el mensaje de cuota excedida.
* **Scenario 3: Reinicio de la Ventana Temporal**
  * **Given** un cliente bloqueado por exceder el límite,
  * **When** transcurren 61 segundos sin emitir tráfico y envía una nueva petición,
  * **Then** el contador de la ventana se reinicia y la petición es aceptada retornando HTTP 200 OK.

## 6. Verification Plan
* Script de prueba automatizada o colección de Postman (Runner) ejecutando 12 peticiones consecutivas a `http://localhost:5000/productos`.
* Verificación de que las respuestas 1 a 10 entregan código HTTP 200 y las respuestas 11 y 12 entregan código HTTP 429.
* Medición de tiempo de respuesta de la petición rechazada confirmando descarte en menos de 5 ms.

## 7. Security and Privacy
* Protección activa contra ataques volumétricos simples de Denegación de Servicio (DoS) y scraping automatizado no controlado.
* Aislamiento de microservicios: la sobrecarga nunca llega a las APIs internas ni a la base de datos SQL.

## 8. Risks and Mitigation
* **Risk:** Bloqueo de clientes corporativos legítimos que comparten una misma IP pública (NAT). -> **Mitigation:** Configuración opcional de cabecera `ClientIdHeader` o whitelist controlada en configuración para entornos de pruebas.

## 9. Deliverables & Config as Code
* Sección `GlobalConfiguration.RateLimitOptions` y `Routes[].RateLimitOptions` en `src/ApiGateway/ocelot.json`.
* Captura de pantalla de evidencia para el informe final PDF mostrando la respuesta HTTP 429 tras la 11ª petición.

## 10. Definition of Done (DoD)
* [x] Rate Limiting activado y probado en todas las rutas clave de Ocelot.
* [x] Límite estricto de 10 peticiones/minuto verificado empíricamente.
* [x] Petición 11 responde consistentemente con HTTP 429 Too Many Requests.
* [x] Evidencia fotográfica generada para el documento PDF de entrega.
