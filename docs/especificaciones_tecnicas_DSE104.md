# Especificaciones Técnicas y Requerimientos - Desafío 2

**Institución:** Universidad Don Bosco
**Facultad:** Facultad de Ingeniería
**Materia:** Desarrollo de Software Empresarial - DSE104
**Cliente Ficticio:** AutoGestion S.A.

## 1. Descripción del Problema
La empresa "AutoGestion S.A." cuenta con múltiples APIs (Productos, Libros y Vehículos), las cuales presentan los siguientes inconvenientes en su estado actual:
* Saturación y sobrecarga debido a que cada API se consulta de forma individual y por separado.
* Lentitud excesiva en la carga y consultas de los listados de datos.
* Ausencia de pruebas automatizadas para verificar que las funciones principales del código sigan operando correctamente.
* Vulnerabilidades de seguridad, permitiendo que cualquier persona pueda acceder y visualizar los datos sin requerir contraseña.

## 2. Requerimientos Técnicos y Arquitectura

### 2.1 API Gateway (Ocelot - Guía 7)
Se requiere centralizar las peticiones mediante una única puerta de entrada.
* **Enrutamiento Dinámico:** El sistema debe redirigir adecuadamente al usuario a la API correcta cuando se solicite acceder a `/productos` o `/libros`.
* **Rate Limiting:** Implementar un límite de tráfico de un máximo de 10 peticiones por minuto.
* *Nota Técnica:* Se exige probar el funcionamiento individual de cada API antes de acoplarlas detrás del Gateway.

### 2.2 Caché de Datos (Redis - Guía 8)
Se requiere acelerar las consultas de listados de productos mediante almacenamiento en memoria.
* **Almacenamiento Temporal:** Guardar en caché el listado por un periodo exacto de 5 minutos.
* **Invalidación de Caché:** La caché debe borrarse automáticamente cada vez que se ejecuten operaciones que modifiquen el estado de los datos (métodos `POST`, `PUT` y `DELETE`). Esto garantiza que los clientes no obtengan información obsoleta.

### 2.3 Pruebas Unitarias (xUnit - Guía 9)
Se requiere desarrollar pruebas automatizadas enfocadas en las validaciones de la entidad `Personas`.
* **Configuración del Entorno:** Está estrictamente indicado usar una base de datos en memoria (`InMemory`) para ejecutar las pruebas, evitando afectar la base de datos real.
* **Escenarios a Validar:**
  * El formato del DUI debe ser estrictamente `00000000-0`.
  * El nombre del registro es de carácter obligatorio.
* **Resultados Esperados en Pruebas:**
  * Debe retornar un error `BadRequest` (HTTP 400) cuando la información proporcionada sea inválida.
  * Debe almacenar la información de forma exitosa cuando los datos cumplan las reglas.

### 2.4 Seguridad y Autenticación (Identity - Guía 10)
Se requiere bloquear la visualización pública del apartado de vehículos.
* **Gestión de Usuarios:** Habilitar y configurar los endpoints de `/register` y `/login`.
* **Protección de Endpoints:** Agregar el atributo `[Authorize]` en los controladores necesarios.
* **Resultado Esperado:** Todo intento de acceso sin una sesión iniciada debe ser rechazado retornando un código de error HTTP 401 (Unauthorized).

### 2.5 Contenerización (Docker)
El proyecto completo debe ser portable e independiente del entorno local donde se ejecute.
* **Orquestación de Servicios:** Elaborar un archivo `docker-compose.yml`.
* **Contenedores Requeridos:**
  1. API Principal.
  2. Base de Datos SQL.
  3. Servidor de Caché (Redis).
* **Condición de Éxito:** Los tres servicios deben levantarse e interconectarse de manera simultánea.

## 3. Criterios de Aceptación (Rúbrica Oficial)

Para lograr el puntaje destacado (9-10) en cada requerimiento, se deben cumplir al 100% las siguientes condiciones:

| Requerimiento | Criterio de Aceptación Nivel Destacado (100%) |
| :--- | :--- |
| **a) Ocelot** | El Gateway enruta **TODOS** los endpoints correctamente y el límite de 10 peticiones/minuto se aplica sin fallos. |
| **b) Redis** | El sistema guarda la lista en caché, la distribuye desde allí y **borra efectivamente la caché** tras un POST, PUT o DELETE. |
| **c) Pruebas (xUnit)**| Existen **5 o más pruebas** pasando en su totalidad, cubriendo: DUI inválido, nombre vacío, ID inexistente y un guardado exitoso. |
| **d) Identity** | Los módulos de login y registro funcionan. El decorador `[Authorize]` protege **TODOS** los endpoints requeridos (respondiendo HTTP 401 sin sesión). |
| **e) Docker** | El archivo `docker-compose.yml` inicializa los 3 contenedores a la vez, y la API logra establecer **conexión correcta con SQL y Redis**. |

*(Nota: Existen penalizaciones reduciendo la calificación a 80%, 50% o 0% si hay fallas en la invalidación de caché, si quedan rutas sin proteger, si las pruebas no alcanzan el mínimo o si algún contenedor falla al conectarse).*

## 4. Lineamientos de Entrega

El desarrollador deberá entregar los siguientes elementos:
1. **Código Fuente:** Enlace al repositorio de Git con toda la solución implementada.
2. **Documentación Gráfica:** Un archivo PDF (máximo 10 páginas) estructurado con:
   * Portada con nombre y carnet.
   * Una captura de pantalla demostrativa por cada requerimiento técnico (puntos a, b, c, d y e) evidenciando su éxito.

## 5. Convenciones Obligatorias de Control de Versiones

Se exige estrictamente el uso de los siguientes mensajes en los *commits* del repositorio para documentar el progreso:

* `git commit -m "feat(gateway): configurar Ocelot con rate limit"`
* `git commit -m "feat(cache): agregar Redis y caché de 5 minutos"`
* `git commit -m "feat(tests): crear pruebas unitarias para Personas"`
* `git commit -m "feat(security): configurar Identity y proteger endpoints"`
* `git commit -m "feat(docker): crear docker-compose con API, SQL y Redis"`
