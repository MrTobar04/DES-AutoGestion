# Runbook Operativo: Swagger UI y Protocolo de Verificación Aislada Previa (SPEC-1.3.1)

**Proyecto:** AutoGestion S.A. — Ecosistema Empresarial de Microservicios  
**Asignatura:** Desarrollo de Software Empresarial (DSE104) — Desafío 2  
**Especificación Técnica:** [`SPEC-1.3.1: Swagger UI y Protocolo de Verificación Aislada de APIs Individuales`](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.3.1-swagger-agregacion-verificacion-aislada.md)  
**Regla Mandatoria Oficial:** *"Se exige probar el funcionamiento individual de cada API antes de acoplarlas detrás del Gateway"*  
**Fecha:** 2026-09-09  

---

## 1. Propósito y Marco de Gobernanza

Este runbook documenta el protocolo técnico para verificar de forma autónoma, aislada y determinista cada uno de los tres (3) microservicios backend de AutoGestion S.A. previo a su conexión con el Gateway Ocelot.

El cumplimiento de este protocolo garantiza:
1. **Detección temprana de fallos:** Confirmar que los endpoints responden con código HTTP 200/201 en sus puertos nativos directos.
2. **Documentación viva e interactiva:** Exposición de esquemas OpenAPI tipados mediante Swagger UI en cada servicio.
3. **Soporte de Autenticación Bearer JWT:** Definición estandarizada del esquema `Bearer` con el botón interactivo **"Authorize"** en Swagger UI.
4. **Validación de Seguridad en Aislamiento:** Certificación de que la API de Vehículos rechaza con **HTTP 401 Unauthorized** las llamadas anónimas y acepta peticiones con cabecera `Authorization: Bearer <token>`.

---

## 2. Topología de Endpoints y Swagger UI

| Microservicio | Puerto Directo | Swagger UI | Esquema OpenAPI | Estado de Seguridad |
| :--- | :---: | :--- | :--- | :--- |
| **API 1: Productos** | **5001** | `http://localhost:5001/swagger` | `/swagger/v1/swagger.json` | Pública (CRUD completo) |
| **API 2: Libros** | **5002** | `http://localhost:5002/swagger` | `/swagger/v1/swagger.json` | Pública (CRUD completo) |
| **API 3: Vehículos** | **5003** | `http://localhost:5003/swagger` | `/swagger/v1/swagger.json` | Protegida (401 sin Bearer / 200 con Bearer) |

---

## 3. Configuración Implementada en Cada Microservicio

En los archivos `Program.cs` de cada microservicio ([ApiProductos](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/AutoGestion/src/ApiProductos/Program.cs), [ApiLibros](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/AutoGestion/src/ApiLibros/Program.cs) y [ApiVehiculos](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/AutoGestion/src/ApiVehiculos/Program.cs)) se configuró:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Individual",
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

---

## 4. Método 1: Verificación Automatizada (Script de Certificación Aislada)

Se proporciona el script [`verify_spec_1_3_1_isolated.ps1`](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/AutoGestion/scripts/verify_spec_1_3_1_isolated.ps1) que comprueba de forma autónoma:
* Disponibilidad del Swagger JSON y Swagger UI en los 3 puertos.
* Presencia del esquema `Bearer` en los documentos OpenAPI.
* Operaciones CRUD completas (`GET`, `POST`, `PUT`, `DELETE`).
* Rechazo 401 en llamadas anónimas a Vehículos y aprobación 200 con token Bearer.

### Ejecución:
```powershell
powershell -ExecutionPolicy Bypass -File .\AutoGestion\scripts\verify_spec_1_3_1_isolated.ps1
```

### Salida Certificada:
```text
======================================================================
INICIANDO PROTOCOLO DE VERIFICACION SPEC-1.3.1 (SWAGGER + AISLAMIENTO)
======================================================================

[PASO 1] Compilando la solución completa...
Compilación exitosa (0 Errores).

======================================================================
MODULO 1: VERIFICACION AISLADA API PRODUCTOS (PUERTO 5001)
======================================================================
Iniciando ApiProductos de forma aislada en puerto 5001...
ApiProductos operativo y escuchando en http://localhost:5001.

[Productos - Swagger] Validando documentación interactiva...
  GET /swagger/v1/swagger.json -> Status: 200 OK
  GET /swagger/ -> Status: 200 OK (Swagger UI Interactivo Activo)
  Esquema Bearer Auth detectado en Swagger: Sí (Botón Authorize habilitado)

[Productos - CRUD Aislado] Probando endpoints de negocio...
  GET /api/productos -> Status: 200 OK
  POST /api/productos -> Status: 201 Created
  GET /api/productos/1 -> Status: 200 OK
  PUT /api/productos/1 -> Status: 200 OK
  DELETE /api/productos/3 -> Status: 200 OK

======================================================================
MODULO 2: VERIFICACION AISLADA API LIBROS (PUERTO 5002)
======================================================================
Iniciando ApiLibros de forma aislada en puerto 5002...
ApiLibros operativo y escuchando en http://localhost:5002.

[Libros - Swagger] Validando documentación interactiva...
  GET /swagger/v1/swagger.json -> Status: 200 OK
  GET /swagger/ -> Status: 200 OK (Swagger UI Interactivo Activo)
  Esquema Bearer Auth detectado en Swagger: Sí (Botón Authorize habilitado)

[Libros - CRUD Aislado] Probando endpoints de negocio...
  GET /api/libros -> Status: 200 OK
  POST /api/libros -> Status: 201 Created
  GET /api/libros/1 -> Status: 200 OK
  PUT /api/libros/1 -> Status: 200 OK
  DELETE /api/libros/3 -> Status: 200 OK

======================================================================
MODULO 3: VERIFICACION AISLADA API VEHICULOS (PUERTO 5003)
======================================================================
Iniciando ApiVehiculos de forma aislada en puerto 5003...
ApiVehiculos operativo y escuchando en http://localhost:5003.

[Vehículos - Swagger] Validando documentación interactiva...
  GET /swagger/v1/swagger.json -> Status: 200 OK
  GET /swagger/ -> Status: 200 OK (Swagger UI Interactivo Activo)
  Esquema Bearer Auth detectado en Swagger: Sí (Botón Authorize habilitado)

[Vehículos - Seguridad Aislada] Comprobando rechazo HTTP 401 ante llamadas sin sesión...
  GET /api/vehiculos (sin sesión) -> Status: 401 Unauthorized (Exitoso: Módulo protegido)

[Vehículos - CRUD Autenticado] Probando operaciones con cabecera Bearer...
  GET /api/vehiculos (con Bearer) -> Status: 200 OK
  POST /api/vehiculos (con Bearer) -> Status: 201 Created
  GET /api/vehiculos/1 (con Bearer) -> Status: 200 OK
  PUT /api/vehiculos/1 (con Bearer) -> Status: 200 OK
  DELETE /api/vehiculos/3 (con Bearer) -> Status: 200 OK

======================================================================
CERTIFICACION EXITOSA: SPEC-1.3.1 VERIFICACION AISLADA Y SWAGGER AL 100%
======================================================================
```

---

## 5. Método 2: Comprobación Interactiva en Navegador (Swagger UI)

### 1. Iniciar los microservicios
```powershell
# En terminal 1:
dotnet run --project .\AutoGestion\src\ApiProductos\ApiProductos.csproj

# En terminal 2:
dotnet run --project .\AutoGestion\src\ApiLibros\ApiLibros.csproj

# En terminal 3:
dotnet run --project .\AutoGestion\src\ApiVehiculos\ApiVehiculos.csproj
```

### 2. Validar Swagger UI en cada URL:
1. **Productos:** Abrir `http://localhost:5001/swagger`
   - Presionar `GET /api/productos` $\to$ `Try it out` $\to$ `Execute`.
   - Verificar respuesta con código `200` y cuerpo JSON de productos.
2. **Libros:** Abrir `http://localhost:5002/swagger`
   - Presionar `GET /api/libros` $\to$ `Try it out` $\to$ `Execute`.
   - Verificar respuesta con código `200` y catálogo de libros.
3. **Vehículos:** Abrir `http://localhost:5003/swagger`
   - **Prueba sin token:** Ejecutar `GET /api/vehiculos` $\to$ Responderá `401 Unauthorized`.
   - **Prueba con token:** Presionar el botón verde **"Authorize"** en la esquina superior derecha, ingresar `Bearer mi-token-de-prueba` y presionar `Authorize`.
   - Volver a ejecutar `GET /api/vehiculos` $\to$ Responderá `200 OK` con el listado de vehículos.

---

## 6. Checklist de Aceptación Pre-Acoplamiento

* [x] **Swagger UI:** Accesible y operativo en `5001/swagger`, `5002/swagger` y `5003/swagger`.
* [x] **Botón Authorize:** Presente y funcional en las 3 interfaces Swagger para inyección de cabecera Bearer JWT.
* [x] **Operaciones CRUD:** Comprobadas directamente en puertos nativos (5001, 5002, 5003) sin intermediación de Gateway.
* [x] **Seguridad Aislada de Vehículos:** Rechazo estricto HTTP 401 sin sesión y respuesta HTTP 200 con Bearer token.
* [x] **Certificación:** Las 3 APIs superaron el 100% de las pruebas previas al acoplamiento con Ocelot Gateway.
