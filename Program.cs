using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Configuration;
using System.Text;
using WebApplication1.Analysis;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// Dodaj dostêp do konfiguracji
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Odczytaj connection string i dodaj do DI
builder.Services.AddSingleton<IPasswordHasherService, Argon2PasswordHasherService>();
builder.Services.AddSingleton<DatabaseService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var hasher = sp.GetRequiredService<IPasswordHasherService>();

    var oracleDbConn = configuration.GetConnectionString("OracleDb");
    var gdalOracleConn = configuration.GetConnectionString("GdalOracle");

    return new DatabaseService(oracleDbConn, gdalOracleConn, hasher);
});
builder.Services.AddSingleton<EmailService>();
builder.Services.AddHttpClient<SentinelHubService>();
builder.Services.AddHostedService<ThresholdService>();
builder.Services.AddSingleton<ThresholdStore>();
builder.Services.AddTransient<NdviAnalysisService>();

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



