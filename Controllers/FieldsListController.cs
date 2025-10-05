using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

[ApiController]
[Route("api/fieldsList")]
[Authorize]
public class FieldsController : ControllerBase
{
    private readonly DatabaseService _db;

    public FieldsController(DatabaseService db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetUserFields()
    {
        var username = User.Identity?.Name; // pobranie nazwy użytkownika z tokenu

        if (string.IsNullOrEmpty(username))
            return Unauthorized("Brak użytkownika w tokenie");

        var fields = _db.GetUserFields(username);
        return Ok(fields);
    }
}
