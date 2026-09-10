# SPEC-3.1.1: Harness de Pruebas Automatizadas con xUnit y Base de Datos en Memoria (InMemory)

## 1. Objective
Configurar e institucionalizar el entorno de pruebas unitarias automatizadas con xUnit y Entity Framework Core `InMemory` para los microservicios de AutoGestion S.A., garantizando la ejecución de pruebas aisladas, deterministas y libres de efectos secundarios sin interactuar con bases de datos físicas de desarrollo o producción, con un tiempo de ejecución total menor a 3 segundos para la suite completa.

## 2. Scope
### 2.1. Included
* Creación y configuración del proyecto `AutoGestion.Tests` basado en xUnit (`net8.0`).
* Instalación y configuración del paquete `Microsoft.EntityFrameworkCore.InMemory`.
* Creación de factoría de contexto `DbContextTestFactory` con nombres de base de datos únicos por prueba (`Guid.NewGuid().ToString()`) para garantizar aislamiento absoluto.
* Configuración de inyección de dependencias mockeadas mediante `Moq` si se requieren servicios satélite.

### 2.2. Not Included (Out of Scope)
* Casos de prueba específicos de reglas de negocio de Personas (delegados a [SPEC-3.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-3.2.1-xunit-validaciones-entidad-personas-dui.md) y [SPEC-3.2.2](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-3.2.2-xunit-casos-prueba-persistencia-inexistente.md)).
* Pruebas de integración de red de Docker (delegado a [SPEC-5.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-5.2.1-docker-compose-orquestacion-api-sql-redis.md)).

## 3. Context and Restrictions
* **Context:** Módulo de aseguramiento de calidad (QA). Se ejecuta localmente por el desarrollador y de forma automática en pipelines CI/CD previos a cualquier empaquetado o despliegue.
* **Restrictions:**
  * **Prohibición Estricta:** Queda terminantemente prohibido utilizar cadenas de conexión a bases de datos relacionales reales (SQL Server) durante la ejecución de las pruebas unitarias.
  * Todas las pruebas deben ejecutarse en memoria de forma idempotente.

## 4. Design (Implementation Details)
* **Architecture:**
  `Test Runner (dotnet test)` $\to$ `xUnit Engine` $\to$ `[Fact] / [Theory]` $\to$ `DbContext InMemory (Scoped Database)` $\to$ `Controlador / Servicio` $\to$ `Asserts Fluent`.
* **Data Model (Patrón de Factoría InMemory):**
```csharp
public static class DbContextTestFactory
{
    public static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```
* **API Contracts (Estructura Base de Clase de Prueba):**
```csharp
public abstract class BaseTestFixture : IDisposable
{
    protected readonly AppDbContext Context;

    protected BaseTestFixture()
    {
        Context = DbContextTestFactory.CreateInMemoryDbContext();
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
```
* **UI/UX:** Salida formateada de xUnit en la consola de comandos de .NET y en el Test Explorer de Visual Studio / VS Code, mostrando ticks verdes en todas las pruebas ejecutadas.

## 5. Acceptance Criteria
* **Scenario 1: Inicialización Aislada de Base de Datos en Memoria**
  * **Given** el proyecto de pruebas configurado con el proveedor InMemory,
  * **When** se ejecutan dos pruebas unitarias en paralelo que agregan entidades con el mismo ID primario,
  * **Then** cada prueba opera en su propio contenedor InMemory aislado (`Guid`), sin colisiones de concurrencia ni excepciones de llave duplicada.
* **Scenario 2: Detección de Fuga de Conexión a Base de Datos Real**
  * **Given** una prueba mal configurada intentando acceder a una cadena `Server=...`,
  * **When** el harness de prueba inicializa el contexto,
  * **Then** la prueba falla inmediatamente notificando la violación de la política de base de datos InMemory.

## 6. Verification Plan
* Ejecución del comando `dotnet test --logger "console;verbosity=detailed"` en la raíz de la solución.
* Inspección del assembly de pruebas para constatar la presencia exclusiva de `Microsoft.EntityFrameworkCore.InMemory`.

## 7. Security and Privacy
* Aislamiento total: Cero riesgo de corrupción de datos de prueba o producción. No se requieren credenciales sensibles en `appsettings.Test.json`.

## 8. Risks and Mitigation
* **Risk:** InMemory no valida algunas restricciones de llaves foráneas o tipos de datos específicos de SQL Server. -> **Mitigation:** Complementar con validaciones de modelo a nivel de ModelState y DataAnnotations estrictas.

## 9. Deliverables & Config as Code
* Archivo de proyecto `tests/AutoGestion.Tests/AutoGestion.Tests.csproj` con paquetes xUnit y EF Core InMemory.
* Clase utilitaria `tests/AutoGestion.Tests/Helpers/DbContextTestFactory.cs`.

## 10. Definition of Done (DoD)
* [x] Proyecto de pruebas compilando con xUnit y EF Core InMemory.
* [x] Factoría generadora de instancias de base de datos independientes implementada.
* [x] Ejecución de `dotnet test` exitosa en menos de 3 segundos.
