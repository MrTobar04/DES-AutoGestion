# Guía Completa para Pruebas en Postman con Docker

Esta guía contiene toda la información necesaria para ejecutar y validar el sistema **DES-AutoGestion** utilizando exclusivamente la infraestructura desplegada mediante **Docker Compose**.

---

## 1. Arquitectura de Servicios y Puertos en Docker

| Servicio | Contenedor Docker | Puerto Host (Postman) | Descripción | Requiere Autenticación JWT |
| :--- | :--- | :--- | :--- | :---: |
| **API Gateway** | `autogestion-api-gateway` | **`http://localhost:5000`** | Gateway Ocelot, enrutador principal y Rate Limiting (10 req/min). | Según la ruta |
| **API Productos** | `autogestion-api-productos` | **`http://localhost:5001`** | Catálogo de Productos (Directo / Aislado). Integrado con Redis. | No |
| **API Libros** | `autogestion-api-libros` | **`http://localhost:5002`** | Catálogo de Libros y Manuales (Directo / Aislado). Integrado con Redis. | No |
| **API Vehículos** | `autogestion-api-vehiculos` | **`http://localhost:5003`** | Inventario de Vehículos, Marcas, Modelos y Auth Identity. | Sí (`[Authorize]`) |
| **SQL Server** | `autogestion-sql` | `localhost:1433` | Base de datos SQL Server 2022 (Identity y Vehículos). | N/A |
| **Redis Cache** | `autogestion-redis` | `localhost:6379` | Caché en memoria para listados de Productos, Libros y Vehículos. | N/A |

---

## 2. Configuración en Postman

### Variables de Entorno Recomendadas

Crea un **Environment** en Postman con las siguientes variables:

| Variable | Valor Inicial / Actual | Descripción |
| :--- | :--- | :--- |
| `gateway_url` | `http://localhost:5000` | URL Base del API Gateway Ocelot |
| `productos_url` | `http://localhost:5001` | URL Directa a API Productos |
| `libros_url` | `http://localhost:5002` | URL Directa a API Libros |
| `vehiculos_url` | `http://localhost:5003` | URL Directa a API Vehículos |
| `jwt_token` | *(Vacío al inicio)* | Se obtiene ejecutando la petición de Login |

---

## 3. Flujo de Autenticación y Gestión de Usuarios (JWT)

### 3.1 Registrar Usuario
- **Método**: `POST`
- **URL Vía Gateway**: `http://localhost:5000/register`
- **URL Directa**: `http://localhost:5003/api/auth/register`
- **Headers**:
  - `Content-Type: application/json`
- **Body (raw JSON)**:
  ```json
  {
    "nombre": "Juan Pérez",
    "dui": "01234567-9",
    "email": "juan.perez@ejemplo.com",
    "password": "Password123!"
  }
  ```
  > [!IMPORTANT]
  > **Regla de Formato DUI**: El campo `dui` debe cumplir con la expresión regular `^\d{8}-\d$` (8 dígitos, guión y 1 dígito). Ej: `01234567-9`. De lo contrario retornará `400 Bad Request`.

- **Respuesta Esperada (`200 OK`)**:
  ```json
  {
    "mensaje": "Usuario registrado exitosamente",
    "email": "juan.perez@ejemplo.com",
    "nombre": "Juan Pérez",
    "dui": "01234567-9"
  }
  ```

---

### 3.2 Iniciar Sesión (Login & Obtención de Token)
- **Método**: `POST`
- **URL Vía Gateway**: `http://localhost:5000/login`
- **URL Directa**: `http://localhost:5003/api/auth/login`
- **Headers**:
  - `Content-Type: application/json`
- **Body (raw JSON)**:
  ```json
  {
    "email": "juan.perez@ejemplo.com",
    "password": "Password123!"
  }
  ```
- **Respuesta Esperada (`200 OK`)**:
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiration": "2026-09-11T05:30:00Z",
    "email": "juan.perez@ejemplo.com"
  }
  ```
- **Próximo Paso**: Copia el valor del campo `token` guardándolo en la variable `jwt_token` de tu entorno en Postman.

---

## 4. Endpoints Disponibles y Payloads de Prueba

### 4.1 Microservicio de Productos

#### A) Obtener Productos (GET)
- **URL Vía Gateway**: `GET http://localhost:5000/productos`
- **URL Directa**: `GET http://localhost:5001/api/productos`
- **Cache**: La primera llamada consulta los datos; las llamadas posteriores se obtienen de Redis por 5 minutos.

#### B) Obtener Producto por ID (GET)
- **URL Vía Gateway**: `GET http://localhost:5000/productos/1`
- **URL Directa**: `GET http://localhost:5001/api/productos/1`

#### C) Crear Producto (POST)
- **URL Vía Gateway**: `POST http://localhost:5000/productos`
- **URL Directa**: `POST http://localhost:5001/api/productos`
- **Body (raw JSON)**:
  ```json
  {
    "nombre": "Aceite Sintético 5W-30",
    "precio": 29.99,
    "stock": 40,
    "categoria": "Mantenimiento"
  }
  ```
  *(Al crear, invalidará automáticamente la caché en Redis)*.

#### D) Actualizar Producto (PUT)
- **URL Vía Gateway**: `PUT http://localhost:5000/productos/1`
- **Body (raw JSON)**:
  ```json
  {
    "id": 1,
    "nombre": "Filtro de Aceite Sintético Premium",
    "precio": 18.50,
    "stock": 60,
    "categoria": "Mantenimiento"
  }
  ```

#### E) Eliminar Producto (DELETE)
- **URL Vía Gateway**: `DELETE http://localhost:5000/productos/1`

---

### 4.2 Microservicio de Libros

#### A) Obtener Libros (GET)
- **URL Vía Gateway**: `GET http://localhost:5000/libros`
- **URL Directa**: `GET http://localhost:5002/api/libros`

#### B) Obtener Libro por ID (GET)
- **URL Vía Gateway**: `GET http://localhost:5000/libros/1`

#### C) Crear Libro (POST)
- **URL Vía Gateway**: `POST http://localhost:5000/libros`
- **Body (raw JSON)**:
  ```json
  {
    "titulo": "Manual de Electricidad Automotriz Moderna",
    "autor": "Roberto Gómez",
    "isbn": "978-9992301234",
    "anioPublicacion": 2025
  }
  ```

---

### 4.3 Microservicio de Vehículos (Requiere Token JWT)

> [!WARNING]
> **Autenticación Requerida**: Para consumir los endpoints de Vehículos, Marcas o Modelos, debes incluir el Header de Autenticación:
> `Authorization: Bearer <jwt_token>`

#### A) Obtener Vehículos (GET)
- **URL Vía Gateway**: `GET http://localhost:5000/vehiculos`
- **URL Directa**: `GET http://localhost:5003/api/vehiculos`
- **Headers**:
  - `Authorization`: `Bearer {{jwt_token}}`

#### B) Crear Vehículo (POST)
- **URL Vía Gateway**: `POST http://localhost:5000/vehiculos`
- **URL Directa**: `POST http://localhost:5003/api/vehiculos`
- **Headers**:
  - `Authorization`: `Bearer {{jwt_token}}`
  - `Content-Type`: `application/json`
- **Body (raw JSON)**:
  ```json
  {
    "modeloId": 1,
    "marca": "Toyota",
    "modelo": "Corolla",
    "anio": 2024,
    "precio": 22500.00,
    "placa": "P123-456"
  }
  ```

---

## 5. Verificación de Funcionalidades Especiales

### 5.1 Rate Limiting (Control de Tráfico)
- **Regla**: El Gateway Ocelot permite un máximo de **10 peticiones por minuto** por cliente.
- **Prueba**: Realiza 11 peticiones seguidas a cualquier endpoint del Gateway (ej. `GET http://localhost:5000/productos`).
- **Resultado en la 11ª Petición**:
  - **Código HTTP**: `429 Too Many Requests`
  - **Body**:
    ```json
    "Se ha excedido el límite máximo de 10 peticiones por minuto para este servicio."
    ```

### 5.2 Documentación Swagger Interactiva
Si deseas explorar las especificaciones en la interfaz web de Swagger mientras Docker está en ejecución:
- **API Productos**: `http://localhost:5001/swagger`
- **API Libros**: `http://localhost:5002/swagger`
- **API Vehículos**: `http://localhost:5003/swagger`

---

## 6. Comandos Útiles de Docker para Debugging

Si alguna prueba en Postman falla o deseas verificar los contenedores:

```bash
# Verificar estado y salud de los contenedores
docker compose ps

# Ver logs en tiempo real del API Gateway
docker compose logs -f api-gateway

# Ver logs del microservicio de Auth y Vehículos
docker compose logs -f api-vehiculos

# Reiniciar todos los servicios
docker compose restart
```
