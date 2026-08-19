using System.Reflection;
using System.Text;
using InvoiceApi.Auth;
using InvoiceApi.Data;
using InvoiceApi.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Validar al arranque evita levantar una API con la seguridad inservible: si
// falta la clave de firma o es mas corta de lo que exige HMAC-SHA256, el proceso
// no llega a aceptar peticiones.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["Key"] ?? string.Empty)),
            // Sin tolerancia de reloj: un token expirado se rechaza al segundo.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Todo endpoint exige autenticacion salvo que se marque [AllowAnonymous].
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Build);

builder.Services.AddProblemDetails(options =>
{
    // El traceId es lo que permite cruzar una respuesta de error con su entrada
    // en el log del servidor.
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Invoice API",
        Version = "v1",
        Description = "API REST para el registro y la consulta de facturas. "
                    + "El acceso requiere un token obtenido en POST /auth/token."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Pegue unicamente el token devuelto por POST /auth/token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [securityScheme] = Array.Empty<string>()
    });

    // Las descripciones que se ven en Swagger salen de los comentarios XML.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Invoice API v1"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Punto de entrada expuesto para las pruebas de integracion.</summary>
public partial class Program;
