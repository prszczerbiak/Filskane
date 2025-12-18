using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Configuration;
using System.Text;
using WebApplication1.Services;
using WebApplication1.DAL;

var builder = WebApplication.CreateBuilder(args);

// Dodaj dostêp do konfiguracji
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Odczytaj connection string i dodaj do DI
builder.Services.AddSingleton<IPasswordHasherService, Argon2PasswordHasherService>();

builder.Services.AddScoped<AuthDAL>();
builder.Services.AddScoped<FarmDAL>();
builder.Services.AddScoped<FieldDAL>();
builder.Services.AddScoped<ScanDAL>();
builder.Services.AddScoped<SettingsDAL>();

//builder.Services.AddSingleton<DatabaseService>(sp =>
//{
//    var configuration = sp.GetRequiredService<IConfiguration>();
//    var hasher = sp.GetRequiredService<IPasswordHasherService>();

//    var oracleDbConn = configuration.GetConnectionString("OracleDb");
//    var gdalOracleConn = configuration.GetConnectionString("GdalOracle");

//    return new DatabaseService(oracleDbConn, gdalOracleConn, hasher);
//});
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<SentinelHubService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<FarmService>();
builder.Services.AddScoped<FieldsListService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<FieldService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PythonService>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseDefaultFiles(); // Umo¿liwia u¿ycie index.html jako domyœlnej strony
app.UseStaticFiles();  // Obs³uguje pliki statyczne jak HTML, CSS, JS

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();



app.MapControllers();


app.Run();



