using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services; // Pamiętaj o usingach

[Authorize]
[Route("api/farm")]
[ApiController]
public class FarmController : ControllerBase
{
    private readonly FarmService _farmService; // Używamy serwisu!

    public FarmController(FarmService farmService)
    {
        _farmService = farmService;
    }

    private string GetCurrentUsername() => User.Identity?.Name ?? string.Empty;

    [HttpGet("getFarm")]
    public async Task<IActionResult> GetCurrentFarm()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _farmService.GetCurrentFarmInfoAsync(username);
        if (user == null) return NotFound("Nie znaleziono użytkownika.");

        // Zwracamy tylko potrzebne pola (anonimowy obiekt lub DTO)
        return Ok(new { user.Username, user.FarmX, user.FarmY });
    }

    [HttpPost("setCoords")]
    public async Task<IActionResult> SetFarmCoords([FromBody] FarmCoordsRequest coords)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        await _farmService.SetFarmCoordsAsync(username, coords.FarmX, coords.FarmY);
        return Ok(new { message = "Koordynaty zostały zapisane." });
    }

    [HttpDelete("deleteFarm")]
    public async Task<IActionResult> DeleteFarmCoords()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        await _farmService.DeleteFarmCoordsAsync(username);
        return Ok(new { message = "Koordynaty zostały usunięte." });
    }

    [HttpPost("saveField")]
    public async Task<IActionResult> SaveField([FromBody] SaveFieldRequest dto)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (string.IsNullOrEmpty(dto.Geojson))
            return BadRequest(new { error = "Pole GeoJSON nie może być puste." });

        // Cała magia dzieje się w serwisie
        int fieldId = await _farmService.AddFieldWithSoilInfoAsync(username, dto);

        return Ok(new { fieldId });
    }

    [HttpGet("getUserFields")]
    public async Task<IActionResult> GetUserFields()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var fields = await _farmService.GetUserFieldsAsync(username);
        return Ok(fields);
    }

    [HttpDelete("deleteField/{fieldId}")]
    public async Task<IActionResult> DeleteField(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        await _farmService.DeleteFieldAsync(username, fieldId);
        return Ok(new { message = "Pole usunięte" });
    }
}