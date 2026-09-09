# SPEC-4.1.1: Gestión de Identidad, Registro de Usuarios y Emisión de Credenciales JWT con ASP.NET Core Identity

## 1. Objective
Implementar el subsistema de gestión de identidades y autenticación para AutoGestion S.A. basado en ASP.NET Core Identity y tokens JWT (JSON Web Tokens), habilitando los endpoints públicos `/register` y `/login` con hashing criptográfico seguro (PBKDF2/SHA-256) y emisión de credenciales firmadas digitalmente con tiempo de expiración configurable de 60 minutos.

## 2. Scope
### 2.1. Included
* Configuración de servicios de `Microsoft.AspNetCore.Identity.EntityFrameworkCore` en SQL Server.
* Exposición del endpoint `POST /register` con validación de contraseña segura y creación de usuario.
* Exposición del endpoint `POST /login` con comprobación de credenciales y generación de Bearer Token JWT.
* Definición del esquema de Claims de identidad (Email, NameIdentifier, Jti).
* Manejo de errores de credenciales inválidas retornando HTTP 400 o 401.

### 2.2. Not Included (Out of Scope)
* Aplicación del filtro `[Authorize]` a los endpoints de negocio de Vehículos (delegado a [SPEC-4.2.1](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-4.2.1-identity-autorizacion-filtro-vehiculos.md)).
* Enrutamiento de estos endpoints en Ocelot (cubierto en [SPEC-1.1.2](file:///d:/UDB/CICLO-10-2026/DES/LAB/Desafio2/specs/SPEC-1.1.2-ocelot-enrutamiento-seguridad-vehiculos.md)).
* Recuperación de contraseña por correo electrónico o autenticación de doble factor (MFA).

## 3. Context and Restrictions
* **Context:** Módulo de Seguridad y Autenticación (Guía 10). Resuelve la vulnerabilidad crítica de acceso anónimo generalizado en el sistema, permitiendo registrar cuentas y generar credenciales de sesión.
* **Restrictions:**
  * No se deben almacenar contraseñas en texto plano bajo ninguna circunstancia.
  * La clave secreta de firma simétrica para los tokens JWT debe tener al menos 256 bits (32 caracteres).

## 4. Design (Implementation Details)
* **Architecture:**
  * **Registro:** `Cliente POST /register` $\to$ `UserManager<IdentityUser>.CreateAsync(user, pass)` $\to$ `Persistencia en Tablas AspNetUsers en SQL` $\to$ Retorna HTTP 200/201.
  * **Login:** `Cliente POST /login` $\to$ `SignInManager / CheckPasswordAsync` $\to$ `Generación de Claims + JwtSecurityTokenHandler` $\to$ Retorna `{ "token": "...", "expiration": "..." }`.
* **Data Model (DTOs de Autenticación):**
```csharp
public class RegisterDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener mínimo 6 caracteres")]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public string Email { get; set; } = string.Empty;
}
```
* **API Contracts:**
  * `POST /api/auth/register` (o `/register`):
    * Entrada: `RegisterDto`.
    * Salida: HTTP 200 OK (`"Usuario registrado exitosamente"`) o HTTP 400 BadRequest con lista de errores de validación.
  * `POST /api/auth/login` (o `/login`):
    * Entrada: `LoginDto`.
    * Salida: HTTP 200 OK con `AuthResponseDto` o HTTP 401 Unauthorized (`"Credenciales inválidas"`).
* **UI/UX:** En Swagger UI y Postman, los endpoints de autenticación aparecen claramente etiquetados en la sección "Auth / Seguridad", permitiendo al evaluador registrarse y autenticarse con un clic.

## 5. Acceptance Criteria
* **Scenario 1: Registro Exitoso de Nuevo Usuario**
  * **Given** un nuevo correo `"operador@autogestion.com"` no registrado previamente en el sistema,
  * **When** se envía una solicitud `POST /register` con contraseña `"PasswordSeguro123!"`,
  * **Then** Identity procesa el registro, aplica el hash de la contraseña en `AspNetUsers` y responde con código HTTP 200 OK.
* **Scenario 2: Autenticación Exitosa y Generación de Token JWT**
  * **Given** el usuario `"operador@autogestion.com"` previamente registrado,
  * **When** envía sus credenciales correctas a `POST /login`,
  * **Then** el sistema valida la contraseña, emite un token JWT firmado y responde con código HTTP 200 OK adjuntando el token y su fecha de expiración.
* **Scenario 3: Rechazo de Autenticación con Contraseña Incorrecta**
  * **Given** un usuario registrado en el sistema,
  * **When** intenta autenticarse mediante `POST /login` con una contraseña errónea,
  * **Then** el servicio rechaza la petición retornando HTTP 401 Unauthorized sin emitir ningún token.

## 6. Verification Plan
* Prueba en Postman:
  1. Enviar `POST /register` con credenciales de prueba.
  2. Enviar `POST /login` y copiar el string del token generado.
  3. Decodificar el token en `jwt.io` comprobando que contenga los claims `sub`, `email`, y algoritmo `HS256`.

## 7. Security and Privacy
* Políticas de contraseñas de Identity: exige longitud mínima y complejidad.
* Almacenamiento criptográfico irreversible de contraseñas con sal aleatoria.
* Clave secreta JWT externa en variables de entorno o archivo de configuración seguro.

## 8. Risks and Mitigation
* **Risk:** Emisión de tokens con tiempo de vida ilimitado o muy extenso. -> **Mitigation:** Fijar expiración estricta de 60 minutos en la configuración del token generator.

## 9. Deliverables & Config as Code
* Controlador `AuthController.cs` con endpoints `/register` y `/login`.
* Configuración de Identity y `JwtBearer` en `Program.cs`.
* Migraciones de Entity Framework Core para tablas de Identity en SQL Server.

## 10. Definition of Done (DoD)
* [ ] Endpoint `/register` funcional y persistiendo usuarios en base de datos.
* [ ] Endpoint `/login` funcional y emitiendo tokens JWT válidos.
* [ ] Rechazo con HTTP 401 probado ante contraseñas incorrectas.
* [ ] Evidencia fotográfica de login y token para el informe PDF.
