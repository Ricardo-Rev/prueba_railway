using System;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;
using Lexico.Application.Contracts;
using Lexico.Application.Services;
using Lexico.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1) Puerto dinámico (Railway inyecta PORT). Local: 8080.
//    También soportamos ASPNETCORE_URLS si viniera seteado.
// ============================================================================
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ============================================================================
// 2) Swagger / OpenAPI
//    - En Desarrollo: siempre ON.
//    - En Producción: controlable con var. de entorno ENABLE_SWAGGER=true
// ============================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================================
// 3) CORS
//    lee Cors:AllowedOrigins (array) de appsettings/variables.
//    - Si viene vacío o contiene "*": AllowAnyOrigin
//    - Si no: WithOrigins(…)
//    También permitimos configurar por variable CORS__ALLOWEDORIGINS="https://a,https://b"
// ============================================================================
string[] allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

// Soporte por variable simple "CORS__ALLOWEDORIGINS" separada por comas
var envCors = Environment.GetEnvironmentVariable("CORS__ALLOWEDORIGINS");
if (!string.IsNullOrWhiteSpace(envCors))
{
    var parts = envCors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length > 0) allowedOrigins = parts;
}

builder.Services.AddCors(o =>
{
    o.AddPolicy("Default", p =>
    {
        if (allowedOrigins.Length == 0 || Array.Exists(allowedOrigins, x => x == "*"))
        {
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            p.WithOrigins(allowedOrigins)
             .AllowAnyHeader()
             .AllowAnyMethod();
        }
    });
});

// ============================================================================
// 4) Límites de subida (multipart y request body en general)
//    Configurable por Uploads:MaxMultipartBodyLength (bytes) o UPLOADS__MAXMULTIPARTBODYLENGTH
//    Default: 10 MB
// ============================================================================
long defaultMax = 10_000_000; // 10 MB
long? maxMultipart = builder.Configuration.GetValue<long?>("Uploads:MaxMultipartBodyLength");

var envMax = Environment.GetEnvironmentVariable("UPLOADS__MAXMULTIPARTBODYLENGTH");
if (long.TryParse(envMax, out var envMaxParsed))
    maxMultipart = envMaxParsed;

var effectiveMax = maxMultipart ?? defaultMax;

builder.Services.Configure<FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = effectiveMax;
});

// Alinear límite de Kestrel (para PUT/POST no-multipart)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = effectiveMax;
});

// ============================================================================
// 5) DI / Servicios de la solución (Dapper + Repos + Servicios)
//    Nota: si tu DapperConnectionFactory lee connection string de IConfiguration,
//    asegúrate de tener ConnectionStrings:DefaultConnection en Railway.
// ============================================================================
builder.Services.AddSingleton<DapperConnectionFactory>();

builder.Services.AddScoped<IIdiomaRepository, IdiomaRepository>();
builder.Services.AddScoped<IDocumentoRepository, DocumentoRepository>();
builder.Services.AddScoped<IAnalisisRepository, AnalisisRepository>();
builder.Services.AddScoped<ILogProcesamientoRepository, LogProcesamientoRepository>();
builder.Services.AddScoped<IConfiguracionAnalisisRepository, ConfiguracionAnalisisRepository>();

builder.Services.AddScoped<AnalysisService>();

builder.Services.AddControllers();

// ============================================================================
// 6) Build
// ============================================================================
var app = builder.Build();

// ============================================================================
// 7) Forwarded Headers (Railway está detrás de proxy / load balancer)
//    Con esto respetas esquema/host reales para redirects, links, etc.
// ============================================================================
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// ============================================================================
// 8) Swagger
//    - Dev: siempre
//    - Prod: si ENABLE_SWAGGER=true
// ============================================================================
var enableSwaggerInProd = string.Equals(
    Environment.GetEnvironmentVariable("ENABLE_SWAGGER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (app.Environment.IsDevelopment() || enableSwaggerInProd)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================================================
// 9) CORS
// ============================================================================
app.UseCors("Default");

// ============================================================================
// 10) HTTPS redirection
//     En Railway suele funcionar bien porque X-Forwarded-Proto viene seteado;
//     si te redirige de forma indeseada, puedes comentar esta línea.
// ============================================================================
app.UseHttpsRedirection();

// ============================================================================
// 11) Rutas / Controllers
// ============================================================================
app.MapControllers();

// ============================================================================
// 12) Health mínimo (por si no tienes controlador dedicado)
// ============================================================================
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    env = app.Environment.EnvironmentName,
    port,
    time = DateTime.UtcNow
}));

// ============================================================================
// 13) Run
// ============================================================================
app.Run();
