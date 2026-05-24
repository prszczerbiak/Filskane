using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Filskane.Models;
using Filskane.Services;

namespace Filskane.Controllers;

/// <summary>
/// Kontroler zarządzający danymi farmy oraz polami uprawnymi użytkownika.
/// </summary>
[Authorize]
[Route("api/farm")]
[ApiController]
public class FarmController : ControllerBase
{
    private readonly FarmService _farmService;
    private readonly ILogger<FarmController> _logger;

    public FarmController(FarmService farmService, ILogger<FarmController> logger)
    {
        _farmService = farmService;
        _logger = logger;
    }

    // Pomocnicza metoda prywatna
    private string GetCurrentUsername() => User.Identity?.Name ?? string.Empty;


    /// <summary>
    /// Pobiera aktualne dane farmy (współrzędne) dla zalogowanego użytkownika.
    /// </summary>
    /// <returns>Obiekt z nazwą użytkownika i współrzędnymi farmy.</returns>
    [HttpGet("getFarm")]
    public async Task<IActionResult> GetCurrentFarm()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var user = await _farmService.GetCurrentFarmInfoAsync(username);
            if (user == null)
            {
                _logger.LogWarning("Użytkownik {Username} nie został znaleziony.", username);
                return NotFound("Nie znaleziono użytkownika.");
            }

            return Ok(new { user.Username, user.FarmX, user.FarmY });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania danych farmy dla użytkownika {Username}", username);
            return StatusCode(500, "Wystąpił błąd serwera.");
        }
    }

    /// <summary>
    /// Ustawia lub aktualizuje współrzędne geograficzne farmy.
    /// </summary>
    /// <param name="coords">Obiekt zawierający nowe współrzędne X i Y.</param>
    /// <returns>Potwierdzenie zapisu.</returns>
    [HttpPost("setCoords")]
    public async Task<IActionResult> SetFarmCoords([FromBody] FarmCoordsRequest coords)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            await _farmService.SetFarmCoordsAsync(username, coords.FarmX, coords.FarmY);
            _logger.LogInformation("Użytkownik {Username} zaktualizował koordynaty farmy.", username);
            return Ok(new { message = "Koordynaty zostały zapisane." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd zapisu koordynatów dla {Username}", username);
            return StatusCode(500, "Błąd zapisu.");
        }
    }

    /// <summary>
    /// Usuwa zapisane współrzędne farmy (resetuje ustawienia lokalizacji).
    /// </summary>
    /// <returns>Potwierdzenie usunięcia.</returns>
    [HttpDelete("deleteFarm")]
    public async Task<IActionResult> DeleteFarmCoords()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            await _farmService.DeleteFarmCoordsAsync(username);
            _logger.LogInformation("Użytkownik {Username} usunął farmę.", username);
            return Ok(new { message = "Koordynaty zostały usunięte." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd usuwania farmy dla {Username}", username);
            return StatusCode(500, "Błąd usuwania.");
        }
    }


    /// <summary>
    /// Dodaje nowe pole uprawne na podstawie danych GeoJSON.
    /// </summary>
    /// <param name="dto">Dane pola, w tym geometria w formacie GeoJSON.</param>
    /// <returns>ID nowo utworzonego pola.</returns>
    /// <exception cref="Exception">Rzucany przy błędzie zapisu do bazy.</exception>
    [HttpPost("saveField")]
    public async Task<IActionResult> SaveField([FromBody] SaveFieldRequest dto)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (string.IsNullOrEmpty(dto.Geojson))
            return BadRequest(new { error = "Pole GeoJSON nie może być puste." });

        try
        {
            int fieldId = await _farmService.AddFieldWithSoilInfoAsync(username, dto);
            _logger.LogInformation("Dodano nowe pole (ID: {FieldId}) dla użytkownika {Username}", fieldId, username);
            return Ok(new { fieldId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Krytyczny błąd podczas dodawania pola dla {Username}", username);
            return StatusCode(500, new { error = "Nie udało się zapisać pola. Sprawdź logi serwera." });
        }
    }

    /// <summary>
    /// Pobiera listę wszystkich pól należących do użytkownika.
    /// </summary>
    /// <returns>Lista obiektów pól.</returns>
    [HttpGet("getUserFields")]
    public async Task<IActionResult> GetUserFields()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var fields = await _farmService.GetUserFieldsAsync(username);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania listy pól dla {Username}", username);
            return StatusCode(500, "Błąd serwera.");
        }
    }

    /// <summary>
    /// Pobiera pojazdy przypisane do użytkownika wraz z wygenerowanymi pozycjami na mapie.
    /// </summary>
    /// <returns>Lista pojazdów do wyświetlenia na mapie.</returns>
    [HttpGet("getUserVehicles")]
    public async Task<IActionResult> GetUserVehicles()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var vehicles = await _farmService.GetUserVehiclesAsync(username);
            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania pojazdów dla {Username}", username);
            return StatusCode(500, "Błąd serwera.");
        }
    }

    /// <summary>
    /// Dodaje nowy pojazd do konta aktualnego użytkownika.
    /// </summary>
    /// <param name="dto">Dane nowego pojazdu.</param>
    /// <returns>ID dodanego pojazdu.</returns>
    [HttpPost("saveVehicle")]
    public async Task<IActionResult> SaveVehicle([FromBody] AddVehicleRequest dto)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.VehicleName))
            return BadRequest(new { error = "Nazwa pojazdu nie może być pusta." });

        if (string.IsNullOrWhiteSpace(dto.IpAdress))
            return BadRequest(new { error = "Adres IP nie może być pusty." });

        if (!System.Net.IPAddress.TryParse(dto.IpAdress, out _))
            return BadRequest(new { error = "Nieprawidłowy adres IP." });

        if (dto.TcpPort is < 1 or > 65535)
            return BadRequest(new { error = "Port TCP musi być z zakresu 1..65535." });

        try
        {
            var vehicleId = await _farmService.AddVehicleAsync(username, dto);
            _logger.LogInformation("Dodano nowy pojazd (ID: {VehicleId}) dla użytkownika {Username}", vehicleId, username);
            return Ok(new { vehicleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd dodawania pojazdu dla {Username}", username);
            return StatusCode(500, new { error = "Nie udało się zapisać pojazdu." });
        }
    }

    /// <summary>
    /// Usuwa pojazd przypisany do aktualnego użytkownika.
    /// </summary>
    [HttpDelete("deleteVehicle/{vehicleId}")]
    public async Task<IActionResult> DeleteVehicle(int vehicleId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var deleted = await _farmService.DeleteVehicleAsync(username, vehicleId);
            if (!deleted)
                return NotFound(new { error = "Nie znaleziono pojazdu." });

            _logger.LogInformation("Użytkownik {Username} usunął pojazd {VehicleId}", username, vehicleId);
            return Ok(new { message = "Pojazd usunięty." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd usuwania pojazdu {VehicleId} dla {Username}", vehicleId, username);
            return StatusCode(500, new { error = "Nie udało się usunąć pojazdu." });
        }
    }

    /// <summary>
    /// Usuwa wybrane pole z systemu.
    /// </summary>
    /// <param name="fieldId">Identyfikator pola do usunięcia.</param>
    /// <returns>Komunikat o powodzeniu.</returns>
    [HttpDelete("deleteField/{fieldId}")]
    public async Task<IActionResult> DeleteField(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            await _farmService.DeleteFieldAsync(username, fieldId);
            _logger.LogInformation("Użytkownik {Username} usunął pole o ID: {FieldId}", username, fieldId);
            return Ok(new { message = "Pole usunięte" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd usuwania pola {FieldId} dla {Username}", fieldId, username);
            return StatusCode(500, "Błąd serwera.");
        }
    }

}