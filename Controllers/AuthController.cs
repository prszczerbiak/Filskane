using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Filskane.Models;
using Filskane.Services;

namespace Filskane.Controllers;

/// <summary>
/// Kontroler odpowiedzialny za autoryzację i zarządzanie kontami użytkowników.
/// </summary>
[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Przeprowadza logowanie użytkownika.
    /// </summary>
    /// <param name="request">Obiekt zawierający nazwę użytkownika i hasło.</param>
    /// <returns>Token JWT w przypadku sukcesu lub informację o błędzie.</returns>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (result.Success)
        {
            return Ok(new { token = result.Token });
        }

        _logger.LogWarning("Nieudana próba logowania dla użytkownika: {Username}", request.Username);
        return Unauthorized(new { message = "Nieprawidłowy login lub hasło." });
    }

    /// <summary>
    /// Rejestruje nowego użytkownika w systemie.
    /// </summary>
    /// <param name="request">Dane wymagane do utworzenia konta.</param>
    /// <returns>Potwierdzenie wysłania e-maila weryfikacyjnego.</returns>
    /// <exception cref="Exception">Rzucany w przypadku błędu serwera lub bazy danych.</exception>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request)
    {
        try
        {
            await _authService.RegisterUserAsync(request);
            return Ok(new { message = "Użytkownik zarejestrowany. Sprawdź e-mail." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas rejestracji użytkownika {Email}", request.Email);
            return StatusCode(500, new { message = "Wystąpił błąd serwera podczas rejestracji." });
        }
    }

    /// <summary>
    /// Sprawdza, czy podany adres e-mail istnieje już w bazie.
    /// </summary>
    /// <param name="request">Adres e-mail do weryfikacji.</param>
    /// <returns>Obiekt JSON z flagą 'exists'.</returns>
    [HttpPost("check-email")]
    public async Task<IActionResult> CheckEmailAsync([FromBody] CheckEmailRequest request)
    {
        bool exists = await _authService.EmailExistsAsync(request.Email);
        return Ok(new { exists });
    }

    /// <summary>
    /// Weryfikuje konto na podstawie tokenu wysłanego e-mailem.
    /// </summary>
    /// <param name="token">Token weryfikacyjny z linku aktywacyjnego.</param>
    /// <returns>Komunikat o powodzeniu aktywacji lub błędzie.</returns>
    [HttpGet("verify")]
    public async Task<IActionResult> VerifyAsync([FromQuery] string token)
    {
        bool success = await _authService.VerifyAccountAsync(token);

        if (success)
        {
            return Ok(new { message = "Konto zostało pomyślnie aktywowane." });
        }

        return BadRequest(new { message = "Nieprawidłowy lub wygasły token weryfikacyjny." });
    }

    /// <summary>
    /// Trwale usuwa konto aktualnie zalogowanego użytkownika.
    /// </summary>
    /// <param name="req">Hasło użytkownika wymagane do potwierdzenia operacji.</param>
    /// <returns>Status operacji usunięcia.</returns>
    [HttpPost("deleteAccount")]
    [Authorize]
    public async Task<IActionResult> DeleteAccountAsync([FromBody] DeleteAccountRequest req)
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrEmpty(username))
            return Unauthorized(new { message = "Brak autoryzacji." });

        bool deleted = await _authService.DeleteAccountAsync(username, req.Password);

        if (!deleted)
        {
            _logger.LogWarning("Nieudana próba usunięcia konta {Username} - błędne hasło.", username);
            return BadRequest(new { message = "Niepoprawne hasło. Konto nie zostało usunięte." });
        }

        _logger.LogInformation("Konto {Username} zostało trwale usunięte.", username);
        return Ok(new { message = "Konto zostało trwale usunięte." });
    }
}