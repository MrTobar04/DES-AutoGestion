# SPEC-5.1.1: Empaquetado y Optimización de Imágenes de Contenedor con Dockerfile Multi-Stage

## 1. Objective
Diseñar y estandarizar el proceso de empaquetado de las aplicaciones de AutoGestion S.A. mediante archivos `Dockerfile` optimizados con arquitectura Multi-Stage Build (.NET SDK para compilación y ASP.NET Runtime para ejecución), reduciendo el tamaño final de la imagen de contenedor a menos de 220 MB y eliminando herramientas de compilación del entorno final para minimizar la superficie de ataque.

## 2. Scope
### 2.1. Included
* Creación de `Dockerfile` estandarizado para las APIs basadas en .NET 10.
* Separación formal de fases: `base`, `build`, `publish` y `final`.
* Optimización de caché de capas de Docker mediante la copia selectiva inicial de archivos de proyecto (`*.csproj`) y ejecución de `dotnet restore`.
* Configuración del usuario no root por defecto y puertos de escucha expuestos (`EXPOSE 80` o `EXPOSE 5001`).

### 2.2. Not Included (Out of Scope)
* Orquestación de múltiples contenedores y redes (delegado a [SPEC-5.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-5.2.1-docker-compose-orquestacion-api-sql-redis.md)).
* Despliegue en Kubernetes o registros en la nube (AWS ECR / Docker Hub).

## 3. Context and Restrictions
* **Context:** Capa de empaquetado inmutable. Asegura que el binario de la aplicación sea idéntico en cualquier máquina host sin requerir el SDK de .NET instalado localmente.
* **Restrictions:**
  * La imagen final debe contener exclusivamente el Runtime de ASP.NET, sin compiladores, código fuente ni paquetes temporales.
  * El tiempo de construcción (build time) con capas en caché debe ser inferior a 30 segundos.

## 4. Design (Implementation Details)
* **Architecture:**
  `Stage 1: SDK Image (mcr.microsoft.com/dotnet/sdk:10.0)` $\to$ Copia `*.csproj` $\to$ `dotnet restore` $\to$ Copia código fuente $\to$ `dotnet publish -c Release -o /app/publish` $\to$
  `Stage 2: Runtime Image (mcr.microsoft.com/dotnet/aspnet:10.0)` $\to$ Copia solo `/app/publish` $\to$ `ENTRYPOINT ["dotnet", "AutoGestion.Api.dll"]`.
* **Data Model (Archivo `Dockerfile` Multi-Stage de Referencia):**
```dockerfile
# Etapa 1: Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Etapa 2: Compilación y Restauración
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia de definición de proyectos para cachear dependencias
COPY ["src/AutoGestion.Api/AutoGestion.Api.csproj", "src/AutoGestion.Api/"]
RUN dotnet restore "src/AutoGestion.Api/AutoGestion.Api.csproj"

# Copia de todo el código fuente y compilación
COPY . .
WORKDIR "/src/src/AutoGestion.Api"
RUN dotnet build "AutoGestion.Api.csproj" -c Release -o /app/build

# Etapa 3: Publicación de Binarios
FROM build AS publish
RUN dotnet publish "AutoGestion.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 4: Imagen Final de Producción
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutoGestion.Api.dll"]
```
* **Data Model (`.dockerignore`):**
```text
**/.git
**/.vs
**/.vscode
**/bin
**/obj
**/*.user
**/*.suo
**/TestResults
```
* **API Contracts:** No aplica directamente a contratos HTTP, define el contrato de entrada y salida de contenedor (`ENTRYPOINT` e imagen base).
* **UI/UX:** No aplica visualmente; la métrica es el feedback en consola durante la ejecución de `docker build`.

## 5. Acceptance Criteria
* **Scenario 1: Compilación Exitosa de Imagen Multi-Stage**
  * **Given** el código fuente del proyecto y el archivo `Dockerfile`,
  * **When** se ejecuta el comando `docker build -t autogestion-api:v1 -f src/AutoGestion.Api/Dockerfile .`,
  * **Then** Docker ejecuta todas las etapas exitosamente y produce una imagen final funcional.
* **Scenario 2: Verificación de Tamaño Reducido y Ausencia de Código Fuente**
  * **Given** la imagen construida `autogestion-api:v1`,
  * **When** se inspecciona mediante `docker images autogestion-api:v1`,
  * **Then** el tamaño total de la imagen no supera los 250 MB y no contiene archivos `.cs` en su interior.

## 6. Verification Plan
* Ejecución de `docker build` en la consola local.
* Inspección de tamaño con `docker images`.
* Ejecución de contenedor independiente `docker run -p 5001:80 autogestion-api:v1` verificando que responda a peticiones HTTP.

## 7. Security and Privacy
* Cero inclusión de credenciales locales o archivos temporales gracias a `.dockerignore`.
* Eliminación de herramientas de compilación que pudieran ser utilizadas por intrusos en caso de compromiso del contenedor.

## 8. Risks and Mitigation
* **Risk:** Aumento drástico del tiempo de construcción por copias innecesarias de archivos de compilaciones locales. -> **Mitigation:** Uso riguroso de archivo `.dockerignore` excluyendo carpetas `bin/` y `obj/`.

## 9. Deliverables & Config as Code
* Archivo `Dockerfile` en cada proyecto de API.
* Archivo `.dockerignore` en la raíz de la solución.

## 10. Definition of Done (DoD)
* [x] Archivo `Dockerfile` implementado con patrón Multi-Stage Build.
* [x] Archivo `.dockerignore` configurado en la raíz.
* [x] Imagen compilando limpiamente sin errores.
* [x] Contenedor ejecutándose y respondiendo sobre puerto HTTP 80/5001.
