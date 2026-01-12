using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

/// <summary>
/// Kontroler zarządzający ustawieniami profilu użytkownika (motywy, dane osobowe, jednostki).
/// </summary>
[Route("api/settings")]
[ApiController]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(SettingsService settingsService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    private string GetCurrentUsername() => User.Identity?.Name ?? string.Empty;

    /// <summary>
    /// Pobiera skrócone informacje o ustawieniach.
    /// </summary>
    /// <returns>Obiekt z podstawowymi ustawieniami.</returns>
    [HttpGet("getShortInfo")]
    public async Task<IActionResult> GetShortInfo()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var userInfo = await _settingsService.GetShortInfoAsync(username);
            if (userInfo == null) return NotFound();
            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania ShortInfo dla {Username}", username);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Pobiera szczegółowe dane profilowe użytkownika.
    /// </summary>
    /// <returns>Obiekt ze szczegółowymi danymi profilu.</returns>
    [HttpGet("getLongInfo")]
    public async Task<IActionResult> GetLongInfo()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var userInfo = await _settingsService.GetLongInfoAsync(username);
            if (userInfo == null) return NotFound();
            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania LongInfo dla {Username}", username);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Zmienia preferowaną jednostkę powierzchni (ha/ar/akr).
    /// </summary>
    /// <param name="dto">Nowa jednostka powierzchni (0/1/2).</param>
    /// <returns>Potwierdzenie aktualizacji.</returns>
    [HttpPost("updateSurface")]
    public async Task<IActionResult> UpdateSurfaceAsync([FromBody] UpdateSurfaceRequest dto)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            await _settingsService.UpdateSurfaceAsync(username, dto.Surface);
            _logger.LogInformation("Użytkownik {Username} zmienił jednostkę powierzchni.", username);
            return Ok(new { message = "Jednostka powierzchni zaktualizowana" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji powierzchni dla {Username}", username);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Aktualizuje motyw graficzny aplikacji (Jasny/Ciemny).
    /// </summary>
    /// <param name="dto">Ustawienia motywu (0 lub 1).</param>
    /// <returns>Potwierdzenie aktualizacji.</returns>
    [HttpPost("updateTheme")]
    public async Task<IActionResult> UpdateThemeAsync([FromBody] UpdateThemeRequest dto)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (dto.DarkMode != 0 && dto.DarkMode != 1)
            return BadRequest(new { message = "Niepoprawny motyw" });

        try
        {
            await _settingsService.UpdateThemeAsync(username, dto.DarkMode);
            return Ok(new { message = "Motyw zapisany" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji motywu dla {Username}", username);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Zmienia imię użytkownika.
    /// </summary>
    /// <param name="req">Nowe imię.</param>
    /// <returns>Potwierdzenie aktualizacji.</returns>
    [HttpPost("updateName")]
    public async Task<IActionResult> UpdateNameAsync([FromBody] UpdateNameRequest req)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Brak przesłanego imienia" });

        try
        {
            await _settingsService.UpdateNameAsync(username, req.Name);
            _logger.LogInformation("Użytkownik {Username} zmienił imię.", username);
            return Ok(new { message = "Imię zapisane" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji imienia dla {Username}", username);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Aktualizuje adres e-mail użytkownika.
    /// </summary>
    /// <param name="req">Nowy adres e-mail.</param>
    /// <returns>Potwierdzenie aktualizacji.</returns>
    [HttpPost("updateEmail")]
    public async Task<IActionResult> UpdateEmailAsync([FromBody] UpdateEmailRequest req)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Brak przesłanego emaila" });

        try
        {
            await _settingsService.UpdateEmailAsync(username, req.Email);
            _logger.LogInformation("Użytkownik {Username} zmienił email na {Email}", username, req.Email);
            return Ok(new { message = "Email zapisany" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji emaila dla {Username}", username);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Zmienia numer telefonu użytkownika.
    /// </summary>
    /// <param name="req">Nowy numer telefonu.</param>
    /// <returns>Potwierdzenie aktualizacji.</returns>
    [HttpPost("updatePhone")]
    public async Task<IActionResult> UpdatePhoneAsync([FromBody] UpdatePhoneRequest req)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { message = "Brak przesłanego telefonu" });

        try
        {
            await _settingsService.UpdatePhoneAsync(username, req.Phone);
            _logger.LogInformation("Użytkownik {Username} zmienił telefon.", username);
            return Ok(new { message = "Telefon zapisany" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji telefonu dla {Username}", username);
            return StatusCode(500);
        }
    }
}
