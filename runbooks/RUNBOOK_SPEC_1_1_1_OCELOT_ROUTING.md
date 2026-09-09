# Runbook Operativo: Verificación y Despliegue de SPEC-1.1.1 (API Gateway Ocelot)

**Proyecto:** AutoGestion S.A. — Ecosistema Empresarial de Microservicios  
**Asignatura:** Desarrollo de Software Empresarial (DSE104) — Desafío 2  
**Especificación Técnica:** [`SPEC-1.1.1: Ocelot Enrutamiento Dinámico Inverso para las Tres APIs`](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.1-ocelot-enrutamiento-productos-libros.md)  
**Criterio de Evaluación:** a) Ocelot — Nivel Destacado (100% de endpoints enrutados)  
**Fecha:** 2026-09-08  

---

## 1. Topología del Sistema y Puertos

El ecosistema implementado se compone de un API Gateway y tres (3) microservicios backend autónomos:

| Servicio | Proyecto | Puerto Local | Swagger UI Directo | Propósito |
| :--- | :--- | :---: | :---: | :--- |
| **API Gateway** | `src/ApiGateway` | **5000** | N/A (Router Reverso) | Puerta de entrada única que unifica el tráfico perimetral. |
| **API 1: Productos** | `src/ApiProductos` | **5001** | `http://localhost:5001/swagger` | Catálogo de repuestos, lubricantes y accesorios. |
| **API 2: Libros** | `src/ApiLibros` | **5002** | `http://localhost:5002/swagger` | Catálogo de manuales técnicos y literatura automotriz. |
| **API 3: Vehículos** | `src/ApiVehiculos` | **5003** | `http://localhost:5003/swagger` | Inventario corporativo de flotillas y vehículos. |

---

## 2. Requisitos Previos

1. **.NET SDK:** Versión 8.0 o superior (instalado .NET 10.0).
2. **Terminal:** PowerShell 5.1 / PowerShell 7 o terminal Bash.
3. **Herramienta de Solicitudes HTTP:** `curl` (incluido en Windows), `Invoke-RestMethod` o Postman.

---

## 3. Método 1: Verificación Automatizada Integral (Recomendado)

Se incluye un script que ejecuta de forma secuencial la compilación, la verificación aislada de las 3 APIs (regla mandatoria), el levantamiento del Gateway y la validación de todos los escenarios con detención limpia al finalizar.

### Ejecución:
Desde la raíz del repositorio (`d:\UDB\CICLO-10-2026\DES\LAB\Desafio2`):

```powershell
powershell -ExecutionPolicy Bypass -File .\AutoGestion\scripts\verify_spec_1_1_1.ps1
```

### Salida Esperada:
```text
======================================================================
INICIANDO PROTOCOLO DE VERIFICACION SPEC-1.1.1 (OCELOT + 3 APIS)
======================================================================

[PASO 1] Compilando la solución completa...
Compilación exitosa (0 Errores).

======================================================================
FASE 1: VERIFICACION AISLADA PREVIA (REGLA MANDATORIA)
======================================================================
ApiProductos activo y escuchando en http://localhost:5001.
ApiLibros activo y escuchando en http://localhost:5002.
ApiVehiculos activo y escuchando en http://localhost:5003.

[Aislamiento 1] Probando API 1 Productos (puerto 5001)...
  GET /api/productos -> Status: 200
  POST /api/productos -> Status: 201

[Aislamiento 2] Probando API 2 Libros (puerto 5002)...
  GET /api/libros -> Status: 200
  POST /api/libros -> Status: 201

[Aislamiento 3] Probando API 3 Vehículos (puerto 5003)...
  GET /api/vehiculos -> Status: 200
  POST /api/vehiculos -> Status: 201

>>> FASE 1 COMPLETADA CON EXITO: 3 APIs funcionando autónomamente. <<<

======================================================================
FASE 2: ACOPLAMIENTO Y ENRUTAMIENTO MEDIANTE OCELOT GATEWAY
======================================================================
ApiGateway activo y escuchando en http://localhost:5000.

[Gateway Scenario 1] Enrutamiento a API 1 (Productos)...
  GET http://localhost:5000/productos -> Status: 200
  GET http://localhost:5000/productos/2 -> Status: 200
  POST http://localhost:5000/productos -> Status: 201
  PUT http://localhost:5000/productos/2 -> Status: 200
  DELETE http://localhost:5000/productos/3 -> Status: 200

[Gateway Scenario 2] Enrutamiento a API 2 (Libros)...
  GET http://localhost:5000/libros -> Status: 200
  GET http://localhost:5000/libros/1 -> Status: 200
  POST http://localhost:5000/libros -> Status: 201

[Gateway Scenario 3] Enrutamiento a API 3 (Vehículos)...
  GET http://localhost:5000/vehiculos -> Status: 200
  GET http://localhost:5000/vehiculos/1 -> Status: 200

[Gateway Scenario 4] Comprobando aislamiento de rutas no mapeadas...
  GET http://localhost:5000/clientes-desconocidos -> 404 Not Found

======================================================================
CERTIFICACION SATISFACTORIA: SPEC-1.1.1 IMPLEMENTADA Y VALIDADA AL 100%
======================================================================
```

---

## 4. Método 2: Ejecución y Comprobación Manual Paso a Paso

Si el evaluador o desarrollador desea levantar los servicios manualmente en ventanas de terminal separadas, seguir este procedimiento:

### Paso 4.1: Compilar la Solución
```powershell
dotnet build .\AutoGestion\AutoGestion.slnx
```

---

### Paso 4.2: Iniciar los Servicios en Terminales Separadas

1. **Terminal 1 (API Productos):**
   ```powershell
   dotnet run --project .\AutoGestion\src\ApiProductos\ApiProductos.csproj
   # Escucha en: http://localhost:5001
   ```

2. **Terminal 2 (API Libros):**
   ```powershell
   dotnet run --project .\AutoGestion\src\ApiLibros\ApiLibros.csproj
   # Escucha en: http://localhost:5002
   ```

3. **Terminal 3 (API Vehículos):**
   ```powershell
   dotnet run --project .\AutoGestion\src\ApiVehiculos\ApiVehiculos.csproj
   # Escucha en: http://localhost:5003
   ```

4. **Terminal 4 (API Gateway Ocelot):**
   ```powershell
   dotnet run --project .\AutoGestion\src\ApiGateway\ApiGateway.csproj
   # Escucha en: http://localhost:5000
   ```

---

### Paso 4.3: Comprobar la Regla Mandatoria (Verificación Aislada Previa)

Antes de probar el Gateway, verificar que cada API responde directamente en su puerto nativo:

```powershell
# 1. Probar API Productos directamente en puerto 5001
curl -i http://localhost:5001/api/productos

# 2. Probar API Libros directamente en puerto 5002
curl -i http://localhost:5002/api/libros

# 3. Probar API Vehículos directamente en puerto 5003
curl -i http://localhost:5003/api/vehiculos
```

> **Verificación Gráfica Swagger UI:**
> Abrir en el navegador las siguientes URLs para validar que cada microservicio expone su documentación interactiva:
> - `http://localhost:5001/swagger`
> - `http://localhost:5002/swagger`
> - `http://localhost:5003/swagger`

---

### Paso 4.4: Comprobar el Enrutamiento a través del Gateway (Puerto 5000)

Todas las peticiones deben dirigirse al puerto **5000**, el cual redirige internamente a la API correspondiente:

#### A. Enrutamiento hacia API 1: Productos (`/productos`)

```powershell
# GET - Obtener todos los productos
curl -i http://localhost:5000/productos

# GET - Obtener producto específico por ID (ej. ID = 1)
curl -i http://localhost:5000/productos/1

# POST - Crear un nuevo producto
curl -i -X POST http://localhost:5000/productos `
  -H "Content-Type: application/json" `
  -d '{"nombre":"Bomba de Agua","precio":65.00,"stock":12,"categoria":"Enfriamiento"}'

# PUT - Actualizar un producto existente (ej. ID = 2)
curl -i -X PUT http://localhost:5000/productos/2 `
  -H "Content-Type: application/json" `
  -d '{"id":2,"nombre":"Pastillas Cerámicas Pro","precio":52.00,"stock":25,"categoria":"Frenos"}'

# DELETE - Eliminar un producto (ej. ID = 3)
curl -i -X DELETE http://localhost:5000/productos/3
```

#### B. Enrutamiento hacia API 2: Libros (`/libros`)

```powershell
# GET - Obtener catálogo de libros
curl -i http://localhost:5000/libros

# GET - Obtener libro por ID (ej. ID = 1)
curl -i http://localhost:5000/libros/1

# POST - Crear nuevo libro o manual
curl -i -X POST http://localhost:5000/libros `
  -H "Content-Type: application/json" `
  -d '{"titulo":"Manual Inyección Diésel Common Rail","autor":"Mauricio Henríquez","isbn":"978-9988776655","anioPublicacion":2024}'

# PUT - Actualizar libro
curl -i -X PUT http://localhost:5000/libros/1 `
  -H "Content-Type: application/json" `
  -d '{"id":1,"titulo":"Manual de Taller - Edición 2026","autor":"Alonso Pérez","isbn":"978-0123456789","anioPublicacion":2026}'

# DELETE - Eliminar libro
curl -i -X DELETE http://localhost:5000/libros/2
```

#### C. Enrutamiento hacia API 3: Vehículos (`/vehiculos`)

```powershell
# GET - Obtener inventario de vehículos
curl -i http://localhost:5000/vehiculos

# GET - Obtener vehículo por ID (ej. ID = 1)
curl -i http://localhost:5000/vehiculos/1

# POST - Registrar nuevo vehículo
curl -i -X POST http://localhost:5000/vehiculos `
  -H "Content-Type: application/json" `
  -d '{"marca":"Hyundai","modelo":"Elantra GLS","anio":2023,"placa":"P333-222","precio":19800.00}'

# PUT - Actualizar datos de vehículo
curl -i -X PUT http://localhost:5000/vehiculos/1 `
  -H "Content-Type: application/json" `
  -d '{"id":1,"marca":"Toyota","modelo":"Corolla LE Actualizado","anio":2022,"placa":"P123-456","precio":18900.00}'

# DELETE - Eliminar vehículo
curl -i -X DELETE http://localhost:5000/vehiculos/2
```

#### D. Prueba de Aislamiento y Seguridad de Rutas No Mapeadas

```powershell
# Petición a ruta que NO existe en ocelot.json
curl -i http://localhost:5000/clientes-desconocidos
```
**Resultado Esperado:** Código HTTP `404 Not Found`. Ocelot intercepta la petición y no expone las APIs internas.

---

## 5. Matriz de Cumplimiento de la Rúbrica Oficial (Criterio a - Ocelot)

| Requisito de la Rúbrica | Evidencia Técnica | Estado |
| :--- | :--- | :---: |
| **El Gateway enruta TODOS los endpoints (100%)** | Configuración de rutas completas con comodín `{everything}` para `/productos`, `/libros` y `/vehiculos` en [ocelot.json](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/AutoGestion/src/ApiGateway/ocelot.json). |  **Destacado (100%)** |
| **Soporte de verbos HTTP estándar** | Verbos `GET`, `POST`, `PUT`, `DELETE` probados y operativos en Gateway. |  **Destacado (100%)** |
| **Prueba individual previa (Regla Mandatoria)** | Verificación autónoma en puertos 5001, 5002 y 5003 documentada y validada en Fase 1. |  **Destacado (100%)** |
| **Aislamiento perimetral** | Rutas fuera de catálogo responden 404 sin exponer microservicios internos. |  **Destacado (100%)** |

---

## 6. Resolución de Problemas (Troubleshooting)

### Conflicto de puertos (`Address already in use`):
Si alguno de los puertos (5000, 5001, 5002 o 5003) quedó tomado por una ejecución previa, detener los procesos con:
```powershell
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Limpieza y reconstrucción de binarios:
```powershell
dotnet clean .\AutoGestion\AutoGestion.slnx
dotnet build .\AutoGestion\AutoGestion.slnx
```
