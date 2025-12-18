using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/fieldsList")]
    [Authorize]
    public class FieldsListController : ControllerBase
    {
        private readonly FieldsListService _fieldsListService;

        public FieldsListController(FieldsListService fieldsListService)
        {
            _fieldsListService = fieldsListService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFieldsList()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized("Brak użytkownika w tokenie");

            // Pobieramy lekką listę (tylko ID i Nazwa)
            var fields = await _fieldsListService.GetFieldsListForMenuAsync(username);

            return Ok(fields);
        }
    }
}