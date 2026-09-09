using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// Middleware de identificación perimetral de cliente para Rate Limiting
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("X-Client-Id"))
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        var clientId = string.IsNullOrWhiteSpace(remoteIp) ? "local-client" : remoteIp;
        context.Request.Headers["X-Client-Id"] = clientId;
    }
    await next();
});

await app.UseOcelot();

app.Run();
