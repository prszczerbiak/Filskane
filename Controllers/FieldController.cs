using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

[ApiController]
[Route("api/field")]
public class FieldController : ControllerBase
{
    private readonly DatabaseService _db;

    public FieldController(DatabaseService db)
    {
        _db = db;
    }

    [HttpGet("getData/{fieldId}")]
    public IActionResult GetFieldInfo(int fieldId)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        var field = _db.GetUserFieldById(username, fieldId);
        if (field == null)
            return NotFound("Nie znaleziono pola");

        return Ok(field);
    }
    [HttpPut("update/{fieldId}")]
    public IActionResult UpdateFieldInfo(int fieldId, [FromBody] UpdateFieldDto updateDto)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        try
        {
            _db.SaveFieldChanges(fieldId, username, updateDto);

            // pobierz zaktualizowane dane i zwróć je
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
