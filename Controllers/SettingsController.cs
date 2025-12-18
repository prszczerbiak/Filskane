using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{


    [Route("api/settings")]
    [ApiController]
    [Authorize] // Dajemy Authorize na całą klasę, bo wszystkie metody go wymagają
    public class SettingsController : ControllerBase
    {
        private readonly SettingsService _settingsService;

        public SettingsController(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        // Helper do wyciągania loginu (DRY - Don't Repeat Yourself)
        private string GetCurrentUsername() => User.Identity?.Name ?? string.Empty;

        [HttpGet("getShortInfo")]
        public async Task<IActionResult> GetShortInfo()
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var userInfo = await _settingsService.GetShortInfoAsync(username);

            if (userInfo == null) return NotFound();

            return Ok(userInfo);
        }

        [HttpGet("getLongInfo")]
        public async Task<IActionResult> GetLongInfo()
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var userInfo = await _settingsService.GetLongInfoAsync(username);

            if (userInfo == null) return NotFound();

            return Ok(userInfo);
        }

        [HttpPost("updateSurface")]
        public async Task<IActionResult> UpdateSurface([FromBody] UpdateSurfaceRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            await _settingsService.UpdateSurfaceAsync(username, dto.Surface);
            return Ok(new { message = "Jednostka powierzchni zaktualizowana" });
        }

        [HttpPost("updateTheme")]
        public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            if (dto.Theme != 0 && dto.Theme != 1)
                return BadRequest(new { message = "Niepoprawny motyw" });

            await _settingsService.UpdateThemeAsync(username, dto.Theme);
            return Ok(new { message = "Motyw zapisany" });
        }

        [HttpPost("updateName")]
        public async Task<IActionResult> UpdateName([FromBody] UpdateNameRequest req)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Brak przesłanego imienia" });

            await _settingsService.UpdateNameAsync(username, req.Name);
            return Ok(new { message = "Imię zapisane" });
        }

        [HttpPost("updateEmail")]
        public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest req)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { message = "Brak przesłanego emaila" });

            await _settingsService.UpdateEmailAsync(username, req.Email);
            return Ok(new { message = "Email zapisany" });
        }

        [HttpPost("updatePhone")]
        public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequest req)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Phone))
                return BadRequest(new { message = "Brak przesłanego telefonu" });

            await _settingsService.UpdatePhoneAsync(username, req.Phone);
            return Ok(new { message = "Telefon zapisany" });
        }
    }
}
