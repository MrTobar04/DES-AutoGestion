# Pruebas en Postman con Docker Compose

La guía completa con todos los endpoints, DTOs, flujos de autenticación JWT, Rate Limiting y comandos de Docker ha sido generada en:
👉 [`docs/POSTMAN_PRUEBAS_DOCKER.md`](file:///c:/Users/Inspiron/source/repos/DES-AutoGestion/docs/POSTMAN_PRUEBAS_DOCKER.md)

---

## Resumen de Puertos y URLs en Docker

| Componente | URL Base / Puerto | Auth JWT |
| :--- | :--- | :---: |
| **API Gateway (Ocelot)** | `http://localhost:5000` | Según ruta |
| **API Productos (Directo)** | `http://localhost:5001` | No |
| **API Libros (Directo)** | `http://localhost:5002` | No |
| **API Vehículos + Identity (Directo)** | `http://localhost:5003` | Sí |

---

## Flujo Rápido de Prueba

1. **Registrar Usuario**: `POST http://localhost:5000/register`
   ```json
   {
     "nombre": "Juan Pérez",
     "dui": "01234567-9",
     "email": "juan.perez@ejemplo.com",
     "password": "Password123!"
   }
   ```

2. **Login & Obtener Token**: `POST http://localhost:5000/login`
   ```json
   {
     "email": "juan.perez@ejemplo.com",
     "password": "Password123!"
   }
   ```

3. **Consumir Servicio Protegido**: `GET http://localhost:5000/vehiculos`
   - Header: `Authorization: Bearer <TOKEN>`
