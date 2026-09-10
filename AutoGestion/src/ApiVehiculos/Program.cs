using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ApiVehiculos.Controllers;
using ApiVehiculos.Data;
using ApiVehiculos.Models;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

// Configuración de Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoGestion - API Vehículos y Autenticación",
        Version = "v1",
        Description = "Microservicio de Flotilla de Vehículos y Gestión de Identidad (ASP.NET Core Identity + JWT)"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese su token JWT (Swagger antepondrá automáticamente 'Bearer ')."
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

// Configuración de Entity Framework Core con SQL Server y Resiliencia ante Fallos Transitorios
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? "Server=(localdb)\\mssqllocaldb;Database=AutoGestionVehiculosDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        );
    });
});

// Configuración de ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configuración de Autenticación JWT Bearer
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? AuthController.DefaultJwtKey;

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? AuthController.DefaultIssuer;

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? AuthController.DefaultAudience;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configuración de Redis Cache
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("RedisConnection")
    ?? "redis-cache:6379";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

var app = builder.Build();

// Aplicar migraciones automáticamente en inicio de la aplicación
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Aviso: No se pudo ejecutar context.Database.Migrate() automáticamente en el arranque.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Vehículos v1");
    });
}

// Middleware de seguridad Bearer para aislamiento de Vehículos (SPEC-1.3.1, SPEC-1.1.2, SPEC-4.2.1)
app.Use(async (context, next) =>
{
    // Permitir acceso libre a Swagger y Endpoints de Autenticación
    if (context.Request.Path.StartsWithSegments("/swagger") ||
        context.Request.Path.StartsWithSegments("/api/auth"))
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers.Authorization.ToString().Trim();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        authHeader = context.Request.Headers["Authorization"].ToString().Trim();
    }

    string token = string.Empty;
    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        token = authHeader["Bearer ".Length..].Trim();
    }
    else if (authHeader.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
    {
        token = authHeader["Bearer".Length..].Trim();
    }
    else if (!string.IsNullOrEmpty(authHeader) && authHeader.Split('.').Length == 3)
    {
        token = authHeader;
    }

    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { mensaje = "Acceso no autorizado. Se requiere cabecera 'Authorization: Bearer <token>' para acceder al módulo de Vehículos." });
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Mapeo de Controladores (AuthController, VehiculosController, MarcasController, ModelosController)
app.MapControllers();

app.Run();

static void LoadEnvFile()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        var envPath = Path.Combine(current.FullName, ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
            break;
        }
        current = current.Parent;
    }
}
