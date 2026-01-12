using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Filskane.DAL;
using Filskane.Models;

namespace Filskane.Services;

/// <summary>
/// Serwis biznesowy odpowiedzialny za procesy uwierzytelniania, rejestracji oraz zarządzania sesją użytkownika (JWT).
/// </summary>
public class AuthService
{
    private readonly AuthDAL _authDal;
    private readonly EmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AuthDAL authDal, EmailService emailService, IConfiguration config, ILogger<AuthService> logger)
    {
        _authDal = authDal;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Weryfikuje poświadczenia użytkownika i w przypadku sukcesu generuje token dostępowy.
    /// </summary>
    /// <param name="request">Obiekt zawierający nazwę użytkownika i hasło.</param>
    /// <returns>Wynik operacji logowania (status + token).</returns>
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        bool isValid = await _authDal.ValidateUserAsync(request.Username, request.Password);

        if (!isValid)
        {
            _logger.LogWarning("Nieudana próba logowania: {Username}", request.Username);
            return new LoginResult(false);
        }

        string token = GenerateJwtToken(request.Username);

        _logger.LogInformation("Pomyślne logowanie użytkownika: {Username}", request.Username);
        return new LoginResult(true, token);
    }



    /// <summary>
    /// Przeprowadza pełny proces rejestracji: tworzy użytkownika w bazie i wysyła e-mail weryfikacyjny.
    /// </summary>
    /// <param name="request">Dane nowego użytkownika.</param>
    public async Task RegisterUserAsync(RegisterRequest request)
    {
        string verificationToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        await _authDal.RegisterUserAsync(request, verificationToken);

        await _emailService.SendVerificationEmailAsync(request.Email, verificationToken);

        _logger.LogInformation("Zarejestrowano nowego użytkownika: {Email}. Wysłano token.", request.Email);
    }

    /// <summary>
    /// Aktywuje konto użytkownika na podstawie tokenu otrzymanego w wiadomości e-mail.
    /// </summary>
    /// <param name="token">Token weryfikacyjny.</param>
    /// <returns>True, jeśli aktywacja przebiegła pomyślnie.</returns>
    public async Task<bool> VerifyAccountAsync(string token)
    {
        bool result = await _authDal.VerifyUserAsync(token);
        if (result)
        {
            _logger.LogInformation("Pomyślnie zweryfikowano konto tokenem.");
        }
        return result;
    }

    /// <summary>
    /// Sprawdza, czy podany adres e-mail jest już zarejestrowany w systemie.
    /// </summary>
    /// <param name="email">Adres e-mail.</param>
    /// <returns>True, jeśli e-mail jest zajęty.</returns>
    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _authDal.CheckIfEmailExistsAsync(email);
    }

    /// <summary>
    /// Trwale usuwa konto użytkownika po ponownej weryfikacji hasła.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="password">Hasło potwierdzające tożsamość.</param>
    /// <returns>True, jeśli konto zostało usunięte.</returns>
    public async Task<bool> DeleteAccountAsync(string username, string password)
    {
        bool deleted = await _authDal.DeleteUserAccountAsync(username, password);

        if (deleted)
        {
            _logger.LogWarning("Użytkownik {Username} usunął trwale swoje konto.", username);
        }
        else
        {
            _logger.LogWarning("Nieudana próba usunięcia konta {Username} (błędne hasło).", username);
        }

        return deleted;
    }

    /// <summary>
    /// Generuje podpisany cyfrowo token JWT (JSON Web Token).
    /// </summary>
    /// <param name="username">Nazwa użytkownika, dla którego generowany jest token.</param>
    /// <returns>Ciąg znaków reprezentujący token.</returns>
    /// <exception cref="InvalidOperationException">Wyrzucany, gdy brakuje konfiguracji JWT w appsettings.json.</exception>
    private string GenerateJwtToken(string username)
    {
        var jwtKey = _config["Jwt:Key"];
        var jwtIssuer = _config["Jwt:Issuer"];

        if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
        {
            _logger.LogError("Brak konfiguracji JWT w appsettings.json!");
            throw new InvalidOperationException("Błąd konfiguracji serwera (JWT).");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
