# SPEC-3.2.2: Pruebas Unitarias xUnit para Persistencia Exitosa y Gestión de ID Inexistente en Personas

## 1. Objective
Completar la suite de 5 o más pruebas unitarias requeridas por la rúbrica oficial de AutoGestion S.A., certificando mediante xUnit el flujo exitoso de persistencia de entidades `Persona` en la base de datos InMemory (HTTP 200/201) y el control defensivo ante consultas o mutaciones sobre identificadores primarios (ID) inexistentes (HTTP 400/404).

## 2. Scope
### 2.1. Included
* Prueba unitaria para guardado exitoso de una entidad Persona con datos válidos $\to$ Retorna HTTP 201 Created y persiste en InMemory.
* Prueba unitaria para consulta de Persona por ID existente $\to$ Retorna HTTP 200 OK con la entidad esperada.
* Prueba unitaria para consulta de Persona por ID inexistente $\to$ Retorna HTTP 404 NotFound / 400 BadRequest.
* Prueba unitaria para actualización o eliminación de ID inexistente $\to$ Retorna HTTP 404 NotFound / 400 BadRequest.
* Verificación de conteo acumulado de pruebas: garantizar $\ge 5$ pruebas pasando al 100% para nivel Destacado (9-10).

### 2.2. Not Included (Out of Scope)
* Pruebas de integración contra base de datos SQL Server física (restringido por rúbrica).
* Pruebas de controladores de Productos o Libros.

## 3. Context and Restrictions
* **Context:** Pruebas del ciclo de vida CRUD y robustez defensiva del controlador `PersonasController`.
* **Restrictions:**
  * La rúbrica oficial exige taxativamente: *"Existen 5 o más pruebas pasando en su totalidad, cubriendo: DUI inválido, nombre vacío, ID inexistente y un guardado exitoso"*.

## 4. Design (Implementation Details)
* **Architecture:**
  `Test Method [Fact]` $\to$ `Arrange (Seed Datos InMemory)` $\to$ `Act (Llamada al Controlador)` $\to$ `Assert (StatusCode & Db State)`.
* **Data Model (Suite de Pruebas CRUD y Resiliencia):**
```csharp
public class PersonasCrudTests : BaseTestFixture
{
    private readonly PersonasController _controller;

    public PersonasCrudTests()
    {
        _controller = new PersonasController(Context);
    }

    [Fact]
    public async Task CrearPersona_DatosValidos_GuardaExitosamenteYRetornaCreated()
    {
        // Arrange
        var nuevaPersona = new Persona
        {
            Nombre = "Carlos Eduardo Martínez",
            DUI = "01234567-9"
        };

        // Act
        var result = await _controller.CrearPersona(nuevaPersona);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var personaGuardada = Assert.IsType<Persona>(createdResult.Value);
        Assert.True(personaGuardada.Id > 0);
        Assert.Equal("Carlos Eduardo Martínez", personaGuardada.Nombre);

        // Comprobación de persistencia efectiva en el contexto InMemory
        var personaEnDb = await Context.Personas.FindAsync(personaGuardada.Id);
        Assert.NotNull(personaEnDb);
        Assert.Equal("01234567-9", personaEnDb.DUI);
    }

    [Fact]
    public async Task ObtenerPorId_IdInexistente_RetornaNotFoundOBadRequest()
    {
        // Arrange
        int idInexistente = 99999;

        // Act
        var result = await _controller.ObtenerPorId(idInexistente);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task ActualizarPersona_IdInexistente_RetornaNotFoundOBadRequest()
    {
        // Arrange
        var personaParaActualizar = new Persona
        {
            Id = 88888,
            Nombre = "Nombre Fantasma",
            DUI = "00000000-0"
        };

        // Act
        var result = await _controller.ActualizarPersona(88888, personaParaActualizar);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }
}
```
* **API Contracts:**
  * `CrearPersona(validPayload)` $\to$ Retorna `CreatedAtActionResult` (201 Created).
  * `ObtenerPorId(invalidId)` $\to$ Retorna `NotFoundObjectResult` (404) o `BadRequestObjectResult` (400).
  * `ActualizarPersona(invalidId, payload)` $\to$ Retorna `NotFoundObjectResult` (404) o `BadRequestObjectResult` (400).
* **UI/UX:** No aplica directamente a pantallas de usuario; la interfaz es el informe de consola de `dotnet test`.

## 5. Acceptance Criteria
* **Scenario 1: Guardado Exitoso y Verificación en Memoria**
  * **Given** un objeto `Persona` con Nombre `"Carlos Eduardo Martínez"` y DUI `"01234567-9"`,
  * **When** se invoca el método `CrearPersona` en el controlador,
  * **Then** el controlador retorna HTTP 201 Created, asigna un ID autoincremental y la base de datos InMemory registra la nueva tupla con éxito.
* **Scenario 2: Detección y Contención de ID Inexistente**
  * **Given** la base de datos InMemory sin ningún registro con ID `99999`,
  * **When** se invoca `ObtenerPorId(99999)`,
  * **Then** el controlador detecta la ausencia del registro y retorna HTTP 404 (NotFound) o HTTP 400 (BadRequest) informando que el identificador solicitado no existe.
* **Scenario 3: Aprobación Integral de la Suite (Total $\ge 5$ Pruebas)**
  * **Given** la suite consolidada de pruebas con los 5 escenarios (DUI inválido, nombre vacío, ID inexistente consulta, ID inexistente actualización, guardado exitoso),
  * **When** se ejecuta el comando `dotnet test`,
  * **Then** las 5 o más pruebas pasan al 100% (`Passed: >= 5, Failed: 0`).

## 6. Verification Plan
* Ejecución del comando de testing completo: `dotnet test --logger "console;verbosity=normal"`.
* Verificación en terminal de que el conteo de pruebas aprobadas sea como mínimo 5.

## 7. Security and Privacy
* Manejo seguro de excepciones: los controladores no deben lanzar NullReferenceException sin controlar ante IDs inexistentes.

## 8. Risks and Mitigation
* **Risk:** Estados compartidos entre tests causando falsos negativos en comprobación de IDs. -> **Mitigation:** Uso de bases de datos InMemory independientes y eliminación limpia al concluir cada test con `Dispose()`.

## 9. Deliverables & Config as Code
* Suite de pruebas `tests/AutoGestion.Tests/UnitTests/PersonasCrudTests.cs`.
* Captura de pantalla de la terminal con las 5 pruebas pasando para el informe técnico en PDF.

## 10. Definition of Done (DoD)
* [x] Caso de prueba de guardado exitoso pasando al 100%.
* [x] Caso de prueba de ID inexistente pasando al 100%.
* [x] Total de pruebas unitarias en la solución $\ge 5$ pruebas passing.
* [x] Cero fallos (`Failed: 0`) en la suite de pruebas automatizadas.
