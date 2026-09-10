# SPEC-1.3.1: Swagger UI y Protocolo de Verificación Aislada de APIs Individuales

## 1. Objective
Establecer el protocolo formal de verificación unitaria y de integración para cada microservicio independiente (Productos, Libros y Vehículos) previo a su integración detrás del Gateway Ocelot, proveyendo documentación interactiva mediante Swagger/OpenAPI en cada servicio y una interfaz consolidada para pruebas de humo (smoke tests) que garanticen una tasa de éxito del 100% en pruebas pre-acoplamiento.

## 2. Scope
### 2.1. Included
* Configuración de Swagger/OpenAPI (`Swashbuckle.AspNetCore`) en cada microservicio independiente (Productos, Libros, Vehículos).
* Protocolo formal de pruebas aisladas verificando cada endpoint directamente en su puerto antes de conectar Ocelot.
* Interfaz gráfica Swagger UI habilitada para validación manual y soporte de cabeceras de autorización Bearer JWT.
* Generación de evidencias fotográficas para el cumplimiento de la condición técnica previa exigida en la evaluación.

### 2.2. Not Included (Out of Scope)
* Configuración del router Ocelot (delegado a [SPEC-1.1.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.1-ocelot-enrutamiento-productos-libros.md)).
* Rate limiting perimetral (delegado a [SPEC-1.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.2.1-ocelot-rate-limiting-control-trafico.md)).

## 3. Context and Restrictions
* **Context:** Etapa de aseguramiento y pruebas previas al despliegue del API Gateway. Según la nota técnica mandatoria: *"Se exige probar el funcionamiento individual de cada API antes de acoplarlas detrás del Gateway"*.
* **Restrictions:**
  * Ninguna API puede ser acoplada al Gateway sin haber superado la suite de pruebas aisladas con respuesta HTTP 200/201 en sus operaciones CRUD.
  * Swagger UI debe permanecer activo en entornos de desarrollo y contener esquemas tipados de datos.

## 4. Design (Implementation Details)
* **Architecture:**
  `Desarrollador / Tester` $\to$ `Navegador / Swagger UI (Puerto Directo: ej. 5001, 5002, 5003)` $\to$ `Validación Aislada de Endpoints` $\to$ `Certificación de Funcionamiento` $\to$ `Habilitación en Ocelot Gateway`.
* **Data Model (Configuración Swagger en `Program.cs`):**
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "AutoGestion API Individual", 
        Version = "v1",
        Description = "Microservicio para pruebas previas al acoplamiento con Gateway Ocelot"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese 'Bearer' [espacio] y luego su token JWT en el campo."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
```
* **API Contracts (Matriz de Verificación Aislada):**
  * `API Productos (Puerto 5001)`:
    * `GET /api/productos` $\to$ 200 OK con array JSON.
    * `POST /api/productos` $\to$ 201 Created con entidad guardada.
  * `API Libros (Puerto 5002)`:
    * `GET /api/libros` $\to$ 200 OK con catálogo de libros.
    * `POST /api/libros` $\to$ 201 Created.
  * `API Vehículos (Puerto 5003)`:
    * `GET /api/vehiculos` $\to$ 401 Unauthorized (sin token) y 200 OK (con token).
* **UI/UX:**
  * Interfaz web Swagger UI accesible en `http://localhost:[puerto]/swagger`.
  * Visualización por etiquetas (Tags) de cada controlador con botones interactivos "Try it out" y botón verde "Authorize" para autenticación Bearer.

## 5. Acceptance Criteria
* **Scenario 1: Verificación Exitosa de API de Productos en Aislamiento**
  * **Given** la API de Productos corriendo individualmente en el puerto 5001 sin Gateway,
  * **When** el auditor accede a `http://localhost:5001/swagger` y ejecuta `GET /api/productos`,
  * **Then** la API responde con HTTP 200 OK y el listado de productos en formato JSON de forma inmediata.
* **Scenario 2: Verificación Exitosa de API de Libros en Aislamiento**
  * **Given** la API de Libros ejecutándose en el puerto 5002 sin Gateway,
  * **When** se ejecuta una petición `POST /api/libros` con un payload de libro válido,
  * **Then** el servicio almacena el libro y responde con código HTTP 201 Created.
* **Scenario 3: Certificación de Fallo Previo al Acoplamiento**
  * **Given** una falla de conexión en la base de datos de una API individual durante la prueba de aislamiento,
  * **When** se detecta un error HTTP 500 en Swagger UI,
  * **Then** el protocolo bloquea la integración de dicha API al archivo `ocelot.json` hasta que la causa raíz sea solventada y verificada.

## 6. Verification Plan
* Ejecución secuencial de pruebas de humo mediante Swagger UI en cada microservicio:
  1. Productos: `GET http://localhost:5001/api/productos`
  2. Libros: `GET http://localhost:5002/api/libros`
  3. Vehículos: `GET http://localhost:5003/api/vehiculos` (constatando rechazo 401)
* Registro de checklist de verificación aislada firmado en el repositorio.

## 7. Security and Privacy
* En entornos de producción, Swagger UI debe condicionarse a entornos de desarrollo (`if (app.Environment.IsDevelopment())`) para evitar exposición del mapa de ataque.

## 8. Risks and Mitigation
* **Risk:** Acoplar servicios rotos al Gateway dificultando el diagnóstico de errores. -> **Mitigation:** Protocolo estricto de pruebas aisladas documentado con capturas de pantalla previo a la edición de `ocelot.json`.

## 9. Deliverables & Config as Code
* Configuración Swagger en `Program.cs` de cada proyecto (`ApiProductos`, `ApiLibros`, `ApiVehiculos`).
* Evidencia en capturas de pantalla individuales de cada API respondiendo exitosamente.

## 10. Definition of Done (DoD)
* [x] Swagger UI operativo en los 3 microservicios individuales.
* [x] Pruebas individuales en Productos, Libros y Vehículos ejecutadas y aprobadas al 100%.
* [x] Pruebas confirmadas antes de habilitar el enrutamiento en Ocelot Gateway.
