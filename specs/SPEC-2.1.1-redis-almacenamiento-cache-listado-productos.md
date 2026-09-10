# SPEC-2.1.1: Almacenamiento y Recuperación de Caché Distribuido con Redis para Listados del Catálogo (Productos, Libros, Vehículos)

## 1. Objective
Optimizar radicalmente el rendimiento de las consultas a los catálogos principales de AutoGestion S.A. (`/api/productos`, `/api/libros`, `/api/vehiculos`), reduciendo la latencia de respuesta de lectura en más del 80% (de $\approx 120$ ms en disco/SQL a $< 10$ ms en memoria) mediante el uso de Redis como caché distribuido, aplicando un tiempo de vida (TTL) estricto de exactamente 5 minutos (300 segundos) con fallback transparente a la fuente de datos.

## 2. Scope
### 2.1. Included
* Integración de `Microsoft.Extensions.Caching.StackExchangeRedis` e inyección de `IDistributedCache` en todos los microservicios del catálogo (`ApiProductos`, `ApiLibros`, `ApiVehiculos`).
* Implementación del patrón Cache-Aside en los endpoints de lectura general:
  * `GET /api/productos` $\to$ Clave en Redis: `"listado_productos"`
  * `GET /api/libros` $\to$ Clave en Redis: `"listado_libros"`
  * `GET /api/vehiculos` $\to$ Clave en Redis: `"listado_vehiculos"`
* Serialización y deserialización binaria/JSON de colecciones de datos utilizando `System.Text.Json`.
* Configuración de `DistributedCacheEntryOptions` con `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)`.
* Fallback tolerante a fallos: si Redis no responde o experimenta desconexión, cada API consulta directamente la fuente de datos sin interrumpir la experiencia del usuario.

### 2.2. Not Included (Out of Scope)
* Invalidación activa de caché ante mutaciones (delegado a [SPEC-2.2.1](file:///c:/Users/Inspiron/source/repos/DES-AutoGestion/specs/SPEC-2.2.1-redis-invalidacion-automatica-mutaciones.md)).
* Caché de consultas individuales por ID (por ejemplo `GET /api/productos/{id}`) (restringido a listados generales según requerimiento de la Guía 8).
* Orquestación de red de Redis en Docker (delegado a [SPEC-5.2.1](file:///c:/Users/Inspiron/source/repos/DES-AutoGestion/specs/SPEC-5.2.1-docker-compose-orquestacion-api-sql-redis.md)).

## 3. Context and Restrictions
* **Context:** Capa de aceleración en memoria transversal para las tres APIs de AutoGestion S.A. Redis intercepta las consultas masivas de lectura antes de saturar el motor relacional.
* **Restrictions:**
  * Las claves de caché deben tener un TTL exacto de 5 minutos (`TimeSpan.FromMinutes(5)`).
  * La serialización debe ser determinista, utilizando `System.Text.Json` para máxima velocidad y bajo consumo de memoria heap.

## 4. Design (Implementation Details)
* **Architecture:**
  `Cliente GET /api/{recurso}` $\to$ `Controlador / Endpoint` $\to$ `¿Existe en Redis (Key: "listado_{recurso}")?`:
  * **Cache Hit (Existe):** Deserializa JSON $\to$ Retorna directamente (Tiempo de respuesta $< 10$ ms).
  * **Cache Miss (No existe o expiró):** Consulta la base de datos $\to$ Serializa a JSON $\to$ `IDistributedCache.SetStringAsync("listado_{recurso}", json, options)` con TTL de 5 min $\to$ Retorna al cliente.
* **Mapeo de Claves por Servicio:**
  * **`ApiProductos`**: `"listado_productos"`
  * **`ApiLibros`**: `"listado_libros"`
  * **`ApiVehiculos`**: `"listado_vehiculos"`
* **Lógica de Implementación Central (Ejemplo genérico):**
```csharp
app.MapGet("/api/libros", async (IDistributedCache cache) =>
{
    const string cacheKey = "listado_libros";
    try
    {
        var cachedData = await cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cachedItems = JsonSerializer.Deserialize<List<Libro>>(cachedData);
            if (cachedItems is not null) return Results.Ok(cachedItems);
        }
    }
    catch (Exception ex)
    {
        // Resilient fallback log
    }

    var items = ObtenerLibrosDesdePersistencia();

    try
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(items), options);
    }
    catch (Exception ex)
    {
        // Resilient fallback log
    }

    return Results.Ok(items);
});
```

## 5. Acceptance Criteria
* **Scenario 1: Primer Acceso y Poblamiento de Caché (Cache Miss & Store)**
  * **Given** la base de datos con registros y la clave correspondiente (`"listado_productos"`, `"listado_libros"`, `"listado_vehiculos"`) ausente en Redis,
  * **When** el cliente envía una petición `GET` al catálogo de cualquier API,
  * **Then** la API consulta la fuente de datos, almacena la colección en Redis con expiración exacta de 5 minutos y retorna el listado con HTTP 200 OK.
* **Scenario 2: Servido Instantáneo desde Caché (Cache Hit)**
  * **Given** el listado almacenado previamente en la memoria de Redis,
  * **When** un cliente envía una segunda petición `GET` 30 segundos después,
  * **Then** la API sirve los datos directamente desde Redis sin ejecutar ninguna consulta a la base de datos.
* **Scenario 3: Expiración Automática a los 5 Minutos (TTL Expiry)**
  * **Given** un registro en caché almacenado a las 10:00:00 AM,
  * **When** transcurren 5 minutos y 1 segundo (10:05:01 AM) y el cliente solicita `GET`,
  * **Then** Redis ha desalojado la clave por expiración natural y la API repobla la caché por otros 5 minutos.

## 6. Verification Plan
* Verificación en terminal mediante `redis-cli`:
  * Ejecutar `KEYS *` y comprobar la existencia de `"listado_productos"`, `"listado_libros"` y `"listado_vehiculos"`.
  * Ejecutar `TTL listado_<recurso>` y verificar un valor entero decreciente entre 300 y 0 segundos.
* Comparativa de tiempos de respuesta en Postman entre la 1ª y 2ª llamada para cada microservicio.

## 7. Security and Privacy
* Conexión a Redis y credenciales sensibles resguardadas exclusivamente en un archivo `.env` local (`REDIS_CONNECTION_STRING`).
* Exclusión garantizada en control de versiones mediante reglas estrictas en `.gitignore` (`.env` y `*.env` ignorados, plantilla pública `.env.example` provista para desarrollo).
* Aislamiento y protección ante divulgación accidental o push involuntario a repositorios de código.

## 8. Risks and Mitigation
* **Risk:** Caída inesperada del servicio Redis deteniendo la aplicación. -> **Mitigation:** Envolver las llamadas al caché en bloques `try-catch` para degradación agraciada (graceful degradation), consultando a la base de datos si Redis no está disponible.

## 9. Deliverables & Config as Code
* Reglas de ignorado en `.gitignore` y plantilla `.env.example` en la raíz del repositorio.
* Inyección de `AddStackExchangeRedisCache` en `Program.cs` para `ApiProductos`, `ApiLibros` y `ApiVehiculos` leyendo la variable de entorno `REDIS_CONNECTION_STRING` del archivo `.env`.
* Configuración de fallback seguro `"RedisConnection": "redis-cache:6379"` en `appsettings.json` sin contraseñas expuestas.
* Servicios/Endpoints con lógica Cache-Aside implementados en las 3 APIs.

## 10. Definition of Done (DoD)
* [ ] Conexión a Redis configurada y operativa en las 3 APIs.
* [ ] Listados de productos, libros y vehículos guardados en caché con TTL de exactamente 5 minutos.
* [ ] Verificación con `redis-cli TTL` demostrando el decremento desde 300 segundos para todas las claves.
* [ ] Fallback resiliente a la fuente de datos implementado en caso de desconexión.
