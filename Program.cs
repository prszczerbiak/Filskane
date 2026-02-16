using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Scalar.AspNetCore; // Nowoczesne UI
using Filskane.DAL;
using Filskane.Services;


var builder = WebApplication.CreateBuilder(args);

// --- Us³ugi podstawowe ---
builder.Services.AddControllers();

// .NET 10 Native OpenAPI (Zamiast AddSwaggerGen)
builder.Services.AddOpenApi("v1", options =>
{
    // Rejestracja transformera, który doda definicjê JWT (kod klasy poni¿ej)
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// --- Warstwa Dostêpu do Danych (DAL) ---
builder.Services.AddScoped<AuthDAL>();
builder.Services.AddScoped<FarmDAL>();
builder.Services.AddScoped<FieldDAL>();
builder.Services.AddScoped<ScanDAL>();
builder.Services.AddScoped<SettingsDAL>();

// --- Warstwa Logiki Biznesowej (Services) ---
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FarmService>();
builder.Services.AddScoped<FieldService>();
builder.Services.AddScoped<FieldsListService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<SettingsService>();

// --- Infrastruktura ---
builder.Services.AddSingleton<IPasswordHasherService, Argon2PasswordHasherService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PythonService>();

// --- Uwierzytelnianie (Bez zmian, to jest standard) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)) // ! dla null-safety w .NET 10
        };
    });

builder.Services.AddAuthorization();

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// --- Pipeline ---

app.UseDefaultFiles();
app.UseStaticFiles();

// Generowanie pliku openapi.json (Natywne)
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    // Nowoczesne UI od Scalar (zastêpuje starego Swagger UI)
    // Dostêpne pod adresem: /scalar/v1
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Filskane API Documentation");
        options.WithTheme(ScalarTheme.DeepSpace); // Ciemny motyw pasuje do .NETowców
    });
}

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
