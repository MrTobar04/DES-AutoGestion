# Guión de Validación Manual mediante Swagger UI: AutoGestion S.A. — Desafío 2

**Asignatura:** Desarrollo de Software Empresarial (DSE104)  
**Institución:** Universidad Don Bosco — Facultad de Ingeniería  
**Objetivo:** Proporcionar un procedimiento interactivo y simplificado paso a paso para la validación manual de cada uno de los literales (**a, b, c, d, e**) requeridos en el Desafío 2 utilizando las interfaces interactivas de **Swagger UI** (`/swagger`) de cada microservicio, complementado con comandos rápidos para la evaluación integral y captura de evidencias para el informe PDF.

---

## 🧭 Panel de Control de Interfaces Swagger UI

Con los contenedores de Docker activos, abra las siguientes pestañas en su navegador web:

| Microservicio | URL de Swagger UI | Propósito en la Validación |
| :--- | :--- | :--- |
| **API 1: Productos** | [`http://localhost:5001/swagger`](http://localhost:5001/swagger) | Validación de **Caché Redis (Literal b)** y operaciones CRUD. |
| **API 2: Libros** | [`http://localhost:5002/swagger`](http://localhost:5002/swagger) | Validación de catálogo y persistencia en SQL Server. |
| **API 3: Vehículos & Auth** | [`http://localhost:5003/swagger`](http://localhost:5003/swagger) | Validación de **Identity, JWT, [Authorize] (Literal d)** y modelos. |
| **API Gateway (Ocelot)** | `http://localhost:5000` | Punto de entrada perimetral con **Rate Limiting (Literal a)**. |

---

## 1. Prerrequisitos y Puesta en Marcha

### 1.1. Iniciar la Infraestructura con Docker Compose
En una terminal de PowerShell en la raíz del proyecto, ejecute:

```powershell
docker compose up -d
```

Verifique que todos los contenedores figuren como `Up` o `Healthy`:
```powershell
docker compose ps
```

---

## 2. Literal a) Puerta de Entrada y Límite de Peticiones (Ocelot & Rate Limiting)

> **Requerimiento del Jefe:** *"Hagan que todas las peticiones pasen por un solo lugar (API Gateway). Quiero que cuando alguien pida `/productos` o `/libros`, el sistema lo redirija a la API correcta. Además, pongan un límite de solo 10 peticiones por minuto para que no nos ataquen."*

### 2.1. Verificación del Enrutamiento Centralizado
El API Gateway centraliza en el puerto `5000` los endpoints de los microservicios documentados en sus respectivos Swagger UI:

1. Abra una nueva pestaña o terminal y consulte los endpoints a través del Gateway:
   * **Productos vía Gateway:** `http://localhost:5000/productos` $\to$ Devuelve la lista servida internamente por `ApiProductos` (`:5001`).
   * **Libros vía Gateway:** `http://localhost:5000/libros` $\to$ Devuelve el catálogo servido internamente por `ApiLibros` (`:5002`).

### 2.2. Verificación de Rate Limiting (10 peticiones / minuto)
En Swagger UI o consola, ejecute más de 10 peticiones consecutivas hacia el Gateway:

```powershell
1..12 | ForEach-Object {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5000/productos" -Method Get -UseBasicParsing -TimeoutSec 3
        Write-Host "Petición $_ -> HTTP $($r.StatusCode) OK" -ForegroundColor Green
    } catch {
        Write-Host "Petición $_ -> HTTP $($_.Exception.Response.StatusCode.value__) (Rate Limit Activado)" -ForegroundColor Yellow
    }
}
```

* **Resultado Visual:** Las peticiones 1 a 10 responden `200 OK`. A partir de la petición 11, el Gateway responde `HTTP 429 Too Many Requests` con el mensaje de cuota excedida.

---

## 3. Literal b) Aceleración de Consultas y Mutaciones (Redis Cache & Invalidación)

> **Requerimiento del Jefe:** *"Los listados de productos tardan mucho. Usen Redis para guardar la lista en caché por 5 minutos. Cuando alguien agregue, edite o elimine un producto, la caché se debe borrar para que no se vean datos viejos."*

### 3.1. Validación Interactiva en Swagger UI de Productos (`:5001/swagger`)

1. Ingrese a [`http://localhost:5001/swagger`](http://localhost:5001/swagger).
2. Expanda el endpoint **`GET /api/productos`**.
3. Haga clic en **`Try it out`** y luego en **`Execute`**.
   * **Comportamiento:** La primera petición consulta SQL Server y almacena el resultado en Redis por 5 minutos (TTL = 300 s).
4. Vuelva a presionar **`Execute`** inmediatamente.
   * **Comportamiento:** La respuesta es servida al instante desde la memoria RAM de Redis.

---

### 3.2. Validación de Invalidación de Caché (Mutaciones POST/DELETE)

1. En Swagger UI, expanda el endpoint **`POST /api/productos`**.
2. Presione **`Try it out`** y pegue el siguiente JSON en el cuerpo:
   ```json
   {
     "nombre": "Batería Bosch S4 12V",
     "descripcion": "Batería de libre mantenimiento 65Ah",
     "precio": 129.99,
     "stock": 10,
     "categoria": "Baterías"
   }
   ```
3. Presione **`Execute`**.
   * **Respuesta:** `HTTP 201 Created` con el ID asignado (ej. `ID: 4`).
   * **Efecto Interno:** Se ejecuta automáticamente la purga de caché `_cache.RemoveAsync("productos_all")`.
4. Regrese a **`GET /api/productos`** y presione **`Execute`**.
   * **Resultado:** El nuevo producto aparece de inmediato en la lista, comprobando que **no se muestran datos obsoletos**.
5. *(Opcional)* Expanda **`DELETE /api/productos/{id}`**, ingrese el ID creado y presione **`Execute`** para limpiar el registro y comprobar la segunda invalidación de caché.

---

## 4. Literal c) Batería de Pruebas Automatizadas (xUnit, DataAnnotations & InMemory)

> **Requerimiento del Jefe:** *"Tenemos un registro de Personas con validaciones (el DUI debe tener formato `00000000-0`, el nombre es obligatorio). Escriban pruebas unitarias que comprueben que el sistema devuelve error (`BadRequest`) cuando los datos están mal, y que guarda bien cuando están correctos."*

### 4.1. Ejecución de la Suite xUnit
Abra la terminal y ejecute el comando:

```powershell
dotnet test AutoGestion/tests/AutoGestion.Tests/AutoGestion.Tests.csproj --verbosity normal
```

**Resultado Visual Esperado:**
```text
Serie de pruebas para AutoGestion.Tests.dll (.NETCoreApp,Version=v10.0)

La serie de pruebas se ejecutó correctamente.
Pruebas totales: 89
     Correcto: 89
     Incorrecto: 0
     Omitido: 0
 Tiempo total: 1.36 Segundos
Compilación correcta: 0 Advertencias, 0 Errores.
```

### 4.2. Validación Interactiva de Reglas en Swagger (`:5003/swagger`)
Puede comprobar visualmente las validaciones de DataAnnotations (DUI y Nombre) en Swagger UI:

1. Vaya a [`http://localhost:5003/swagger`](http://localhost:5003/swagger) $\to$ **`POST /api/auth/register`**.
2. Presione **`Try it out`** y pruebe enviar un DUI sin guión (`"012345678"`):
   ```json
   {
     "nombre": "Prueba Error",
     "dui": "012345678",
     "email": "error@autogestion.com",
     "password": "Password123!"
   }
   ```
3. Presione **`Execute`**.
   * **Respuesta:** `HTTP 400 Bad Request` indicando: `"El formato del DUI debe ser 00000000-0"`.

---

## 5. Literal d) Seguridad, Autenticación y Control de Acceso (Identity & JWT)

> **Requerimiento del Jefe:** *"Solo la gente registrada puede ver los vehículos. Configuren Identity con los endpoints `/register` y `/login`. Pongan el atributo `[Authorize]` en los controladores para que, si no ha iniciado sesión, devuelva error 401."*

Toda la validación de seguridad se realiza directamente en **Swagger UI de Vehículos** ([`http://localhost:5003/swagger`](http://localhost:5003/swagger)):

### Paso 1: Comprobar Protección `[Authorize]` (Rechazo Anónimo 401)
1. En Swagger UI, expanda **`GET /api/vehiculos`**.
2. Presione **`Try it out`** y luego **`Execute`** (sin haber iniciado sesión).
3. **Resultado:** Se muestra el código de respuesta **`HTTP 401 Unauthorized`** (candado cerrado).

---

### Paso 2: Registrar Usuario (`POST /api/auth/register`)
1. Expanda el endpoint **`POST /api/auth/register`**.
2. Presione **`Try it out`** y pegue el JSON:
   ```json
   {
     "nombre": "Usuario Demo Swagger",
     "dui": "01234567-8",
     "email": "swagger.user@autogestion.com",
     "password": "PasswordSeguro123!"
   }
   ```
3. Presione **`Execute`**.
4. **Resultado:** Código **`HTTP 200 OK`** con el mensaje:
   ```json
   {
     "mensaje": "Usuario registrado exitosamente",
     "email": "swagger.user@autogestion.com",
     "nombre": "Usuario Demo Swagger",
     "dui": "01234567-8"
   }
   ```

---

### Paso 3: Iniciar Sesión y Obtener Token JWT (`POST /api/auth/login`)
1. Expanda el endpoint **`POST /api/auth/login`**.
2. Presione **`Try it out`** y pegue el JSON:
   ```json
   {
     "email": "swagger.user@autogestion.com",
     "password": "PasswordSeguro123!"
   }
   ```
3. Presione **`Execute`**.
4. **Resultado:** Código **`HTTP 200 OK`** con la estructura:
   ```json
   {
     "mensaje": "Inicio de sesión exitoso",
     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.ey...",
     "expiracionMinutos": 60,
     "email": "swagger.user@autogestion.com"
   }
   ```
5. **Copie todo el valor del campo `token`** (sin comillas).

---

### Paso 4: Inyectar el Token en Swagger mediante el Botón "Authorize"
1. Desplácese a la parte superior derecha de Swagger UI y haga clic en el botón verde **`Authorize 🔓`**.
2. En el cuadro de texto **Value**, ingrese:
   ```text
   Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.ey...
   ```
   *(Escriba `Bearer`, un espacio y pegue el token copiado).*
3. Haga clic en el botón **`Authorize`** y luego en **`Close`**. El candado cambiará a **cerrado/autenticado 🔒**.

---

### Paso 5: Consultar Vehículos con Sesión Autenticada
1. Regrese al endpoint **`GET /api/vehiculos`**.
2. Presione **`Execute`**.
3. **Resultado:** Código **`HTTP 200 OK`** con la lista completa de vehículos retornada desde SQL Server.

---

## 6. Literal e) Orquestación Completa en Contenedores (Docker & Docker Compose)

> **Requerimiento del Jefe:** *"Queremos que el sistema funcione igual en cualquier computadora. Hagan un `docker-compose.yml` que levante la API, la base de datos SQL y el servidor de Redis al mismo tiempo."*

### 6.1. Verificación en Swagger y Docker
1. Al abrir simultáneamente en el navegador las 3 URLs de Swagger:
   * [`http://localhost:5001/swagger`](http://localhost:5001/swagger)
   * [`http://localhost:5002/swagger`](http://localhost:5002/swagger)
   * [`http://localhost:5003/swagger`](http://localhost:5003/swagger)
   Se demuestra que los microservicios están ejecutándose concurrentemente en contenedores independientes.

2. En la terminal de PowerShell, ejecute:
   ```powershell
   docker compose ps
   ```
   **Salida Esperada:** Los 6 contenedores (`autogestion-sql`, `autogestion-redis`, `autogestion-api-gateway`, `autogestion-api-productos`, `autogestion-api-libros`, `autogestion-api-vehiculos`) en estado `Up` y `Healthy`.

---

## 7. Resumen de Capturas para el Informe PDF

Utilice Swagger UI para obtener fácilmente las capturas requeridas para el documento de entrega:

| Literal | Pantalla / Acción a Capturar en Swagger UI | Evidencia Clave Visible |
| :---: | :--- | :--- |
| **a** | Terminal con bucle hacia `http://localhost:5000/productos` | Peticiones 1 a 10 con `200 OK` y petición 11 con `429 Too Many Requests`. |
| **b** | Swagger Productos (`:5001/swagger`): `POST /api/productos` y posterior `GET` | Producto insertado y reflejado inmediatamente en la respuesta JSON. |
| **c** | Consola ejecutando `dotnet test` | Resumen de **89 pruebas pasadas en verde**. |
| **d** | Swagger Vehículos (`:5003/swagger`): Botón `Authorize` activo y `GET /api/vehiculos` | Código `200 OK` con candado cerrado y cabecera `Bearer` inyectada. |
| **e** | Docker Desktop o terminal ejecutando `docker compose ps` | Lista de los 6 contenedores con estado `Up` y `Healthy`. |
