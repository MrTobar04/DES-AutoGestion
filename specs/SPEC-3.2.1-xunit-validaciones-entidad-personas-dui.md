# SPEC-3.2.1: Pruebas Unitarias xUnit para Validación de Entidad Personas (Formato DUI y Obligatoriedad de Nombre)

## 1. Objective
Asegurar la integridad estructural de la entidad de dominio `Persona` de AutoGestion S.A. mediante pruebas unitarias rigurosas con xUnit, garantizando que el sistema rechace con código HTTP 400 (BadRequest) cualquier registro con formato de DUI que no cumpla estrictamente con el patrón regex `00000000-0` o con el campo Nombre omitido o en blanco.

## 2. Scope
### 2.1. Included
* Modelado formal de la entidad `Persona` con anotaciones de validación (`DataAnnotations`) y/o `IValidatableObject`.
* Prueba unitaria para DUI con formato numérico sin guion (ej. `123456789`) $\to$ Esperado: `BadRequest`.
* Prueba unitaria para DUI con longitud menor o mayor (ej. `123-4`, `000000000-0`) $\to$ Esperado: `BadRequest`.
* Prueba unitaria para DUI con letras o caracteres especiales inválidos $\to$ Esperado: `BadRequest`.
* Prueba unitaria para campo Nombre nulo (`null`) o vacío (`""`) $\to$ Esperado: `BadRequest`.
* Verificación de los mensajes de error devueltos en la respuesta de validación.

### 2.2. Not Included (Out of Scope)
* Pruebas para ID inexistente y guardado exitoso (delegadas a [SPEC-3.2.2](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-3.2.2-xunit-casos-prueba-persistencia-inexistente.md)).
* Configuración del harness InMemory (cubierto en [SPEC-3.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-3.1.1-xunit-entorno-inmemory-aislamiento.md)).

## 3. Context and Restrictions
* **Context:** Capa de lógica de negocio y validación de entrada en el controlador `PersonasController`. Previene el almacenamiento de identidades salvadoreñas ilegítimas o anónimas.
* **Restrictions:**
  * El formato de DUI debe ser obligatoriamente: 8 dígitos enteros, un guion medio y 1 dígito verificador: `^\d{8}-\d$`.
  * El campo Nombre es estrictamente obligatorio (no nulo, no cadenas de solo espacios en blanco).

## 4. Design (Implementation Details)
* **Architecture:**
  `Objeto Persona Inválido` $\to$ `Simulación de ModelState / Validación` $\to$ `PersonasController.CrearPersona(persona)` $\to$ `Assert.IsType<BadRequestObjectResult>(resultado)`.
* **Data Model (Entidad `Persona.cs`):**
```csharp
public class Persona
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre del registro es de carácter obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe contener al menos 2 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DUI es obligatorio.")]
    [RegularExpression(@"^\d{8}-\d$", ErrorMessage = "El formato del DUI debe ser estrictamente 00000000-0.")]
    public string DUI { get; set; } = string.Empty;
}
```
* **API Contracts (Pruebas Unitarias xUnit):**
```csharp
public class PersonasValidationTests : BaseTestFixture
{
    private readonly PersonasController _controller;

    public PersonasValidationTests()
    {
        _controller = new PersonasController(Context);
    }

    [Theory]
    [InlineData("12345678")]      // Sin guion y falta 1 dígito
    [InlineData("123456789")]     // 9 dígitos sin guion
    [InlineData("00000000-A")]    // Caracter alfabético en verificador
    [InlineData("ABCDEFGH-I")]    // Todo texto
    [InlineData("1234567-8")]     // 7 dígitos antes del guion
    [InlineData("12345678--9")]   // Doble guion
    public async Task CrearPersona_DuiInvalido_RetornaBadRequest(string duiInvalido)
    {
        // Arrange
        var persona = new Persona { Nombre = "Juan Pérez", DUI = duiInvalido };
        _controller.ModelState.AddModelError("DUI", "El formato del DUI debe ser estrictamente 00000000-0.");

        // Act
        var result = await _controller.CrearPersona(persona);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CrearPersona_NombreVacioONulo_RetornaBadRequest(string nombreInvalido)
    {
        // Arrange
        var persona = new Persona { Nombre = nombreInvalido, DUI = "02345678-9" };
        _controller.ModelState.AddModelError("Nombre", "El nombre del registro es de carácter obligatorio.");

        // Act
        var result = await _controller.CrearPersona(persona);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }
}
```
* **UI/UX:** Mensajes de error descriptivos serializados en formato estándar `ProblemDetails` o `SerializableError` para feedback inmediato al consumidor de la API.

## 5. Acceptance Criteria
* **Scenario 1: Rechazo de DUI con Formato Inválido**
  * **Given** una solicitud de creación de Persona con el DUI `"12345"` o `"12345678-X"`,
  * **When** el controlador procesa la petición de guardado,
  * **Then** la validación rechaza la operación retornando código HTTP 400 (BadRequest) con el mensaje descriptivo del formato exigido.
* **Scenario 2: Rechazo de Registro con Nombre Omitido**
  * **Given** una solicitud de creación de Persona con `Nombre = ""` y un DUI válido `"01234567-8"`,
  * **When** el controlador ejecuta la validación del modelo,
  * **Then** el controlador retorna HTTP 400 (BadRequest) indicando que el nombre es obligatorio, sin registrar nada en la base de datos InMemory.

## 6. Verification Plan
* Ejecución de `dotnet test --filter FullyQualifiedName~PersonasValidationTests`.
* Comprobación de que todos los casos de prueba de `Theory` pasen en verde (100% exitosas).

## 7. Security and Privacy
* Prevención de inyección de caracteres maliciosos mediante expresiones regulares canónicas estrictas.

## 8. Risks and Mitigation
* **Risk:** Validación omitida si el controlador no consulta `ModelState.IsValid` antes de guardar. -> **Mitigation:** Implementar filtro global `[ApiController]` que automatice el rechazo 400 automático ante inconsistencias de modelo.

## 9. Deliverables & Config as Code
* Modelo `src/ApiPersonas/Models/Persona.cs` con DataAnnotations.
* Suite de pruebas `tests/AutoGestion.Tests/UnitTests/PersonasValidationTests.cs`.

## 10. Definition of Done (DoD)
* [ ] Validación de expresión regular `^\d{8}-\d$` probada con múltiples casos de borde.
* [ ] Validación de campo Nombre obligatorio probada con nulos y espacios en blanco.
* [ ] Respuestas HTTP 400 (BadRequest) verificadas en todas las pruebas de validación.
