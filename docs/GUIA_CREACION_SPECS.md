# Guía Maestra para la Creación de Especificaciones Técnicas (SPEC-GUIDE)

**Proyecto:** Pulpox V5 — Motor de Análisis Estático, Dinámico & QA en Frontend  
**Área de Gobernanza:** Arquitectura de Software & Estándares de Ingeniería  
**Versión de la Guía:** 1.0.0  
**Audiencia:** Desarrolladores Core, Líderes Técnicos, Auditores y Agentes de IA

---

## 1. INTRODUCCIÓN Y PROPÓSITO

El sistema de Especificaciones Técnicas (**SPECs**) de Pulpox V5 constituye el marco de gobernanza arquitectónica y diseño de detalle previo a la implementación de cualquier componente, refactorización o integración en el motor.

El propósito fundamental de cada SPEC es:
1. **Eliminar la ambigüedad:** Definir formalmente los contratos, estructuras de datos y algoritmos antes de escribir código.
2. **Preservar las invariantes arquitectónicas:** Garantizar el cumplimiento de principios no negociables (Zero-Copy SAB, No-Eval, rendimiento a 60 FPS, aislamiento de Workers, procesamiento 100% cliente).
3. **Establecer criterios de aceptación verificables (BDD):** Proveer escenarios `Given-When-Then` inequívocos que guíen las pruebas unitarias y de integración en Vitest.
4. **Mantener la Living Documentation:** Permitir que tanto ingenieros como agentes autónomos de IA operen con contexto completo, trazabilidad y sin desincronización entre diseño y código fuente.

---

## 2. TAXONOMÍA Y CONVENCIONES DE NOMENCLATURA

### 2.1. Estructura de Módulos del Sistema (Estratificación 1 a 7)

Toda SPEC debe pertenecer a uno de los 7 módulos del árbol arquitectónico de Pulpox V5:

| Módulo | Dominio Arquitectónico | Responsabilidad Principal |
| :---: | :--- | :--- |
| **M1** | **Capa Fundacional & Estructuras de Datos** | Text SAB Zero-Copy (8MB), EventBus EDA, Sagas, Árboles Patricia Radix, Decodificadores WeakMap, Hashing FNV-1a y Scheduler UI. |
| **M2** | **Motor de Reglas & Semántica AST** | Compilador declarativo de reglas (`RuleCompiler.js`), Parser AST ESTree, Taint Analysis, Complejidad ciclomática McCabe $v(G)$ y Pre-indexador Cross-File. |
| **M3** | **Pipeline Central Unificado & Workers** | Orquestador `UnifiedPipelineEngine.js`, Web Workers dedicados (`WorkerVerde`, `WorkerAmarillo`, `WorkerRojo`), 6 Fases canónicas, segmentación modular ($v(G) \le 10$, $\le 300$ líneas) y fase dinámica DAST/MockServer. |
| **M4** | **Motores Estocásticos & Entropía** | 10 submotores matemáticos de teoría de la información (Shannon, NCD, Chi-Cuadrado, Markov, etc.) y cálculo del Índice Global IGET. |
| **M5** | **Panel de Control & Políticas** | Mesa de trabajo modal, Circuit Breaker granular en $O(1)$, carga Drag & Drop, Hot Update de memorias JSON, selector de motores y API REST con sincronización/auditoría. |
| **M6** | **Experiencia de Usuario & Workspace** | Explorador de archivos con Crawler Worker y File System Access API, Monaco Editor multi-pestaña con diagnósticos en gutter y Navbar con sistema de temas Cupertino. |
| **M7** | **Calidad, Testing & Gobernanza** | Estándar de pares de documentación `.proceso.md` / `.invariante.md`, casos piloto de refactorización y harness de pruebas automatizadas con Vitest. |

---

### 2.2. Convención de Código y Nombre de Archivo

El nombre de archivo debe seguir estrictamente el patrón canónico en minúsculas y kebab-case:

```text
specs/SPEC-<MÓDULO>.<SUBMÓDULO>.<SECUENCIA>-<slug-kebab-case-descriptivo>.md
```

#### Reglas de Nomenclatura:
* `<MÓDULO>`: Dígito del módulo raíz (`1` al `7`).
* `<SUBMÓDULO>`: Dígito de la subárea temática dentro del módulo (ej. `1` para Patricia, `2` para Infraestructura/EDA).
* `<SECUENCIA>`: Dígito secuencial incremental de la especificación (ej. `1`, `2`, `3`).
* `<slug-kebab-case>`: Resumen conciso de 3 a 7 palabras descriptivas unidas por guiones medios (sin tildes, caracteres especiales ni espacios).

#### Ejemplos Válidos:
* `specs/SPEC-1.1.1-patricia-tree-esquema-json-memoria.md`
* `specs/SPEC-3.1.2-estandarizacion-pipeline-fases-comentarios.md`
* `specs/SPEC-5.2.4-ui-circuit-breaker-activacion-memorias.md`
* `specs/SPEC-7.1.1-estandar-documentacion-md-componente.md`

---

## 3. ESTRUCTURA CANÓNICA DE LAS 10 SECCIONES OBLIGATORIAS

Toda especificación técnica en Pulpox V5 debe contener obligatoriamente las **10 secciones canónicas numeradas**. No está permitido omitir, reordenar ni fusionar secciones.

```text
# SPEC-X.Y.Z: [Título Formal y Descriptivo]

## 1. Objective
## 2. Scope
   ### 2.1. Included
   ### 2.2. Not Included (Out of Scope)
## 3. Context and Restrictions
## 4. Design (Implementation Details)
## 5. Acceptance Criteria
## 6. Verification Plan
## 7. Security and Privacy
## 8. Risks and Mitigation
## 9. Deliverables & Config as Code
## 10. Definition of Done (DoD)
```

---

### 3.1. Encabezado / Título Principal (H1)
* **Formato:** `# SPEC-X.Y.Z: [Título Descriptivo y Canónico]`
* **Requisito:** Debe reflejar con precisión el componente y su alcance técnico.
* **Ejemplo:** `# SPEC-1.1.1: Esquema JSON en Formato Árbol Binario Patricia con Consultas Semánticas AST, Taint Flow y Grafos`

---

### 3.2. Sección 1: Objective (Objetivo)
* **Propósito:** Declaración concisa del problema técnico que resuelve la especificación y el estado final esperado.
* **Requisitos Obligatorios:**
  * Debe incluir **métricas cuantitativas duras y límites de complejidad algorítmica** donde aplique (ej. tiempo de búsqueda $O(k)$, consumo de memoria RAM $< 5$ MB para 10,000 reglas, latencia $< 0.5$ ms, límite ciclomático $v(G) \le 10$, frame budget $\le 12$ ms).
  * Explicar el impacto en la arquitectura general y la experiencia del usuario/auditor.
* **Extensión:** 1 a 2 párrafos de alta densidad informativa.

---

### 3.3. Sección 2: Scope (Alcance)
Se divide obligatoriamente en dos subapartados con formato `###`:

#### `### 2.1. Included`
* Lista detallada de todas las capacidades, contratos, transformaciones de datos, interfaces y eventos que cubre directamente este SPEC.
* Se redacta en forma de viñetas claras con lenguaje técnico específico.

#### `### 2.2. Not Included (Out of Scope)`
* Lista explícita de lo que **NO** se implementará en este SPEC para evitar *scope creep*.
* **Regla de Delegación:** Cada punto fuera de alcance debe indicar explícitamente a qué otra especificación o módulo se delega la responsabilidad (ej. `(delegada a SPEC-3.1.1)`).

---

### 3.4. Sección 3: Context and Restrictions (Contexto y Restricciones)
Estructurado mediante dos puntos en negrita:
* `* **Context:**` Explica la ubicación del componente en la arquitectura multicapa de Pulpox V5, qué módulos lo preceden y qué subsistemas dependen de él (*Living Documentation*).
* `* **Restrictions:**` Lista de restricciones arquitectónicas estrictas y mandatorias, por ejemplo:
  * **No-Eval:** Cero evaluación dinámica de código mediante `eval()` o `new Function()`.
  * **Zero-Copy:** Uso obligatorio de `SharedArrayBuffer` para transferencia masiva de texto a Workers.
  * **Inmutabilidad:** Congelamiento de configuraciones y esquemas mediante `Object.freeze()`.
  * **Complejidad Algorítmica:** Acceso y consultas en $O(1)$ o $O(k)$ en hot-paths de escaneo.
  * **Límites de Código:** Máximo 300 líneas por módulo y $v(G) \le 10$ por función.

---

### 3.5. Sección 4: Design / Implementation Details (Diseño y Detalles de Implementación)
Esta sección contiene la especificación de ingeniería profunda y se organiza en 4 subcampos estructurados:

1. `* **Architecture:**` Flujo de datos y secuencia de control paso a paso. Se recomienda describir las transiciones mediante notación de flechas (`Disparador $\to$ Función() $\to$ Estado $\to$ Evento`) o diagramas Mermaid.
2. `* **Data Model:**` Definición formal de tipos, esquemas JSON Schema (Draft-07), interfaces TypeScript/JSDoc o estructuras en memoria (`Map<string, Set<string>>`, `Int32Array`, etc.) con bloques de código completos sin placeholders.
3. `* **API Contracts:**` Firmas exactas de métodos y funciones públicas con sus tipos de entrada y salida (ej. `validateMemorySchema(jsonPayload: object): { valid: boolean, errors: string[] }`).
4. `* **UI/UX:**` Especificación visual si el componente tiene interacción con el usuario (componentes HTML/CSS involucrados, micro-animaciones, colores por severidad, heurística de usabilidad, persistencia en `localStorage`/`sessionStorage`).

---

### 3.6. Sección 5: Acceptance Criteria (Criterios de Aceptación)
* **Formato Obligatorio:** Behavior Driven Development (**BDD**) con sintaxis formal `Given-When-Then`.
* **Cantidad Mínima:** Al menos **2 escenarios** exhaustivos:
  * **Scenario 1:** Flujo principal de éxito (*Happy Path*) con entradas nominales.
  * **Scenario 2:** Caso de fallo, caso borde (*Edge Case*), contención de errores o prueba de rendimiento/límite de memoria.
* **Estructura por Escenario:**
  ```markdown
  * **Scenario 1: [Nombre descriptivo del escenario]**
    * **Given** [Estado inicial del sistema, precondiciones y datos de entrada],
    * **When** [Acción ejecutada o evento disparado],
    * **Then** [Resultado exacto esperado, cambios de estado verificables y eventos emitidos].
  ```

---

### 3.7. Sección 6: Verification Plan (Plan de Verificación)
Detalla la estrategia para certificar que la implementación cumple los contratos:
* **Pruebas Unitarias & Integración:** Nombres de las suites de prueba en Vitest (`tests/contracts/*.test.js`, `tests/unit/*.test.js`).
* **Pruebas de Rendimiento / Benchmarks:** Mediciones de latencia, frame budget y límites de memoria en el heap de V8.
* **Linters y Gobernanza:** Scripts automatizados de verificación (`scripts/analyze-complexity.js`, `scripts/verify-doc-pairs.js`).

---

### 3.8. Sección 7: Security and Privacy (Seguridad y Privacidad)
Directrices de ciberseguridad aplicadas al diseño:
* **Zero-Trust:** Validación estricta de esquemas y tipos en todos los límites de entrada.
* **Aislamiento en Cliente:** Procesamiento 100% local en el navegador; cero telemetría externa o exfiltración de código fuente.
* **Mitigación de Vulnerabilidades:** Prevención de inyección de prototipos (`__proto__`), supresión de XSS mediante `esc()`, sanitización de BOM UTF-8 y contención en Web Workers.

---

### 3.9. Sección 8: Risks and Mitigation (Riesgos y Mitigación)
Identificación proactiva de riesgos técnicos y sus contramedidas:
* **Formato:**
  ```markdown
  * **Risk:** [Descripción del riesgo técnico, de degradación o regresión] -> **Mitigation:** [Acción concreta de diseño, arquitectura o validación que neutraliza el riesgo].
  ```

---

### 3.10. Sección 9: Deliverables & Config as Code (Entregables)
Lista exhaustiva de archivos del proyecto que serán creados, modificados o configurados como parte de este SPEC:
* Rutas relativas canónicas en `src/core/`, `src/motores/`, `specs/`, `tests/` o `scripts/`.
* Pares de documentación `*.proceso.md` e `*.invariante.md` asociados.

---

### 3.11. Sección 10: Definition of Done / DoD (Definición de Terminado)
Lista de verificación con casillas de Markdown (`* [ ]`) que representan las condiciones no negociables para dar por completada la especificación:
* Contratos validados.
* Cobertura de tests unitarios al 100% en Vitest.
* Pares `.proceso.md` e `.invariante.md` creados o sincronizados.
* Verificación de complejidad ciclomática $v(G) \le 10$ y volumen $\le 300$ líneas.
* Cero dependencias externas no autorizadas.

---

## 4. PLANTILLA MAESTRA DE REFERENCIA (TEMPLATE)

Utilice la siguiente plantilla como punto de partida exacto para redactar nuevas especificaciones:

````markdown
# SPEC-X.Y.Z: [Título Descriptivo Canónico de la Especificación]

## 1. Objective
[Describir en 1 o 2 párrafos el propósito fundamental del componente, el problema técnico resuelto y las metas cuantitativas de rendimiento (ej. O(k), < 5 MB, <= 12 ms, v(G) <= 10).]

## 2. Scope
### 2.1. Included
* [Entregable funcional 1 con detalles de interfaces o contratos].
* [Entregable funcional 2 con algoritmos o transformaciones de datos].
* [Eventos o integración con subsistemas].

### 2.2. Not Included (Out of Scope)
* [Elemento no incluido 1] (delegado a [SPEC-A.B.C]).
* [Elemento no incluido 2] (delegado a [Módulo N]).

## 3. Context and Restrictions
* **Context:** [Explicar la posición del componente dentro de la arquitectura de capas de Pulpox V5 y su relación con módulos adyacentes].
* **Restrictions:**
  * [Restricción 1: ej. No-Eval, Zero-Copy SAB, Inmutabilidad Object.freeze].
  * [Restricción 2: ej. Rendimiento O(1) en hot-path, límite de 300 líneas por módulo].

## 4. Design (Implementation Details)
* **Architecture:** [Describir el flujo de control y las transiciones de estado: Paso A -> Paso B -> Emisión de evento].
* **Data Model:**
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["propiedadA"],
  "properties": {
    "propiedadA": { "type": "string" }
  }
}
```
* **API Contracts:** `funcionPrincipal(parametroA: TipoA, parametroB: TipoB): RetornoTipo`.
* **UI/UX:** [Detalles visuales, componentes de interfaz, micro-animaciones o persistencia en sessionStorage/localStorage si aplica].

## 5. Acceptance Criteria
* **Scenario 1: [Nombre del escenario Happy Path]**
  * **Given** [Precondición o contexto inicial],
  * **When** [Acción ejecutada o disparador],
  * **Then** [Resultado esperado verificable].
* **Scenario 2: [Nombre del escenario Edge Case / Error Handling]**
  * **Given** [Condición de fallo o límite de recursos],
  * **When** [Acción ejecutada],
  * **Then** [Comportamiento defensivo y contención esperada].

## 6. Verification Plan
* Pruebas unitarias en Vitest en `tests/unit/[nombre].test.js`.
* Pruebas de rendimiento validando consumo de memoria y tiempo de ejecución.
* Verificación estática con `scripts/analyze-complexity.js`.

## 7. Security and Privacy
* Zero-Trust: Validación estricta de entradas y sanitización defensiva.
* Procesamiento 100% en cliente sin transferencias de datos a servidores externos.

## 8. Risks and Mitigation
* **Risk:** [Descripción de riesgo técnico o de regresión] -> **Mitigation:** [Estrategia de mitigación].

## 9. Deliverables & Config as Code
* Módulo principal en `src/[capa]/[Componente].js`.
* Pares de documentación `src/[capa]/[Componente].proceso.md` e `invariante.md`.
* Suite de pruebas en `tests/[directorio]/[Componente].test.js`.

## 10. Definition of Done (DoD)
* [ ] Código implementado cumpliendo estrictamente con el diseño.
* [ ] Tests unitarios en Vitest aprobados al 100%.
* [ ] Pares `.proceso.md` e `.invariante.md` actualizados y validados.
* [ ] Complejidad ciclomática v(G) <= 10 y límite de 300 líneas auditado.
* [ ] Registro del SPEC incorporado en `specs/SPEC-INDEX.md`.
````

---

## 5. REGLAS DE INTEGRACIÓN Y SINCRONIZACIÓN CON `SPEC-INDEX.md`

Toda nueva especificación creada debe registrarse de manera obligatoria en [`specs/SPEC-INDEX.md`](file:///d:/UDB/PRACTICAS/EMPRENDE%20HOY/ENTREGABLES/2.MOTOR/pulpox-v5/specs/SPEC-INDEX.md):

### 5.1. Actualización del Grafo Mermaid
1. Ubicar el subgrafo correspondiente al módulo (`M1` a `M7`).
2. Declarar el nodo: `SXYZ[SPEC-X.Y.Z: Título Corto]`.
3. Trazar las flechas de dependencia de entrada y salida: `S_PREVIO --> SXYZ` y `SXYZ --> S_POSTERIOR`.

### 5.2. Actualización de la Tabla Maestra (§ 2)
Agregar una fila en la tabla de especificaciones con las 7 columnas completas:
```markdown
| [`SPEC-X.Y.Z`](file:///d:/UDB/PRACTICAS/EMPRENDE%20HOY/ENTREGABLES/2.MOTOR/pulpox-v5/specs/SPEC-X.Y.Z-nombre.md) | Título Corto | Objetivo Conciso | Dependencias Previas (Entrada) | Módulos Dependientes (Salida) | Componentes de Código | Nivel de Construcción |
```

### 5.3. Actualización del Desglose Modular (§ 3)
Agregar la subsección bajo el módulo respectivo con los siguientes 4 metadatos:
* `#### [SPEC-X.Y.Z] Título Completo de la Especificación`
* `* **Nivel de Construcción:**` (**Planificada (0%)**, **Construida Parcialmente (X%)**, o **Construida (100%)**).
* `* **Objetivo:**` Síntesis de 1 párrafo.
* `* **Dependencias:**` Lista de códigos SPEC previos o `Ninguna`.
* `* **Dependientes Directos:**` Lista de códigos SPEC que consumen este componente.

---

## 6. CHECKLIST DE CALIDAD ANTES DE PUBLICAR UN SPEC

Antes de considerar una SPEC como lista y aprobada, valide la siguiente lista de control de calidad:

- [x] **Nomenclatura Canónica:** El archivo se encuentra en `specs/` y sigue el patrón `SPEC-X.Y.Z-slug-kebab-case.md`.
- [x] **Completitud Estructural:** Contiene las 10 secciones obligatorias sin omitir ninguna.
- [x] **Sin Placeholders:** Cero comentarios o esqueletos incompletos (`// ...`, `TODO`, `/* resto del código */`).
- [x] **Métricas Explícitas:** Incluye métricas de rendimiento y complejidad matemática ($O(1)$, $O(k)$, latencias, MB).
- [x] **Criterios BDD Formales:** Todos los escenarios en la Sección 5 usan `Given-When-Then`.
- [x] **Trazabilidad de Delegación:** Todo ítem marcado en *Not Included* delega explícitamente a otro SPEC.
- [x] **Contratos Completos:** Las interfaces y esquemas en la Sección 4 son válidos y ejecutables.
- [x] **Sincronización en Índice:** El SPEC está vinculado en el grafo, tabla y desglose de `SPEC-INDEX.md`.
