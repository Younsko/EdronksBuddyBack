namespace BudgetBuddy.Services;
using BudgetBuddy.Models;
using Microsoft.EntityFrameworkCore;

public class CategoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(AppDbContext db, ILogger<CategoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Categories created by default with no budget
    /// </summary>
    private static readonly List<(string Name, string Color)> DefaultCategories = new()
    {
        ("Food & Dining", "#FF6B6B"),
        ("Transportation", "#4ECDC4"),
        ("Shopping", "#45B7D1"),
        ("Entertainment", "#FFA07A"),
        ("Healthcare", "#98D8C8"),
        ("Housing", "#6C5CE7"),
        ("Utilities", "#FDCB6E"),
        ("Education", "#E17055"),
        ("Travel", "#A29BFE"),
        ("Personal Care", "#00B894"),
        ("Gifts & Donations", "#E84393"),
        ("Miscellaneous", "#636E72")
    };

    /// <summary>
    /// Initialize default categories for new user
    /// </summary>
    public async Task InitializeDefaultCategoriesAsync(int userId)
    {
        _logger.LogInformation($"Initializing default categories for user {userId}");

        var categories = DefaultCategories.Select(dc => new Category
        {
            UserId = userId,
            Name = dc.Name,
            Color = dc.Color,
            MonthlyBudget = 0,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _db.Categories.AddRange(categories);
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Created {categories.Count} default categories for user {userId}");
    }

    /// <summary>
    /// Get all categories with spending info for user
    /// </summary>
    public async Task<List<CategoryDto>> GetCategoriesWithSpendingAsync(int userId, int? year = null, int? month = null)
    {
        var now = DateTime.UtcNow;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;

        var categories = await _db.Categories
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var result = new List<CategoryDto>();

        foreach (var cat in categories)
        {
            var spent = await _db.Transactions
                .Where(t => t.CategoryId == cat.Id
                    && t.TransactionDate.Year == targetYear
                    && t.TransactionDate.Month == targetMonth)
                .SumAsync(t => t.Amount);

            var transactionCount = await _db.Transactions
                .Where(t => t.CategoryId == cat.Id
                    && t.TransactionDate.Year == targetYear
                    && t.TransactionDate.Month == targetMonth)
                .CountAsync();

            result.Add(new CategoryDto
            {
                Id = cat.Id,
                Name = cat.Name,
                Color = cat.Color,
                MonthlyBudget = cat.MonthlyBudget,
                SpentThisMonth = spent,
                TransactionCount = transactionCount
            });
        }

        return result.OrderBy(c => c.Name).ToList();
    }

    /// <summary>
    /// Check if user owns category
    /// </summary>
    public async Task<bool> UserOwnsCategoryAsync(int userId, int categoryId)
    {
        return await _db.Categories.AnyAsync(c => c.Id == categoryId && c.UserId == userId);
    }
}