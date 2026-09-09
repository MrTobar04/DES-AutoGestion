# Universidad Don Bosco
## Facultad de Ingeniería
### DESARROLLO DE SOFTWARE EMPRESARIAL - DSE104

---

## Desafío 2

La empresa **"AutoGestion S.A."** tiene varias APIs (Productos, Libros y Vehículos) pero tienen estos problemas:
- Cada quien las consulta por separado y se saturan.
- Las consultas de listados son muy lentas.
- No hay pruebas para saber si el código sigue funcionando.
- Cualquier persona puede entrar a ver los datos sin contraseña.

---

### Lo que pide el jefe

#### a) Poner una sola puerta de entrada (Guía 7 - Ocelot)
> "Hagan que todas las peticiones pasen por un solo lugar (API Gateway). Quiero que cuando alguien pida `/productos` o `/libros`, el sistema lo redirija a la API correcta. Además, pongan un límite de solo 10 peticiones por minuto para que no nos ataquen."

#### b) Hacer que las consultas sean más rápidas (Guía 8 - Redis)
> "Los listados de productos tardan mucho. Usen Redis para guardar la lista en caché por 5 minutos. Cuando alguien agregue, edite o elimine un producto, la caché se debe borrar para que no se vean datos viejos."

#### c) Escribir pruebas para que no fallen (Guía 9 - xUnit)
> "Tenemos un registro de Personas con validaciones (el DUI debe tener formato `00000000-0`, el nombre es obligatorio). Escriban pruebas unitarias que comprueben que el sistema devuelve error (`BadRequest`) cuando los datos están mal, y que guarda bien cuando están correctos."

#### d) Ponerle contraseña a los datos (Guía 10 - Identity)
> "Solo la gente registrada puede ver los vehículos. Configuren Identity con los endpoints `/register` y `/login`. Pongan el atributo `[Authorize]` en los controladores para que, si no ha iniciado sesión, devuelva error 401."

#### e) Que todo corra junto en contenedores (Docker)
> "Queremos que el sistema funcione igual en cualquier computadora. Hagan un `docker-compose.yml` que levante la API, la base de datos SQL y el servidor de Redis al mismo tiempo."

---

### 3. ¿Qué tienen que entregar?

1. Repositorio en Git con toda la solución.
2. Un PDF (máximo 10 hojas) con:
   - Portada (nombre y carnet).
   - Una captura de pantalla por cada punto (a, b, c, d, e) mostrando que funciona.

---

### 4. Mensajes de commit obligatorios

```bash
git commit -m "feat(gateway): configurar Ocelot con rate limit"
git commit -m "feat(cache): agregar Redis y caché de 5 minutos"
git commit -m "feat(tests): crear pruebas unitarias para Personas"
git commit -m "feat(security): configurar Identity y proteger endpoints"
git commit -m "feat(docker): crear docker-compose con API, SQL y Redis"
```

---

### Notas y recomendaciones

- Prueben cada API por separado antes de meter el Gateway.
- No olviden borrar la caché en los métodos `POST`, `PUT` y `DELETE`.
- Usen base de datos en memoria (`InMemory`) para las pruebas, no la real.
- Revisen bien las guías 7, 8, 9 y 10, ahí viene todo paso a paso.

**¡Éxito en su examen!**

---

## RÚBRICA DE EVALUACIÓN

| Criterio | Destacado (9-10) | Competente (6-8) | Básico (1-5) | No logrado (0) |
| :--- | :--- | :--- | :--- | :--- |
| **a) Ocelot** | El Gateway enruta TODOS los endpoints y el límite de 10 peticiones por minuto funciona bien en un 100%. | El Gateway enruta TODOS los endpoints PERO el límite de peticiones no funciona o está mal configurado (80%). | El Gateway enruta SOLO ALGUNOS endpoints (50%). | No funciona o no está hecho (0%). |
| **b) Redis** | Guarda la lista en caché, la sirve desde allí y la borra al hacer POST, PUT o DELETE en un 100%. | Guarda en caché y sirve desde allí PERO no borra la caché al actualizar (se ven datos viejos) (80%). | Guarda en caché PERO nunca la usa (siempre va a la BD) (50% o menos). | No está implementado (0%). |
| **c) Pruebas** | 5 o más pruebas pasando (cubren DUI inválido, nombre vacío, ID inexistente y guardado exitoso) en un 100%. | 3 o 4 pruebas pasando (80%). | 1 o 2 pruebas pasando (50% o menos). | Ninguna prueba o todas fallan (0%). |
| **d) Identity** | `/register` y `/login` funcionan, y `[Authorize]` protege TODOS los endpoints (devuelve 401 sin sesión) en un 100%. | Login y registro funcionan PERO algunos endpoints quedan sin protección (80%). | Login y registro funcionan PERO no protege ningún endpoint (50% o menos). | No está configurado (0%). |
| **e) Docker** | `docker-compose.yml` levanta los 3 servicios (API, SQL y Redis) y la API se conecta a todos en un 100%. | Los contenedores se levantan PERO la API no se conecta a SQL o a Redis (80%). | Solo levanta la API, PERO SQL o Redis fallan (50% o menos). | No está contenerizado (0%). |
