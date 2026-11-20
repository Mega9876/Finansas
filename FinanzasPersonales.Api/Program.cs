using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection mediante una variable de entorno.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// No aplicar migraciones automáticamente en producción, incluso si se activa la opción.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
    await app.ApplyDevelopmentMigrationsAsync();

// Respuestas genéricas: no enviar trazas, sentencias SQL ni detalles internos al cliente.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Sin redirección HTTPS: HTTP local en Docker. En producción, terminar TLS en un proxy.
app.MapControllers();
app.Run();
