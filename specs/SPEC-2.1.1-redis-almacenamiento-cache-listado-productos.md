# SPEC-2.1.1: Almacenamiento y Recuperación de Caché Distribuido con Redis para Listado de Productos

## 1. Objective
Optimizar radicalmente el rendimiento de las consultas al catálogo de productos de AutoGestion S.A., reduciendo la latencia de respuesta de lectura en más del 80% (de $\approx 120$ ms en disco/SQL a $< 10$ ms en memoria) mediante el uso de Redis como caché distribuido, aplicando un tiempo de vida (TTL) estricto de exactamente 5 minutos (300 segundos) con fallback transparente a la base de datos SQL.

## 2. Scope
### 2.1. Included
* Integración de `Microsoft.Extensions.Caching.StackExchangeRedis` e inyección de `IDistributedCache`.
* Implementación del patrón Cache-Aside en el endpoint `GET /api/productos`.
* Serialización y deserialización binaria/JSON de colecciones de productos.
* Configuración de `DistributedCacheEntryOptions` con `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)`.
* Fallback tolerante a fallos: si Redis no responde, el sistema consulta directamente a SQL Server sin interrumpir al usuario.

### 2.2. Not Included (Out of Scope)
* Invalidación activa de caché ante mutaciones (delegado a [SPEC-2.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-2.2.1-redis-invalidacion-automatica-mutaciones.md)).
* Caché de consultas individuales `GET /api/productos/{id}` (restringido a listado general según requerimiento de la Guía 8).
* Orquestación de red de Redis en Docker (delegado a [SPEC-5.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-5.2.1-docker-compose-orquestacion-api-sql-redis.md)).

## 3. Context and Restrictions
* **Context:** Capa de acceso a datos y aceleración en memoria de la API de Productos. Redis intercepta las consultas masivas de lectura antes de saturar el motor relacional SQL Server.
* **Restrictions:**
  * La clave de caché debe tener un TTL exacto de 5 minutos (`TimeSpan.FromMinutes(5)`).
  * La serialización debe ser determinista, utilizando `System.Text.Json` para máxima velocidad y bajo consumo de memoria heap.

## 4. Design (Implementation Details)
* **Architecture:**
  `Cliente GET /productos` $\to$ `Controlador Productos` $\to$ `¿Existe en Redis (Key: "listado_productos")?`:
  * **Cache Hit (Existe):** Deserializa JSON $\to$ Retorna directamente (Tiempo de respuesta $< 10$ ms).
  * **Cache Miss (No existe o expiró):** Consulta `DbContext.Productos.ToListAsync()` $\to$ Serializa a JSON $\to$ `IDistributedCache.SetStringAsync("listado_productos", json, options)` con TTL de 5 min $\to$ Retorna al cliente.
* **Data Model:**
```csharp
public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string Categoria { get; set; } = string.Empty;
}
```
* **API Contracts (Servicio de Caché):**
```csharp
public interface IProductosService
{
    Task<IEnumerable<Producto>> ObtenerListadoProductosAsync();
}
```
* **Lógica de Implementación Central:**
```csharp
public async Task<IEnumerable<Producto>> ObtenerListadoProductosAsync()
{
    const string cacheKey = "listado_productos";
    var cachedData = await _cache.GetStringAsync(cacheKey);

    if (!string.IsNullOrEmpty(cachedData))
    {
        return JsonSerializer.Deserialize<IEnumerable<Producto>>(cachedData)!;
    }

    var productos = await _context.Productos.AsNoTracking().ToListAsync();
    var cacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    var serializedData = JsonSerializer.Serialize(productos);
    await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);

    return productos;
}
```
* **UI/UX:** En herramientas cliente (Postman o Swagger UI), la cabecera personalizada o la métrica de tiempo de respuesta (Response Time) evidencia el paso de $\approx 100$ ms (primera petición) a $\approx 5$ ms (segunda petición y subsiguientes).

## 5. Acceptance Criteria
* **Scenario 1: Primer Acceso y Poblamiento de Caché (Cache Miss & Store)**
  * **Given** la base de datos SQL con registros de productos y la clave `"listado_productos"` ausente en Redis,
  * **When** el cliente envía una petición `GET /productos`,
  * **Then** la API consulta a SQL Server, almacena la colección en Redis con expiración exacta a los 5 minutos y retorna el listado con código HTTP 200 OK.
* **Scenario 2: Servido Instantáneo desde Caché (Cache Hit)**
  * **Given** el listado almacenado previamente en la memoria de Redis,
  * **When** un cliente envía una segunda petición `GET /productos` 30 segundos después,
  * **Then** la API sirve los datos directamente desde Redis sin ejecutar ninguna consulta SQL, con un tiempo de respuesta significativamente inferior.
* **Scenario 3: Expiración Automática a los 5 Minutos (TTL Expiry)**
  * **Given** un registro en caché almacenado a las 10:00:00 AM,
  * **When** transcurren 5 minutos y 1 segundo (10:05:01 AM) y el cliente solicita `GET /productos`,
  * **Then** Redis ha desalojado la clave por expiración natural, y la API vuelve a consultar la base de datos SQL para repoblar la caché por otros 5 minutos.

## 6. Verification Plan
* Verificación en terminal mediante `redis-cli`:
  * Ejecutar `KEYS *` y comprobar la existencia de `"listado_productos"`.
  * Ejecutar `TTL listado_productos` y verificar un valor entero decreciente entre 300 y 0 segundos.
* Comparativa de tiempos de respuesta en Postman entre la 1ª y 2ª llamada.

## 7. Security and Privacy
* Conexión a Redis protegida mediante contraseña en ambientes compartidos.
* Aislamiento en red interna de Docker: el puerto 6379 no se expone a redes públicas no autenticadas.

## 8. Risks and Mitigation
* **Risk:** Caída inesperada del servicio Redis deteniendo la aplicación. -> **Mitigation:** Envolver la llamada al caché en bloques `try-catch` para degradación agraciada (graceful degradation), consultando a SQL si Redis no está disponible.

## 9. Deliverables & Config as Code
* Inyección de `AddStackExchangeRedisCache` en `src/ApiProductos/Program.cs`.
* Cadena de conexión `"RedisConnection": "redis-server:6379"` en `appsettings.json`.
* Servicio o controlador de Productos con lógica Cache-Aside.

## 10. Definition of Done (DoD)
* [ ] Conexión a Redis configurada y operativa.
* [ ] Listado de productos guardado en caché con TTL de exactamente 5 minutos.
* [ ] Verificación con `redis-cli TTL` demostrando el decremento desde 300 segundos.
* [ ] Fallback resiliente a base de datos implementado en caso de desconexión.
