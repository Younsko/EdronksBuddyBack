namespace BudgetBuddy.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetBuddy.Models;
using BudgetBuddy.Services;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly CurrencyService _currencyService; 
    private readonly ILogger<UserController> _logger;

    public UserController(
        AppDbContext db, 
        AuthService authService, 
        CurrencyService currencyService, 
        ILogger<UserController> logger)
    {
        _db = db;
        _authService = authService;
        _currencyService = currencyService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = GetUserId();
        var user = await _db.Users
            .Include(u => u.Categories)
            .Include(u => u.Transactions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { error = "User not found" });

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            PreferredCurrency = user.PreferredCurrency,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            TotalCategories = user.Categories.Count,
            TotalTransactions = user.Transactions.Count
        });
    }

    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile(UserUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound(new { error = "User not found" });

        try
        {
            bool hasChanges = false;

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                user.Name = dto.Name.Trim();
                hasChanges = true;
                _logger.LogInformation($"User {userId} updated name");
            }

            if (dto.ProfilePhotoUrl != null)
            {
                if (!string.IsNullOrEmpty(dto.ProfilePhotoUrl) && 
                    !Uri.TryCreate(dto.ProfilePhotoUrl, UriKind.Absolute, out _))
                {
                    return BadRequest(new { error = "Invalid profile photo URL" });
                }

                user.ProfilePhotoUrl = dto.ProfilePhotoUrl;
                hasChanges = true;
                _logger.LogInformation($"User {userId} updated profile photo");
            }

            if (hasChanges)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                Email = user.Email,
                PreferredCurrency = user.PreferredCurrency,
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                TotalCategories = await _db.Categories.CountAsync(c => c.UserId == userId),
                TotalTransactions = await _db.Transactions.CountAsync(t => t.UserId == userId)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Profile update error for user {userId}: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while updating profile" });
        }
    }

    [HttpPut("settings")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult> UpdateSettings([FromBody] UserSettingsDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound(new { error = "User not found" });

        try
        {
            if (!string.IsNullOrWhiteSpace(dto.PreferredCurrency))
            {
                if (!_currencyService.IsValidCurrency(dto.PreferredCurrency))
                {
                    return BadRequest(new { 
                        error = "Unsupported currency", 
                        supported = CurrencyService.SupportedCurrencies 
                    });
                }
                
                user.PreferredCurrency = dto.PreferredCurrency.ToUpper();
                _logger.LogInformation($"User {userId} updated currency to {user.PreferredCurrency}");
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Settings updated successfully", currency = user.PreferredCurrency });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Settings update error for user {userId}: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while updating settings" });
        }
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(StatsDto), 200)]
    public async Task<ActionResult<StatsDto>> GetStats([FromQuery] int year = 0, [FromQuery] int month = 0)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;
        if (year == 0) year = now.Year;
        if (month == 0) month = now.Month;

        try
        {
            var user = await _db.Users
                .Include(u => u.Categories)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { error = "User not found" });

            var transactions = await _db.Transactions
                .Where(t => t.UserId == userId
                    && t.TransactionDate.Year == year
                    && t.TransactionDate.Month == month)
                .Include(t => t.Category)
                .ToListAsync();

            var stats = new StatsDto
            {
                TotalTransactions = transactions.Count
            };

            foreach (var cat in user.Categories)
            {
                var categoryTransactions = transactions.Where(t => t.CategoryId == cat.Id).ToList();
                var spent = categoryTransactions.Sum(t => t.Amount);

                stats.ByCategory.Add(new CategorySpendingDto
                {
                    CategoryName = cat.Name,
                    Color = cat.Color,
                    Spent = spent,
                    Budget = cat.MonthlyBudget,
                    TransactionCount = categoryTransactions.Count
                });
                stats.TotalBudgetThisMonth += cat.MonthlyBudget;
            }

            var byCurrency = transactions.GroupBy(t => t.Currency);
            foreach (var group in byCurrency)
            {
                var totalInCurrency = group.Sum(t => t.Amount);
                var converted = await _currencyService.ConvertAsync(
                    totalInCurrency,
                    group.Key,
                    user.PreferredCurrency
                );

                stats.ByCurrency.Add(new CurrencySpendingDto
                {
                    Currency = group.Key,
                    Amount = totalInCurrency,
                    ConvertedToPreferred = converted
                });
                stats.TotalSpentThisMonth += converted;
            }

            var dailyGroups = transactions
                .GroupBy(t => t.TransactionDate.Date)
                .OrderBy(g => g.Key);

            foreach (var group in dailyGroups)
            {
                stats.DailySpending.Add(new DailySpendingDto
                {
                    Date = group.Key,
                    Amount = group.Sum(t => t.Amount),
                    TransactionCount = group.Count()
                });
            }

            _logger.LogInformation($"Stats generated for user {userId}: {year}/{month}");

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Stats error for user {userId}: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while fetching statistics" });
        }
    }

[HttpPut("password")]
[ProducesResponseType(200)]
[ProducesResponseType(400)]
[ProducesResponseType(404)]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var userId = GetUserId();
    var user = await _db.Users.FindAsync(userId);

    if (user == null)
        return NotFound(new { error = "User not found" });

    // Verify current password
    if (!_authService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
        return BadRequest(new { error = "Current password is incorrect" });

    try
    {
        // Update password
        user.PasswordHash = _authService.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();

        _logger.LogInformation($"User {userId} changed password successfully");

        return Ok(new { message = "Password changed successfully" });
    }
    catch (Exception ex)
    {
        _logger.LogError($"Password change error for user {userId}: {ex.Message}");
        return StatusCode(500, new { error = "An error occurred while changing password" });
    }
}
    [HttpDelete("profile")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { error = "Invalid request body" });
        }

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound(new { error = "User not found" });

        if (!_authService.VerifyPassword(dto.Password, user.PasswordHash))
            return BadRequest(new { error = "Invalid password" });

        if (dto.Confirmation != "DELETE_ZONE1")
            return BadRequest(new { error = "Invalid confirmation phrase" });

        try
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            _logger.LogWarning($"User account deleted: {user.Username} (ID: {userId})");

            return Ok(new { message = "Account deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Account deletion error for user {userId}: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while deleting account" });
        }
    }
}