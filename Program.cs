using Filskane.DAL;
using Filskane.Services;
using MaxRev.Gdal.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OSGeo.GDAL;
using System.Runtime.InteropServices;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var nativePath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
nativePath = Path.GetFullPath(nativePath);
Console.WriteLine("Native path: " + nativePath);
Console.WriteLine("Istnieje: " + Directory.Exists(nativePath));
Environment.SetEnvironmentVariable("PATH", nativePath + ";" + Environment.GetEnvironmentVariable("PATH"));

Console.WriteLine("BaseDirectory: " + AppContext.BaseDirectory);
Console.WriteLine("CurrentDirectory: " + Directory.GetCurrentDirectory());

var dlls = Directory.GetFiles(AppContext.BaseDirectory, "gdal_wrap.dll", SearchOption.AllDirectories);
Console.WriteLine("gdal_wrap.dll znaleziony w: " + (dlls.Length > 0 ? dlls[0] : "BRAK!"));

NativeLibrary.SetDllImportResolver(
    typeof(OSGeo.GDAL.Gdal).Assembly,
    (libraryName, assembly, searchPath) =>
    {
        var nativePath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        var fullPath = Path.Combine(nativePath, libraryName + ".dll");
        Console.WriteLine($"Szukam: {fullPath}, istnieje: {File.Exists(fullPath)}");
        if (File.Exists(fullPath))
            return NativeLibrary.Load(fullPath);
        return IntPtr.Zero;
    }
);

GdalBase.ConfigureAll();
Gdal.AllRegister();

// Podstawowe us³ugi frameworka ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Konfiguracja generatora dokumentacji Swagger (OpenAPI)
// Uwzglêdniono definicjê bezpieczeñstwa dla tokenów JWT (Bearer)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Filskane API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Autoryzacja JWT przy u¿yciu schematu Bearer. Wpisz: 'Bearer {twój_token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
    {
        new OpenApiSecurityScheme {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        new string[] { }
    }});
});

// --- Warstwa Dostêpu do Danych (DAL) ---
// Rejestracja klas dostêpowych w cyklu ¿ycia Scoped (jedna instancja na ¿¹danie HTTP)
builder.Services.AddScoped<AuthDAL>();
builder.Services.AddScoped<FarmDAL>();
builder.Services.AddScoped<FieldDAL>();
builder.Services.AddScoped<ScanDAL>();
builder.Services.AddScoped<SettingsDAL>();

// --- Warstwa Logiki Biznesowej (Services) ---
// Rejestracja serwisów realizuj¹cych logikê domeny systemu Filskane
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FarmService>();
builder.Services.AddScoped<FieldService>();
builder.Services.AddScoped<FieldsListService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<SettingsService>();

// --- Infrastruktura i Integracje ---
// Serwis haszuj¹cy has³a (algorytm Argon2) - zarejestrowany jako Singleton (bezstanowy)
builder.Services.AddSingleton<IPasswordHasherService, Argon2PasswordHasherService>();

// Serwis powiadomieñ e-mail
builder.Services.AddScoped<EmailService>();

builder.Services.AddHttpClient();

// Integracja z interpreterem jêzyka Python (Singleton - inicjalizacja silnika raz na start aplikacji)
builder.Services.AddSingleton<PythonService>();

// Konfiguracja mechanizmu uwierzytelniania JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,             // Weryfikacja wystawcy tokena
            ValidateAudience = false,          // Wy³¹czono weryfikacjê odbiorcy (architektura monolityczna)
            ValidateLifetime = true,           // Sprawdzanie daty wa¿noœci tokena
            ValidateIssuerSigningKey = true,   // Weryfikacja podpisu cyfrowego
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Konfiguracja polityki CORS (Cross-Origin Resource Sharing)
// Umo¿liwia komunikacjê z aplikacj¹ klienck¹ (SPA)
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Obs³uga plików statycznych (wymagane dla hostowania aplikacji frontendowej)
app.UseDefaultFiles();
app.UseStaticFiles();

// W³¹czenie interfejsu Swagger UI w œrodowisku deweloperskim
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware obs³uguj¹cy CORS (musi znajdowaæ siê przed uwierzytelnianiem)
app.UseCors("FrontendPolicy");

// Middleware uwierzytelniania (weryfikacja to¿samoœci) i autoryzacji (weryfikacja uprawnieñ)
app.UseAuthentication();
app.UseAuthorization();

// Mapowanie tras kontrolerów API
app.MapControllers();

app.Run();