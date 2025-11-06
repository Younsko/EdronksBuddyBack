namespace BudgetBuddy.Services;

using BudgetBuddy.Models;
using Microsoft.EntityFrameworkCore;

public class MonthlyBudgetService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MonthlyBudgetService> _logger;

    public MonthlyBudgetService(AppDbContext db, ILogger<MonthlyBudgetService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<CategoryMonthlyBudget>> InitializeMonthlyBudgetsAsync(int userId, int year, int month)
    {
        var existing = await _db.CategoryMonthlyBudgets
            .Where(b => b.UserId == userId && b.Year == year && b.Month == month)
            .ToListAsync();

        if (existing.Any())
        {
            _logger.LogInformation($"Monthly budgets already exist for user {userId} - {year}/{month}");
            return existing;
        }

        var categories = await _db.Categories
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (!categories.Any())
        {
            _logger.LogWarning($"No categories found for user {userId}");
            return new List<CategoryMonthlyBudget>();
        }

        var (prevYear, prevMonth) = GetPreviousMonth(year, month);
        var previousBudgets = await _db.CategoryMonthlyBudgets
            .Where(b => b.UserId == userId && b.Year == prevYear && b.Month == prevMonth)
            .ToListAsync();

        var newBudgets = new List<CategoryMonthlyBudget>();

        if (previousBudgets.Any())
        {
            foreach (var prevBudget in previousBudgets)
            {
                newBudgets.Add(new CategoryMonthlyBudget
                {
                    UserId = userId,
                    CategoryId = prevBudget.CategoryId,
                    Year = year,
                    Month = month,
                    BudgetAmount = prevBudget.BudgetAmount,
                    Currency = prevBudget.Currency,
                    CreatedAt = DateTime.UtcNow
                });
            }
            _logger.LogInformation($"Cloned {newBudgets.Count} budgets from {prevYear}/{prevMonth} to {year}/{month}");
        }
        else
        {
            foreach (var category in categories)
            {
                newBudgets.Add(new CategoryMonthlyBudget
                {
                    UserId = userId,
                    CategoryId = category.Id,
                    Year = year,
                    Month = month,
                    BudgetAmount = category.MonthlyBudget,
                    Currency = "PHP",
                    CreatedAt = DateTime.UtcNow
                });
            }
            _logger.LogInformation($"Initialized {newBudgets.Count} budgets for {year}/{month} from categories");
        }

        _db.CategoryMonthlyBudgets.AddRange(newBudgets);
        await _db.SaveChangesAsync();

        return newBudgets;
    }

    public async Task<List<MonthlyBudgetDto>> GetMonthlyBudgetsAsync(int userId, int year, int month)
    {
        await InitializeMonthlyBudgetsAsync(userId, year, month);

        var budgets = await _db.CategoryMonthlyBudgets
            .Where(b => b.UserId == userId && b.Year == year && b.Month == month)
            .Include(b => b.Category)
            .ToListAsync();

        var result = new List<MonthlyBudgetDto>();

        foreach (var budget in budgets)
        {
            var spent = await _db.Transactions
                .Where(t => t.UserId == userId 
                    && t.CategoryId == budget.CategoryId
                    && t.TransactionDate.Year == year
                    && t.TransactionDate.Month == month)
                .SumAsync(t => t.AmountPHP);

            result.Add(new MonthlyBudgetDto
            {
                CategoryId = budget.CategoryId,
                CategoryName = budget.Category.Name,
                CategoryColor = budget.Category.Color,
                BudgetAmount = budget.BudgetAmount,
                Currency = budget.Currency,
                Year = budget.Year,
                Month = budget.Month,
                SpentThisMonth = spent
            });
        }

        return result.OrderBy(b => b.CategoryName).ToList();
    }
    public async Task<CategoryMonthlyBudget?> UpdateMonthlyBudgetAsync(
        int userId, int categoryId, int year, int month, decimal newBudgetAmount)
    {
        var budget = await _db.CategoryMonthlyBudgets
            .FirstOrDefaultAsync(b => 
                b.UserId == userId 
                && b.CategoryId == categoryId 
                && b.Year == year 
                && b.Month == month);

        if (budget == null)
        {
            _logger.LogWarning($"Budget not found for user {userId}, category {categoryId}, {year}/{month}");
            return null;
        }

        budget.BudgetAmount = newBudgetAmount;
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Updated budget for user {userId}, category {categoryId}, {year}/{month} → {newBudgetAmount}");

        return budget;
    }

    private (int year, int month) GetPreviousMonth(int year, int month)
    {
        if (month == 1)
            return (year - 1, 12);
        else
            return (year, month - 1);
    }
}