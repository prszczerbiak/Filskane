using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    

    public record UpdateNameRequest(string Name);
    public record UpdateEmailRequest(string Email);
    public record UpdatePhoneRequest(string Phone);

    [Route("api/userinfo")]
    [ApiController]
    public class UserInfoController : ControllerBase
    {
        private readonly DatabaseService _dbService;

        public UserInfoController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        [HttpGet("getShortInfo")]
        [Authorize] // wymaga poprawnego JWT
        public IActionResult GetShortInfo()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var userInfo = _dbService.GetUserShortInfo(username);

            if (userInfo == null)
                return NotFound();

            return Ok(userInfo);
        }

        [HttpGet("getLongInfo")]
        [Authorize]
        public IActionResult GetLongInfo()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var userInfo = _dbService.GetUserInfo(username);

            if (userInfo == null)
                return NotFound();

            return Ok(userInfo);
        }

        //[HttpGet]
        //public IActionResult Get()
        //{
        //    // Zakładamy, że User.Identity.Name zawiera login użytkownika
        //    var username = User.Identity?.Name;

        //    if (string.IsNullOrEmpty(username))
        //        return Unauthorized();

        //    // Pobranie ustawień z bazy
        //    var settings = _dbService.GetUserSettings(username);

        //    if (settings == null)
        //        return NotFound();

        //    return Ok(settings);
        //}

        //// POST /api/user/settings
        [HttpPost("updateSurface")]
        [Authorize]
        public IActionResult UpdateSurface([FromBody] SurfaceUpdateDto dto)
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            try
            {
                _dbService.UpdateUserSurface(username, dto.Surface);
                return Ok(new { message = "Jednostka powierzchni zaktualizowana" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("updateTheme")]
        [Authorize]
        public IActionResult UpdateTheme([FromBody] ThemeUpdateDto dto)
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            if (dto.Theme != "light" && dto.Theme != "dark")
                return BadRequest(new { message = "Niepoprawny motyw" });

            try
            {
                _dbService.UpdateUserTheme(username, dto.Theme);
                return Ok(new { message = "Motyw zapisany" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Błąd zapisu motywu: " + ex.Message });
            }
        }

        [HttpPost("updateName")]
        [Authorize]
        public IActionResult UpdateName([FromBody] UpdateNameRequest req)
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Brak przesłanego imienia" });

            try
            {
                _dbService.UpdateUserName(username, req.Name);
                return Ok(new { message = "Imię zapisane" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Błąd zapisu imienia: " + ex.Message });
            }
        }

        [HttpPost("updateEmail")]
        [Authorize]
        public IActionResult UpdateEmail([FromBody] UpdateEmailRequest req)
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { message = "Brak przesłanego emaila" });

            try
            {
                _dbService.UpdateUserEmail(username, req.Email);
                return Ok(new { message = "Email zapisany" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Błąd zapisu emaila: " + ex.Message });
            }
        }

        [HttpPost("updatePhone")]
        [Authorize]
        public IActionResult UpdatePhone([FromBody] UpdatePhoneRequest req)
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Phone))
                return BadRequest(new { message = "Brak przesłanego telefonu" });

            try
            {
                _dbService.UpdateUserPhone(username, req.Phone);
                return Ok(new { message = "Telefon zapisany" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Błąd zapisu telefonu: " + ex.Message });
            }
        }
    }
}
