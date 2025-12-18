using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // Używamy AuthService zgodnie z diagramem, zamiast surowego DbService
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Cała logika walidacji hasła i generowania tokena jest w serwisie
            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                return Ok(new { token = result.Token });
            }

            return Unauthorized("Nieprawidłowy login lub hasło.");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Kontroler nie wie nic o tokenach weryfikacyjnych ani mailach.
                // Mówi tylko serwisowi: "Zarejestruj tego człowieka".
                await _authService.RegisterUserAsync(request);
                return Ok(new { message = "Użytkownik zarejestrowany. Sprawdź e-mail." });
            }
            catch (Exception ex)
            {
                // W produkcji nie zwracamy ex.Message użytkownikowi (security!), ale w inżynierce ujdzie
                return StatusCode(500, new { message = "Wystąpił błąd serwera." });
            }
        }

        [HttpPost("check-email")]
        public async Task<IActionResult> CheckEmail([FromBody] CheckEmailRequest request)
        {
            bool exists = await _authService.EmailExistsAsync(request.Email);
            return Ok(new { exists });
        }

        [HttpGet("verify")]
        public async Task<IActionResult> Verify([FromQuery] string token)
        {
            bool success = await _authService.VerifyAccountAsync(token);
            return success ? Ok("Konto aktywowane.") : BadRequest("Błędny token.");
        }

        [HttpPost("deleteAccount")]
        [Authorize] // To zapewnia, że User.Identity jest wypełnione
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest req)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            bool deleted = await _authService.DeleteAccountAsync(username, req.Password);

            if (!deleted) return BadRequest(new { message = "Niepoprawne hasło." });

            return Ok(new { message = "Konto usunięte." });
        }
    }
}