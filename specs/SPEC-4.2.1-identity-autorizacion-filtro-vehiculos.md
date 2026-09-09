# SPEC-4.2.1: Bloqueo de Acceso y Protección de Endpoints de Vehículos con Atributo [Authorize]

## 1. Objective
Asegurar y privatizar por completo el catálogo y operaciones sobre la entidad `Vehículos` de AutoGestion S.A., implementando el filtro de autorización `[Authorize]` con validación estricta del esquema JWT Bearer, garantizando que todo intento de acceso no autenticado o con token expirado/inválido sea rechazado de inmediato con el código de estado HTTP 401 (Unauthorized).

## 2. Scope
### 2.1. Included
* Aplicación del atributo `[Authorize]` a nivel de clase en `VehiculosController` (protegiendo el 100% de sus endpoints).
* Configuración del middleware de autenticación `app.UseAuthentication()` y `app.UseAuthorization()`.
* Validación criptográfica de la firma del token, emisor (`Issuer`), audiencia (`Audience`) y vigencia temporal.
* Garantía de respuesta HTTP 401 Unauthorized ante llamadas anónimas.
* Habilitación de acceso HTTP 200 OK únicamente cuando la cabecera `Authorization: Bearer <jwt_valido>` esté presente y sea legítima.

### 2.2. Not Included (Out of Scope)
* Generación del token en `/login` (cubierto en [SPEC-4.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.1.1-identity-gestion-usuarios-registro-login.md)).
* Control de acceso basado en roles específicos como Admin/Vendedor (la rúbrica exige autenticación y bloqueo con 401).

## 3. Context and Restrictions
* **Context:** Núcleo de seguridad de recursos de negocio. Resuelve el requerimiento específico del cliente: *"Solo la gente registrada puede ver los vehículos... si no ha iniciado sesión, devuelva error 401"*.
* **Restrictions:**
  * **Cero tolerancia a fugas:** El 100% de las rutas de Vehículos (`GET /vehiculos`, `GET /vehiculos/{id}`, `POST`, `PUT`, `DELETE`) deben requerir autenticación.
  * Todo acceso sin sesión iniciada debe retornar estrictamente el código HTTP 401 Unauthorized (la rúbrica penaliza si algún endpoint queda desprotegido).

## 4. Design (Implementation Details)
* **Architecture:**
  `Petición a /vehiculos` $\to$ `AuthenticationMiddleware` $\to$ `¿Posee cabecera Bearer válida?`:
  * **No posee o inválido:** Intercepta $\to$ Retorna inmediatamente HTTP 401 Unauthorized.
  * **Sí posee y token válido:** Inyecta `ClaimsPrincipal` en `HttpContext.User` $\to$ Ejecuta `VehiculosController` $\to$ Retorna HTTP 200 OK.
* **Data Model (Entidad `Vehiculo.cs` y Controlador Protegido):**
```csharp
public class Vehiculo
{
    public int Id { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Anio { get; set; }
    public decimal Precio { get; set; }
    public string Placa { get; set; } = string.Empty;
}

[Authorize] // Bloqueo total de la clase controladora
[ApiController]
[Route("api/[controller]")]
public class VehiculosController : ControllerBase
{
    private readonly AppDbContext _context;

    public VehiculosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerVehiculos()
    {
        var vehiculos = await _context.Vehiculos.ToListAsync();
        return Ok(vehiculos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var vehiculo = await _context.Vehiculos.FindAsync(id);
        if (vehiculo == null) return NotFound();
        return Ok(vehiculo);
    }

    [HttpPost]
    public async Task<IActionResult> CrearVehiculo([FromBody] Vehiculo vehiculo)
    {
        _context.Vehiculos.Add(vehiculo);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = vehiculo.Id }, vehiculo);
    }
}
```
* **API Contracts:**
  * `GET /api/vehiculos` sin token $\to$ HTTP 401 Unauthorized.
  * `GET /api/vehiculos` con token válido $\to$ HTTP 200 OK con colección JSON de vehículos.
  * `POST /api/vehiculos` sin token $\to$ HTTP 401 Unauthorized.
* **UI/UX:** En Swagger UI, los endpoints de Vehículos se muestran con un ícono de candado cerrado. Al intentar ejecutar sin autorizar, Swagger muestra claramente la respuesta HTTP 401; tras autenticarse mediante el modal Bearer, el candado se cierra y la respuesta cambia a HTTP 200 OK.

## 5. Acceptance Criteria
* **Scenario 1: Bloqueo de Acceso Anónimo a Vehículos (Rúbrica Oficial 100%)**
  * **Given** un usuario anónimo o un navegador sin sesión iniciada,
  * **When** envía una solicitud `GET /api/vehiculos` (o a través del Gateway `/vehiculos`),
  * **Then** el servidor intercepta la llamada y responde inmediatamente con el código de error HTTP 401 Unauthorized.
* **Scenario 2: Acceso Autorizado con Token Válido**
  * **Given** un usuario autenticado que obtuvo un token JWT mediante `/login`,
  * **When** envía la solicitud `GET /api/vehiculos` con la cabecera `Authorization: Bearer <token_valido>`,
  * **Then** el servidor valida el token y devuelve el listado completo de vehículos con código HTTP 200 OK.
* **Scenario 3: Rechazo de Token Expirado o Manipulado**
  * **Given** una solicitud que adjunta un token con firma alterada o expirado,
  * **When** se solicita `GET /api/vehiculos`,
  * **Then** el validador JwtBearer rechaza la firma y devuelve código HTTP 401 Unauthorized.

## 6. Verification Plan
* Prueba en cURL:
  * `curl -i http://localhost:5000/vehiculos` $\to$ Verificar presencia de `HTTP/1.1 401 Unauthorized`.
  * `curl -i -H "Authorization: Bearer <token>" http://localhost:5000/vehiculos` $\to$ Verificar presencia de `HTTP/1.1 200 OK`.
* Comprobación en Swagger UI mostrando el candado y las respuestas 401 vs 200.

## 7. Security and Privacy
* Cero exposición de datos vehiculares a usuarios no registrados.
* Cumplimiento estricto del principio de menor privilegio y Zero-Trust.

## 8. Risks and Mitigation
* **Risk:** Omitir el decorador en algún método individual si se decora acción por acción. -> **Mitigation:** Decorar la clase completa a nivel de controlador (`[Authorize]`) para que cualquier nuevo endpoint añadido herede la protección automáticamente.

## 9. Deliverables & Config as Code
* Controlador protegido `src/ApiVehiculos/Controllers/VehiculosController.cs`.
* Configuración de autenticación en `src/ApiVehiculos/Program.cs`.
* Capturas de pantalla para el PDF de entrega evidenciando el error 401 sin sesión y el éxito 200 con sesión.

## 10. Definition of Done (DoD)
* [ ] Atributo `[Authorize]` colocado a nivel de clase en el controlador de vehículos.
* [ ] El 100% de los endpoints de vehículos devuelven HTTP 401 sin sesión.
* [ ] Acceso exitoso con HTTP 200 verificado tras adjuntar token Bearer.
* [ ] Captura de pantalla de la respuesta 401 y 200 lista para la rúbrica del informe.
