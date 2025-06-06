using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using csharpapi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllers();
builder.Services.AddSingleton<ControlConexion>();
builder.Services.AddSingleton<TokenService>();

// ✅ CORS ajustado para desarrollo y producción
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontendReact", policy =>
    {
        policy.WithOrigins(
            // Dev local
            "http://frontreact.runasp.net" // Producción (ajustar si el dominio cambia) // Producción - cámbialo por el real
        )
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 🔹 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Api Genérica C#",
        Version = "v1",
        Description = "API de prueba con ASP.NET Core y Swagger",
        Contact = new OpenApiContact
        {
            Name = "Soporte API",
            Email = "soporte@miapi.com",
            Url = new Uri("https://miapi.com/contacto")
        }
    });
});

var app = builder.Build();

// 🔥 Swagger en todos los entornos (opcional)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Api Genérica C#");
    c.RoutePrefix = string.Empty;
});


app.UseCors("PermitirFrontendReact");
app.UseSession();
app.UseAuthorization();
app.MapControllers();
app.Run();