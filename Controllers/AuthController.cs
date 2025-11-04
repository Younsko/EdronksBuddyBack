namespace BudgetBuddy.Controllers;
using Microsoft.AspNetCore.Mvc;
using BudgetBuddy.Models;
using BudgetBuddy.Services;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly CategoryService _categoryService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db, 
        AuthService authService, 
        CategoryService categoryService,
        ILogger<AuthController> logger)
    {
        _db = db;
        _authService = authService;
        _categoryService = categoryService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account with validation
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        // Validation automatique des DataAnnotations
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { errors });
        }

        // Check username availability
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
        {
            _logger.LogWarning($"Registration failed: Username '{dto.Username}' already taken");
            return BadRequest(new { error = "Username already taken" });
        }

        // Check email availability
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
        {
            _logger.LogWarning($"Registration failed: Email '{dto.Email}' already registered");
            return BadRequest(new { error = "Email already registered" });
        }

        try
        {
            // Create user
            var user = new User
            {
                Name = dto.Name,
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = _authService.HashPassword(dto.Password),
                PreferredCurrency = "USD",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // ✅ Initialize default categories
            await _categoryService.InitializeDefaultCategoriesAsync(user.Id);

            _logger.LogInformation($"User registered successfully: {user.Username} (ID: {user.Id})");

            var expiresAt = DateTime.UtcNow.AddHours(1);

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Name = user.Name,
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                Token = _authService.GenerateToken(user),
                ExpiresAt = expiresAt
            });
        }
catch (Exception ex)
{
    _logger.LogError(ex, "Registration error");
    return StatusCode(500, new { 
        error = "An error occurred during registration", 
        details = ex.Message,
        inner = ex.InnerException?.Message
    });
}

    }

    /// <summary>
    /// Login with username/email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Support login with username or email
            var user = await _db.Users.FirstOrDefaultAsync(u => 
                u.Username == dto.Username || u.Email == dto.Username);

            if (user == null || !_authService.VerifyPassword(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning($"Failed login attempt for: {dto.Username}");
                return Unauthorized(new { error = "Invalid credentials" });
            }

            _logger.LogInformation($"User logged in: {user.Username} (ID: {user.Id})");

            var expiresAt = DateTime.UtcNow.AddHours(1);

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Name = user.Name,
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                Token = _authService.GenerateToken(user),
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Validate current token (useful for frontend)
    /// </summary>
    [HttpPost("validate")]
    public IActionResult ValidateToken([FromBody] string token)
    {
        var isValid = _authService.ValidateToken(token);
        return Ok(new { valid = isValid });
    }
}