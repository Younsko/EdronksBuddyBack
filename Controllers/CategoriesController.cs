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

    /// <summary>
    /// List all categories for current user with spending info
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), 200)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(
        [FromQuery] int? year = null, 
        [FromQuery] int? month = null)
    {
        try
        {
            var categories = await _categoryService.GetCategoriesWithSpendingAsync(
                GetUserId(), year, month);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get categories error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while fetching categories" });
        }
    }

    /// <summary>
    /// Get a single category by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        var userId = GetUserId();

        // ✅ Security: Verify ownership
        if (!await _categoryService.UserOwnsCategoryAsync(userId, id))
            return Forbid();

        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(new { error = "Category not found" });

        var now = DateTime.UtcNow;
        var spent = await _db.Transactions
            .Where(t => t.CategoryId == id
                && t.TransactionDate.Year == now.Year
                && t.TransactionDate.Month == now.Month)
            .SumAsync(t => t.Amount);

        var transactionCount = await _db.Transactions
            .Where(t => t.CategoryId == id
                && t.TransactionDate.Year == now.Year
                && t.TransactionDate.Month == now.Month)
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

    /// <summary>
    /// Create a new category
    /// </summary>
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
            // Check for duplicate name
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

            _logger.LogInformation($"Category created: {category.Name} (ID: {category.Id}) by user {userId}");

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

    /// <summary>
    /// Update a category
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CategoryDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        // ✅ Security: Verify ownership
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (category == null)
            return NotFound(new { error = "Category not found or access denied" });

        try
        {
            // Check for duplicate name (excluding current category)
            if (await _db.Categories.AnyAsync(c => 
                c.UserId == userId && c.Name == dto.Name && c.Id != id))
                return BadRequest(new { error = "A category with this name already exists" });

            category.Name = dto.Name;
            category.Color = dto.Color;
            category.MonthlyBudget = dto.MonthlyBudget;

            await _db.SaveChangesAsync();

            _logger.LogInformation($"Category updated: {category.Name} (ID: {id}) by user {userId}");

            var now = DateTime.UtcNow;
            var spent = await _db.Transactions
                .Where(t => t.CategoryId == id
                    && t.TransactionDate.Year == now.Year
                    && t.TransactionDate.Month == now.Month)
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

    /// <summary>
    /// Delete a category
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var userId = GetUserId();

        // ✅ Security: Verify ownership
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (category == null)
            return NotFound(new { error = "Category not found or access denied" });

        try
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Category deleted: {category.Name} (ID: {id}) by user {userId}");

            return Ok(new { message = "Category deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Delete category error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while deleting category" });
        }
    }
}