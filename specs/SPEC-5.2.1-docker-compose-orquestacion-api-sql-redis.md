# SPEC-5.2.1: Orquestación Integral del Ecosistema con Docker Compose (API, SQL Server y Redis)

## 1. Objective
Orquestar el despliegue simultáneo, la interconexión en red privada virtual y la sincronización de dependencias de los 3 servicios fundamentales de AutoGestion S.A. (API Principal, Base de Datos Microsoft SQL Server y Servidor de Caché Redis) mediante un único archivo `docker-compose.yml`, asegurando el levantamiento integral del ecosistema con un solo comando y garantizando una tasa de conectividad exitosa del 100% entre la API y sus motores de persistencia y memoria.

## 2. Scope
### 2.1. Included
* Creación y configuración del archivo `docker-compose.yml` en la raíz del repositorio.
* Definición de los 3 servicios contenedorizados requeridos:
  1. `api-backend`: API en .NET 10 construida a partir de Dockerfile.
  2. `sql-server`: Motor relacional SQL Server (`mcr.microsoft.com/mssql/server:2022-latest`).
  3. `redis-cache`: Motor de caché en memoria (`redis:7-alpine`).
* Creación de red virtual interna (`bridge`) para resolución de nombres de host (`sql-server`, `redis-cache`).
* Definición de volúmenes persistentes (`named volumes`) para los datos de SQL Server.
* Configuración de variables de entorno para inyección de cadenas de conexión en la API.
* Directivas de salud (`healthcheck`) y dependencias de arranque (`depends_on`).

### 2.2. Not Included (Out of Scope)
* Configuración de clústeres Kubernetes o alta disponibilidad con Redis Sentinel/Cluster.
* Despliegue en servidores cloud remotos.

## 3. Context and Restrictions
* **Context:** Capa de infraestructura local y portabilidad. Cumple el requerimiento esencial de la rúbrica oficial de examen: *"El archivo docker-compose.yml inicializa los 3 contenedores a la vez, y la API logra establecer conexión correcta con SQL y Redis"*.
* **Restrictions:**
  * Los 3 contenedores deben iniciar simultáneamente con la instrucción `docker-compose up -d`.
  * La API no debe abortar por falta de disponibilidad de SQL Server al iniciar; debe esperar a que el servicio esté saludable.

## 4. Design (Implementation Details)
* **Architecture:**
```text
                       [ docker-compose.yml ]
                                 │
                 ┌───────────────┴───────────────┐
                 │       Red: autogestion-net    │
                 ▼                               ▼
       [ sql-server:1433 ]              [ redis-cache:6379 ]
                 ▲                               ▲
                 │ (Conexión TCP)                │ (Conexión TCP)
                 └───────────────┬───────────────┘
                                 │
                        [ api-backend:80 ]
                                 ▲
                                 │ (Mapeo de Puerto: 8080:80)
                       [ Cliente / Auditor ]
```
* **Data Model (Archivo `docker-compose.yml` Completo):**
```yaml
version: '3.8'

services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: autogestion-sql
    restart: always
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=SuperP@ssw0rd2026!
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sql_data:/var/opt/mssql
    networks:
      - autogestion-net
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SuperP@ssw0rd2026!' -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 20s

  redis-cache:
    image: redis:7-alpine
    container_name: autogestion-redis
    restart: always
    ports:
      - "6379:6379"
    networks:
      - autogestion-net
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  api-backend:
    build:
      context: .
      dockerfile: src/AutoGestion.Api/Dockerfile
    container_name: autogestion-api
    restart: on-failure
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=80
      - ConnectionStrings__DefaultConnection=Server=sql-server,1433;Database=AutoGestionDb;User Id=sa;Password=SuperP@ssw0rd2026!;TrustServerCertificate=True;
      - ConnectionStrings__RedisConnection=redis-cache:6379
    ports:
      - "8080:80"
    depends_on:
      sql-server:
        condition: service_healthy
      redis-cache:
        condition: service_healthy
    networks:
      - autogestion-net

networks:
  autogestion-net:
    driver: bridge

volumes:
  sql_data:
    driver: local
```
* **API Contracts:**
  * Hostname interno para SQL: `sql-server:1433`.
  * Hostname interno para Redis: `redis-cache:6379`.
  * Puerto externo expuesto para el auditor: `http://localhost:8080`.
* **UI/UX:** Visualización del ecosistema en Docker Desktop, mostrando los 3 contenedores agrupados bajo el proyecto `desafio2` con indicador visual de color verde (`Running` / `Healthy`).

## 5. Acceptance Criteria
* **Scenario 1: Inicialización Simultánea Exitosa (Rúbrica Oficial 100%)**
  * **Given** el archivo `docker-compose.yml` en la raíz del proyecto y Docker en ejecución,
  * **When** el usuario ejecuta `docker-compose up -d --build`,
  * **Then** Docker crea la red `autogestion-net`, inicializa los contenedores de `sql-server` y `redis-cache`, aguarda sus healthchecks y levanta exitosamente `api-backend`.
* **Scenario 2: Interconexión y Conectividad con Motores de Datos**
  * **Given** los 3 contenedores corriendo en estado Healthy,
  * **When** se envía una petición `GET http://localhost:8080/api/productos` a través del puerto expuesto de la API,
  * **Then** la API se comunica exitosamente con SQL Server y Redis, devolviendo la respuesta sin arrojar ninguna excepción de `SocketException` o fallo de conexión.
* **Scenario 3: Resiliencia ante Caída de Contenedor Secundario**
  * **Given** el servicio Redis detenido momentáneamente con `docker stop autogestion-redis`,
  * **When** se solicita el listado de productos a la API,
  * **Then** la API no crashea, activa su fallback y consulta la información directamente desde SQL Server.

## 6. Verification Plan
* Ejecución de `docker compose ps` comprobando que los 3 servicios figuren con estado `Up (healthy)`.
* Inspección de logs con `docker compose logs api-backend` verificando la aplicación de migraciones y conexión exitosa.
* Prueba de invocación de endpoints mediante cURL o Postman a `http://localhost:8080`.

## 7. Security and Privacy
* Red virtual privada: el tráfico entre la API, SQL y Redis transcurre encapsulado en la red bridge de Docker.
* Aislamiento de volúmenes: los datos sensibles residen en volúmenes protegidos por el motor de Docker.

## 8. Risks and Mitigation
* **Risk:** Carrera de arranque (Race condition) donde la API arranca antes de que SQL Server termine de crear sus bases maestras. -> **Mitigation:** Uso estricto de directiva `condition: service_healthy` vinculada a un script de `sqlcmd` en el healthcheck.

## 9. Deliverables & Config as Code
* Archivo `docker-compose.yml` en la raíz del repositorio.
* Captura de pantalla de la terminal o Docker Desktop con los 3 contenedores activos y conectados para el informe en PDF.

## 10. Definition of Done (DoD)
* [x] Archivo `docker-compose.yml` probado y funcional con `docker compose up -d`.
* [x] Los 3 servicios (API, SQL, Redis) arrancan e interconectan simultáneamente.
* [x] Conexión de la API a SQL Server confirmada.
* [x] Conexión de la API a Redis confirmada.
* [x] Captura de pantalla generada para la evidencia del criterio (e) de la rúbrica.
