# SPEC-2.2.1: Invalidación Automática y Consistencia de Caché ante Mutaciones de Estado en Todas las APIs

## 1. Objective
Garantizar la consistencia estricta de datos (evitando datos obsoletos o *stale data*) en las consultas de AutoGestion S.A. (`/api/productos`, `/api/libros`, `/api/vehiculos`), implementando el desalojo e invalidación automática de la clave de caché correspondiente en Redis inmediatamente tras la ejecución exitosa de cualquier operación de mutación de estado (`POST`, `PUT`, `DELETE`).

## 2. Scope
### 2.1. Included
* Interceptación y purga de la clave `"listado_productos"` en `ApiProductos` tras crear (`POST`), actualizar (`PUT`) o eliminar (`DELETE`) un producto.
* Interceptación y purga de la clave `"listado_libros"` en `ApiLibros` tras crear (`POST`), actualizar (`PUT`) o eliminar (`DELETE`) un libro.
* Interceptación y purga de la clave `"listado_vehiculos"` en `ApiVehiculos` tras crear (`POST`), actualizar (`PUT`) o eliminar (`DELETE`) un vehículo.
* Verificación de consistencia: la siguiente lectura `GET` de cualquier catálogo debe reflejar obligatoriamente los datos actualizados.

### 2.2. Not Included (Out of Scope)
* Invalidación parcial o parcheo en memoria de elementos individuales (se opta por purga total de la clave del catálogo para garantizar 100% de consistencia según la Guía 8).

## 3. Context and Restrictions
* **Context:** Complemento mandatorio de la capa de rendimiento para todas las APIs de AutoGestion. De nada sirve una caché rápida si devuelve información desfasada a los clientes.
* **Restrictions:**
  * Si la transacción o la operación en la fuente de datos falla o es rechazada, la caché NO debe ser invalidada innecesariamente.
  * La invalidación debe ejecutarse con una complejidad algorítmica $O(1)$ mediante el comando `DEL` / `RemoveAsync`.

## 4. Design (Implementation Details)
* **Architecture:**
  `Petición de Mutación (POST/PUT/DELETE)` $\to$ `Validación y Guardado en Persistencia` $\to$ `Si éxito: await _cache.RemoveAsync("listado_{recurso}")` $\to$ `Retorna HTTP 200/201/204 al Cliente`.
* **Claves Invalidadas por Microservicio:**
  * **`ApiProductos`**: `await _cache.RemoveAsync("listado_productos")`
  * **`ApiLibros`**: `await _cache.RemoveAsync("listado_libros")`
  * **`ApiVehiculos`**: `await _cache.RemoveAsync("listado_vehiculos")`
* **Lógica de Implementación (Ejemplo genérico):**
```csharp
app.MapPost("/api/libros", async (Libro nuevoLibro, IDistributedCache cache) =>
{
    // Persistir nuevo libro...
    
    try
    {
        // Invalidación obligatoria e inmediata de la caché de libros
        await cache.RemoveAsync("listado_libros");
    }
    catch (Exception ex)
    {
        // Log resilient fallback
    }

    return Results.Created($"/api/libros/{nuevoLibro.Id}", nuevoLibro);
});
```

## 5. Acceptance Criteria
* **Scenario 1: Invalidación Tras Creación (POST)**
  * **Given** un catálogo almacenado en Redis (`"listado_productos"`, `"listado_libros"`, `"listado_vehiculos"`),
  * **When** el usuario realiza una petición `POST` para crear un nuevo registro en cualquiera de las 3 APIs,
  * **Then** el sistema guarda el registro, ejecuta `RemoveAsync(...)` para su respectiva clave, y la subsiguiente petición `GET` retorna el listado actualizado.
* **Scenario 2: Invalidación Tras Actualización (PUT)**
  * **Given** el catálogo en caché de cualquiera de las 3 APIs,
  * **When** se actualiza un registro mediante `PUT`,
  * **Then** la clave de caché respectiva es eliminada inmediatamente de Redis, y el siguiente `GET` devuelve los datos actualizados.
* **Scenario 3: Invalidación Tras Eliminación (DELETE)**
  * **Given** un registro existente en la caché de cualquiera de las 3 APIs,
  * **When** se envía una petición `DELETE`,
  * **Then** el registro se elimina, la caché correspondiente se purga, y el siguiente `GET` ya no contiene el registro eliminado.

## 6. Verification Plan
* Secuencia de prueba en Postman o Swagger UI para cada API:
  1. `GET /api/{recurso}` $\to$ Verifica almacenamiento en caché.
  2. `POST /api/{recurso}` $\to$ Crea nuevo registro.
  3. Comprobación en `redis-cli`: ejecutar `EXISTS listado_{recurso}` $\to$ Debe responder `(integer) 0`.
  4. `GET /api/{recurso}` $\to$ Constata que el nuevo ítem aparece y la caché vuelve a poblarse.

## 7. Security and Privacy
* Consistencia transaccional: previene fraudes o inconsistencias de datos debido a demoras de caché.

## 8. Risks and Mitigation
* **Risk:** Inconsistencia si la base de datos guarda el dato pero la llamada `RemoveAsync` a Redis falla por error de red. -> **Mitigation:** Registrar la advertencia/error de Redis y garantizar que el cliente reciba la respuesta de éxito de la mutación.

## 9. Deliverables & Config as Code
* Endpoints `POST`, `PUT` y `DELETE` en `ApiProductos`, `ApiLibros` y `ApiVehiculos` con llamadas explícitas a `_cache.RemoveAsync(...)`.

## 10. Definition of Done (DoD)
* [x] Invalidación probada y funcionando en `POST`, `PUT` y `DELETE` para `ApiProductos`.
* [x] Invalidación probada y funcionando en `POST`, `PUT` y `DELETE` para `ApiLibros`.
* [x] Invalidación probada y funcionando en `POST`, `PUT` y `DELETE` para `ApiVehiculos`.
* [x] Cero discrepancias o datos obsoletos observados en las 3 APIs.
