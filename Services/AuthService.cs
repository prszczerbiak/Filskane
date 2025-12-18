using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApplication1.DAL;
using WebApplication1.Models;
namespace WebApplication1.Services
{
    public class AuthService
    {
        private readonly AuthDAL _authDal;
        private readonly EmailService _emailService;
        private readonly IConfiguration _config; // Do JWT

        public AuthService(AuthDAL authDal, EmailService emailService, IConfiguration config)
        {
            _authDal = authDal;
            _emailService = emailService;
            _config = config;
        }

        public async Task<LoginResult> LoginAsync(LoginRequest request)
        {
            // 1. Sprawdź usera w DB (asynchronicznie!)
            // Zakładam, że przerobisz DatabaseService na async
            bool isValid = await _authDal.ValidateUserAsync(request.Username, request.Password);

            if (!isValid) return new LoginResult ( false );

            // 2. Wygeneruj token (metoda prywatna w serwisie)
            string token = GenerateJwtToken(request.Username);

            return new LoginResult ( true, token );
        }

        public async Task RegisterUserAsync(RegisterRequest request)
        {
            // 1. Generuj token
            string verificationToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            // 2. Zapisz do bazy
            await _authDal.RegisterUserAsync(request, verificationToken);

            // 3. Wyślij maila (await - czekamy aż wyjdzie, żeby mieć pewność)
            await _emailService.SendVerificationEmailAsync(request.Email, verificationToken);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _authDal.CheckIfEmailExistsAsync(email);
        }

        public async Task<bool> VerifyAccountAsync(string token)
        {
            return await _authDal.VerifyUserAsync(token);
        }

        public async Task<bool> DeleteAccountAsync(string username, string password)
        {
            // Logika usuwania wymaga weryfikacji hasła wewnątrz DatabaseService
            return await _authDal.DeleteUserAccountAsync(username, password);
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

    }
}
