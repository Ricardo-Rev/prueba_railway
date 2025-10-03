using Microsoft.AspNetCore.Http.Features;
using Lexico.Application.Contracts;
using Lexico.Application.Services;
using Lexico.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Swagger
// -----------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// -----------------------------------------------------------------------------
// CORS (lee AllowedOrigins de appsettings; si viene "*" o vacío => AllowAnyOrigin)
// -----------------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o =>
{
    o.AddPolicy("Default", p =>
    {
        if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

// -----------------------------------------------------------------------------
// Límite global de subida multipart (por si subes TXT grandes)
// -----------------------------------------------------------------------------
var maxMultipart = builder.Configuration.GetValue<long?>("Uploads:MaxMultipartBodyLength") ?? 10_000_000; // 10 MB
builder.Services.Configure<FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = maxMultipart;
});

// -----------------------------------------------------------------------------
// Servicios (conexión Dapper + repos + servicio de análisis)
// -----------------------------------------------------------------------------
builder.Services.AddSingleton<DapperConnectionFactory>();

// Repositorios alineados al esquema real de BD
builder.Services.AddScoped<IIdiomaRepository, IdiomaRepository>();
builder.Services.AddScoped<IDocumentoRepository, DocumentoRepository>();
builder.Services.AddScoped<IAnalisisRepository, AnalisisRepository>();
builder.Services.AddScoped<ILogProcesamientoRepository, LogProcesamientoRepository>();
builder.Services.AddScoped<IConfiguracionAnalisisRepository, ConfiguracionAnalisisRepository>();

// Servicio orquestador del análisis léxico
builder.Services.AddScoped<AnalysisService>();

builder.Services.AddControllers();

var app = builder.Build();

// -----------------------------------------------------------------------------
// Middlewares
// -----------------------------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Default");

// Escuchar el puerto que provee Railway; para local usa 8080
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.MapControllers();

app.Run();
