# SPEC-2.2.1: Invalidación Automática y Consistencia de Caché ante Mutaciones de Estado

## 1. Objective
Garantizar la consistencia estricta de datos (evitando datos obsoletos o *stale data*) en las consultas de AutoGestion S.A., implementando el desalojo e invalidación automática de la clave de caché de productos en Redis inmediatamente tras la ejecución exitosa de cualquier operación de mutación de estado (`POST`, `PUT`, `DELETE`).

## 2. Scope
### 2.1. Included
* Interceptación y purga de la clave `"listado_productos"` en Redis tras crear un nuevo producto (`POST /api/productos`).
* Purga inmediata de la clave tras modificar un producto existente (`PUT /api/productos/{id}`).
* Purga inmediata de la clave tras eliminar un producto (`DELETE /api/productos/{id}`).
* Verificación de consistencia: la siguiente lectura `GET` debe reflejar obligatoriamente los datos actualizados.

### 2.2. Not Included (Out of Scope)
* Invalidación selectiva o parcheo en memoria de elementos del array JSON (se opta por purga total de la lista para garantizar 100% de consistencia según la Guía 8).
* Caché de otros servicios (Libros, Vehículos).

## 3. Context and Restrictions
* **Context:** Complemento mandatorio de la capa de rendimiento. De nada sirve una caché rápida si devuelve información desfasada a los clientes.
* **Restrictions:**
  * Si la transacción de base de datos falla o es rechazada, la caché NO debe ser invalidada innecesariamente.
  * La invalidación debe ejecutarse con una complejidad algorítmica $O(1)$ mediante el comando `DEL` / `RemoveAsync`.

## 4. Design (Implementation Details)
* **Architecture:**
  `Petición de Mutación (POST/PUT/DELETE)` $\to$ `Validación y Guardado en SQL (DbContext.SaveChangesAsync())` $\to$ `Si éxito: await _cache.RemoveAsync("listado_productos")` $\to$ `Retorna HTTP 200/201/204 al Cliente`.
* **Data Model (Flujo en Controlador / Handler):**
```csharp
[HttpPost]
public async Task<IActionResult> CrearProducto([FromBody] Producto nuevoProducto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    _context.Productos.Add(nuevoProducto);
    await _context.SaveChangesAsync();

    // Invalidación obligatoria e inmediata de la caché
    await _cache.RemoveAsync("listado_productos");

    return CreatedAtAction(nameof(ObtenerPorId), new { id = nuevoProducto.Id }, nuevoProducto);
}

[HttpPut("{id}")]
public async Task<IActionResult> ActualizarProducto(int id, [FromBody] Producto productoActualizado)
{
    if (id != productoActualizado.Id)
        return BadRequest("El ID de la ruta no coincide con el cuerpo.");

    var productoExistente = await _context.Productos.FindAsync(id);
    if (productoExistente == null)
        return NotFound();

    productoExistente.Nombre = productoActualizado.Nombre;
    productoExistente.Precio = productoActualizado.Precio;
    productoExistente.Stock = productoActualizado.Stock;
    productoExistente.Categoria = productoActualizado.Categoria;

    await _context.SaveChangesAsync();

    // Invalidación obligatoria e inmediata de la caché
    await _cache.RemoveAsync("listado_productos");

    return Ok(productoExistente);
}

[HttpDelete("{id}")]
public async Task<IActionResult> EliminarProducto(int id)
{
    var producto = await _context.Productos.FindAsync(id);
    if (producto == null)
        return NotFound();

    _context.Productos.Remove(producto);
    await _context.SaveChangesAsync();

    // Invalidación obligatoria e inmediata de la caché
    await _cache.RemoveAsync("listado_productos");

    return NoContent();
}
```
* **API Contracts:**
  * `POST /api/productos`: Si inserta con éxito en SQL $\to$ Purga caché $\to$ 201 Created.
  * `PUT /api/productos/{id}`: Si actualiza con éxito en SQL $\to$ Purga caché $\to$ 200 OK.
  * `DELETE /api/productos/{id}`: Si elimina con éxito en SQL $\to$ Purga caché $\to$ 204 NoContent.
* **UI/UX:** En la experiencia de usuario final y en las pruebas visuales en Swagger, al agregar o editar un producto y recargar la vista general inmediatamente, el nuevo elemento aparece reflejado sin ningún retardo o discrepancia visual.

## 5. Acceptance Criteria
* **Scenario 1: Invalidación Tras Creación de Producto (POST)**
  * **Given** una lista de 5 productos almacenada en Redis (`listado_productos`),
  * **When** el usuario realiza una petición `POST /api/productos` para crear el producto "Laptop Dell",
  * **Then** el sistema guarda el registro en SQL, ejecuta `RemoveAsync("listado_productos")`, y la subsiguiente petición `GET /api/productos` retorna 6 productos incluyendo "Laptop Dell".
* **Scenario 2: Invalidación Tras Actualización de Datos (PUT)**
  * **Given** el listado en caché reflejando un producto con precio $10.00,
  * **When** se actualiza el precio a $15.00 mediante `PUT /api/productos/1`,
  * **Then** la clave de caché es eliminada inmediatamente de Redis, y el siguiente `GET` devuelve el precio actualizado de $15.00.
* **Scenario 3: Invalidación Tras Eliminación de Producto (DELETE)**
  * **Given** un producto existente presente en la caché,
  * **When** se envía una petición `DELETE /api/productos/1`,
  * **Then** el producto se elimina de la base de datos, la caché se purga, y el siguiente `GET` ya no contiene el producto eliminado.
* **Scenario 4: Contención de Fallo en Mutación Invalida**
  * **Given** un payload de producto inválido enviado a `POST /api/productos`,
  * **When** el sistema rechaza la operación con HTTP 400 (BadRequest),
  * **Then** la caché en Redis NO es eliminada, preservando la información íntegra.

## 6. Verification Plan
* Secuencia de prueba en Postman:
  1. `GET /productos` $\to$ Verifica almacenamiento en caché.
  2. `POST /productos` $\to$ Crea nuevo registro.
  3. Comprobación en `redis-cli`: ejecutar `EXISTS listado_productos` $\to$ Debe responder `(integer) 0`.
  4. `GET /productos` $\to$ Constata que el nuevo ítem aparece y la caché vuelve a poblarse.

## 7. Security and Privacy
* Consistencia transaccional: previene fraudes o ventas de stock con precios obsoletos debido a demoras de caché.

## 8. Risks and Mitigation
* **Risk:** Inconsistencia si la base de datos guarda el dato pero la llamada `RemoveAsync` a Redis falla por error de red. -> **Mitigation:** Si la llamada a Redis falla, registrar error crítico y reintentar, o usar políticas de resiliencia con Polly.

## 9. Deliverables & Config as Code
* Endpoints `POST`, `PUT` y `DELETE` en `ProductosController.cs` con llamadas explícitas a `_cache.RemoveAsync("listado_productos")`.
* Captura de pantalla de evidencia mostrando la eliminación y recarga de datos para el informe PDF.

## 10. Definition of Done (DoD)
* [ ] Invalidación probada y funcionando en `POST /productos`.
* [ ] Invalidación probada y funcionando en `PUT /productos/{id}`.
* [ ] Invalidación probada y funcionando en `DELETE /productos/{id}`.
* [ ] Cero discrepancias o datos viejos observados en pruebas dinámicas.
