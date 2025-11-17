namespace BudgetBuddy.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetBuddy.Models;
using BudgetBuddy.Services;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CategoryService _categoryService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(
        AppDbContext db, 
        CategoryService categoryService,
        ILogger<CategoriesController> logger)
    {
        _db = db;
        _categoryService = categoryService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), 200)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] int? year = null, [FromQuery] int? month = null)
    {
        try
        {
            var categories = await _categoryService.GetCategoriesWithSpendingAsync(GetUserId(), year, month);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get categories error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while fetching categories" });
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        var userId = GetUserId();

        if (!await _categoryService.UserOwnsCategoryAsync(userId, id))
            return Forbid();

        var category = await _db.Categories.Include(c => c.MonthlyBudgets).FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
            return NotFound(new { error = "Category not found" });

        var now = DateTime.UtcNow;
        var spent = await _db.Transactions
            .Where(t => t.CategoryId == id && t.TransactionDate.Year == now.Year && t.TransactionDate.Month == now.Month)
            .SumAsync(t => t.Amount);

        var transactionCount = await _db.Transactions
            .Where(t => t.CategoryId == id && t.TransactionDate.Year == now.Year && t.TransactionDate.Month == now.Month)
            .CountAsync();

        return Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            MonthlyBudget = category.MonthlyBudget,
            SpentThisMonth = spent,
            TransactionCount = transactionCount
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        try
        {
            if (await _db.Categories.AnyAsync(c => c.UserId == userId && c.Name == dto.Name))
                return BadRequest(new { error = "A category with this name already exists" });

            var category = new Category
            {
                UserId = userId,
                Name = dto.Name,
                Color = dto.Color,
                MonthlyBudget = dto.MonthlyBudget,
                CreatedAt = DateTime.UtcNow
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Color = category.Color,
                MonthlyBudget = category.MonthlyBudget,
                SpentThisMonth = 0,
                TransactionCount = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Create category error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while creating category" });
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CategoryDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        var category = await _db.Categories.Include(c => c.MonthlyBudgets)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (category == null)
            return NotFound(new { error = "Category not found or access denied" });

        try
        {
            if (await _db.Categories.AnyAsync(c => c.UserId == userId && c.Name == dto.Name && c.Id != id))
                return BadRequest(new { error = "A category with this name already exists" });

            category.Name = dto.Name;
            category.Color = dto.Color;
            category.MonthlyBudget = dto.MonthlyBudget;

            // Update or create the monthly budget automatically
            var now = DateTime.UtcNow;
            var year = now.Year;
            var month = now.Month;

            var monthlyBudget = category.MonthlyBudgets.FirstOrDefault(b => b.Year == year && b.Month == month);
            if (monthlyBudget == null)
            {
                monthlyBudget = new CategoryMonthlyBudget
                {
                    CategoryId = category.Id,
                    UserId = userId,
                    Year = year,
                    Month = month,
                    BudgetAmount = category.MonthlyBudget,
                    CreatedAt = DateTime.UtcNow
                };
                _db.CategoryMonthlyBudgets.Add(monthlyBudget);
            }
            else
            {
                monthlyBudget.BudgetAmount = category.MonthlyBudget;
            }

            await _db.SaveChangesAsync();

            var spent = await _db.Transactions
                .Where(t => t.CategoryId == id && t.TransactionDate.Year == year && t.TransactionDate.Month == month)
                .SumAsync(t => t.Amount);

            return Ok(new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Color = category.Color,
                MonthlyBudget = category.MonthlyBudget,
                SpentThisMonth = spent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update category error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while updating category" });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var userId = GetUserId();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (category == null)
            return NotFound(new { error = "Category not found or access denied" });

        try
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Category deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Delete category error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while deleting category" });
        }
    }
}
