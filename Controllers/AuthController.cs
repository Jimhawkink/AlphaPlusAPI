using Microsoft.AspNetCore.Mvc;
using AlphaPlusAPI.Services;
using AlphaPlusAPI.DTOs;

namespace AlphaPlusAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.UserID) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "UserID and Password are required"
                });
            }

            var result = await _authService.AuthenticateAsync(request);
            
            if (!result.Success)
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<bool>>> Register([FromBody] CreateUserRequest request)
        {
            if (string.IsNullOrEmpty(request.UserID) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "UserID and Password are required"
                });
            }

            var result = await _authService.CreateUserAsync(request);
            return Ok(result);
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "API is running!", timestamp = DateTime.Now });
        }
    }
}

