using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using WebApplication1.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using WebApplication1.Models;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _dbService;
    private readonly IConfiguration _config;


    public AuthController(DatabaseService dbService, IConfiguration config)
    {
        _dbService = dbService;
        _config = config;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
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
}

