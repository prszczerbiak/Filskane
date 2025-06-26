using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/userinfo")]
    [ApiController]
    public class UserInfoController : ControllerBase
    {
        private readonly DatabaseService _dbService;

        public UserInfoController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        [HttpGet]
        [Authorize] // wymaga poprawnego JWT
        public IActionResult Get()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var user = _dbService.GetUserInfo(username);

            if (user == null)
                return NotFound();

            return Ok(user);
        }
    }
}
