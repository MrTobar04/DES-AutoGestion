using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Vehículos",
        Version = "v1",
        Description = "Microservicio de Flotilla e Inventario de Vehículos con soporte de autenticación (/register y /login) para Ocelot API Gateway"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese 'Bearer' [espacio] y luego su token JWT en el campo."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Vehículos v1");
    });
}

// Middleware de seguridad Bearer para aislamiento de Vehículos (SPEC-1.3.1 y SPEC-1.1.2)
app.Use(async (context, next) =>
{
    // Permitir acceso libre a Swagger y Endpoints de Autenticación
    if (context.Request.Path.StartsWithSegments("/swagger") || context.Request.Path.StartsWithSegments("/api/auth"))
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers["Authorization"].ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { mensaje = "Acceso no autorizado. Se requiere cabecera 'Authorization: Bearer <token>' para acceder al módulo de Vehículos." });
        return;
    }

    var token = authHeader["Bearer ".Length..].Trim();
    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { mensaje = "Token Bearer inválido o vacío." });
        return;
    }

    await next();
});

// In-memory data store for autonomous testing
var vehiculos = new List<Vehiculo>
{
    new() { Id = 1, Marca = "Toyota", Modelo = "Corolla LE", Anio = 2022, Placa = "P123-456", Precio = 18500.00m },
    new() { Id = 2, Marca = "Nissan", Modelo = "Sentra Advance", Anio = 2023, Placa = "P654-321", Precio = 21000.00m },
    new() { Id = 3, Marca = "Honda", Modelo = "Civic Touring", Anio = 2024, Placa = "P789-012", Precio = 26500.00m }
};

var nextId = vehiculos.Max(v => v.Id) + 1;
var syncLock = new object();

app.MapGet("/api/vehiculos", () =>
{
    lock (syncLock)
    {
        return Results.Ok(vehiculos.ToList());
    }
})
.WithName("ObtenerVehiculos")
.WithSummary("Obtiene el inventario completo de vehículos");

app.MapGet("/api/vehiculos/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var vehiculo = vehiculos.FirstOrDefault(v => v.Id == id);
        return vehiculo is not null ? Results.Ok(vehiculo) : Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado" });
    }
})
.WithName("ObtenerVehiculoPorId")
.WithSummary("Obtiene un vehículo por su identificador");

app.MapPost("/api/vehiculos", (Vehiculo nuevoVehiculo) =>
{
    lock (syncLock)
    {
        nuevoVehiculo.Id = nextId++;
        vehiculos.Add(nuevoVehiculo);
        return Results.Created($"/api/vehiculos/{nuevoVehiculo.Id}", nuevoVehiculo);
    }
})
.WithName("CrearVehiculo")
.WithSummary("Registra un nuevo vehículo en el inventario");

app.MapPut("/api/vehiculos/{id:int}", (int id, Vehiculo vehiculoActualizado) =>
{
    lock (syncLock)
    {
        var index = vehiculos.FindIndex(v => v.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para actualización" });
        }

        vehiculoActualizado.Id = id;
        vehiculos[index] = vehiculoActualizado;
        return Results.Ok(vehiculoActualizado);
    }
})
.WithName("ActualizarVehiculo")
.WithSummary("Actualiza los datos de un vehículo existente");

app.MapDelete("/api/vehiculos/{id:int}", (int id) =>
{
    lock (syncLock)
    {
        var index = vehiculos.FindIndex(v => v.Id == id);
        if (index == -1)
        {
            return Results.NotFound(new { mensaje = $"Vehículo con Id {id} no encontrado para eliminación" });
        }

        var eliminado = vehiculos[index];
        vehiculos.RemoveAt(index);
        return Results.Ok(eliminado);
    }
})
.WithName("EliminarVehiculo")
.WithSummary("Elimina un vehículo del inventario");

app.MapPost("/api/auth/register", (RegisterDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nombre))
    {
        return Results.BadRequest(new { mensaje = "El nombre es obligatorio." });
    }

    if (string.IsNullOrWhiteSpace(dto.Dui) || !Regex.IsMatch(dto.Dui.Trim(), @"^\d{8}-\d$"))
    {
        return Results.BadRequest(new { mensaje = "El formato del DUI debe ser 00000000-0." });
    }

    if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
    {
        return Results.BadRequest(new { mensaje = "El correo electrónico es obligatorio y debe tener un formato válido." });
    }

    if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
    {
        return Results.BadRequest(new { mensaje = "La contraseña debe tener mínimo 6 caracteres." });
    }

    return Results.Ok(new
    {
        mensaje = "Usuario registrado exitosamente",
        nombre = dto.Nombre.Trim(),
        dui = dto.Dui.Trim(),
        email = dto.Email.Trim()
    });
})
.WithName("RegistroUsuario")
.WithSummary("Registra un nuevo usuario en el sistema con Nombre, DUI, Email y Contraseña");

app.MapPost("/api/auth/login", (LoginDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
    {
        return Results.BadRequest(new { mensaje = "El correo electrónico y la contraseña son obligatorios." });
    }

    // Generar un token Bearer JWT de prueba de 60 min de vigencia (SPEC-1.1.2 / SPEC-4.1.1)
    var tokenMock = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZW1haWwiOiJ7ZHRvLkVtYWlsfSIsImlhdCI6MTUxNjIzOTAyMn0.dummy_signature_for_{dto.Email.Replace("@", "_")}";
    var expiration = DateTime.UtcNow.AddMinutes(60);

    return Results.Ok(new AuthResponseDto
    {
        Token = tokenMock,
        Expiration = expiration,
        Email = dto.Email
    });
})
.WithName("LoginUsuario")
.WithSummary("Inicia sesión y genera credencial JWT Bearer");

app.Run();

public class Vehiculo
{
    public int Id { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Anio { get; set; }
    public string Placa { get; set; } = string.Empty;
    public decimal Precio { get; set; }
}

public class RegisterDto
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DUI es obligatorio")]
    [RegularExpression(@"^\d{8}-\d$", ErrorMessage = "El formato del DUI debe ser 00000000-0")]
    public string Dui { get; set; } = string.Empty;

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
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
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
