using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Filskane.Services;

namespace Filskane.Controllers;

/// <summary>
/// Kontroler obsługujący pobieranie listy pól do menu nawigacyjnego aplikacji.
/// </summary>
[ApiController]
[Route("api/fieldsList")]
[Authorize]
public class FieldsListController : ControllerBase
{
    private readonly FieldsListService _fieldsListService;
    private readonly ILogger<FieldsListController> _logger;

    public FieldsListController(FieldsListService fieldsListService, ILogger<FieldsListController> logger)
    {
        _fieldsListService = fieldsListService;
        _logger = logger;
    }

    /// <summary>
    /// Pobiera uproszczoną listę pól użytkownika (ID i Nazwa) na potrzeby budowania menu bocznego.
    /// </summary>
    /// <returns>Lista obiektów zawierających podstawowe dane pól.</returns>
    [HttpGet]
    public async Task<IActionResult> GetFieldsList()
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("Próba dostępu do listy pól bez poprawnego User Identity.");
            return Unauthorized("Brak użytkownika w tokenie");
        }

        try
        {
            var fields = await _fieldsListService.GetFieldsListForMenuAsync(username);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania menu pól dla {Username}", username);
            return StatusCode(500, "Błąd serwera");
        }
    }
}
