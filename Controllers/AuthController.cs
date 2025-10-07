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
    private readonly IConfiguration _config;
    private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

    public AuthController(DatabaseService dbService, IConfiguration config)
    {
        _dbService = dbService;
        _config = config;
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
            _dbService.RegisterUser(request);
            return Ok(new { message = "Użytkownik zarejestrowany pomyślnie" });
        }
        catch (OracleException ex)
        {
            // Zwróć sensowny komunikat JSON, żeby frontend mógł to odczytać
            return StatusCode(500, new { message = "Błąd bazy danych: " + ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Wewnętrzny błąd serwera: " + ex.Message });
        }
    }
}

