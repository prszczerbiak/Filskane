using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Oracle.ManagedDataAccess.Client;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApplication1.Models;
using WebApplication1.Services;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _dbService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

    public AuthController(DatabaseService dbService, EmailService emailService, IConfiguration config, ILogger<EmailService> logger)
    {
        _dbService = dbService;
        _config = config;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] WebApplication1.Models.LoginRequest request)
    {
        if (_dbService.ValidateUser(request.Username, request.Password))
        {
            var token = GenerateJwtToken(request.Username);
            return Ok(new { token });
        }
        return Unauthorized("Nieprawidłowy login lub hasło.");
    }

    private string GenerateJwtToken(string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _config["Jwt:Issuer"],
            _config["Jwt:Issuer"],
            new[]
            {
                new Claim(ClaimTypes.Name, username)
            },
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("check-email")]
    public IActionResult CheckEmail([FromBody] EmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        bool exists = _dbService.CheckIfEmailExists(request.Email);

        return Ok(new { exists });
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] WebApplication1.Models.RegisterRequest request)
    {
        try
        {
            string token = GenerateVerificationToken();
            _dbService.RegisterUser(request, token); // Zapisz użytkownika i token w DB

            Task.Run(() => _emailService.SendVerificationEmail(request.Email, token));

            return Ok(new { message = "Użytkownik zarejestrowany. Wysłano e-mail weryfikacyjny." });
        }
        catch (OracleException ex)
        {
            _logger.LogError(ex, "Błąd bazy danych podczas rejestracji użytkownika {Email}", request.Email);
            return StatusCode(500, new { message = "Błąd bazy danych: " + ex.Message });
        }
        catch (InvalidOperationException ex) // przechwytywane z EmailService
        {
            _logger.LogError(ex, "Błąd wysyłki maila dla {Email}", request.Email);
            return StatusCode(500, new { message = "Błąd wysyłki maila: " + ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nieoczekiwany błąd przy rejestracji użytkownika {Email}", request.Email);
            return StatusCode(500, new { message = "Wewnętrzny błąd serwera: " + ex.Message });
        }
    }

    public string GenerateVerificationToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    [HttpGet("verify")]
    public IActionResult Verify([FromQuery] string token)
    {
        bool success = _dbService.VerifyUser(token);
        if (success)
            return Ok("Konto zostało aktywowane.");
        else
            return BadRequest("Nieprawidłowy lub wygasły token.");
    }

    [HttpPost("deleteAccount")]
    [Authorize]
    public IActionResult DeleteAccount([FromBody] DeleteAccountRequest req)
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        try
        {
            // Wywołujemy funkcję, która weryfikuje hasło i usuwa konto
            bool deleted = _dbService.DeleteUserAccount(username, req.Password);

            if (!deleted)
                return BadRequest(new { message = "Niepoprawne hasło" });

            // Konto usunięte pomyślnie
            return Ok(new { message = "Konto zostało usunięte" });
        }
        catch (Exception ex)
        {
            // Obsługa błędów np. połączenia z bazą
            return StatusCode(500, new { message = "Wystąpił błąd podczas usuwania konta: " + ex.Message });
        }
    }
}

