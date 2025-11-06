namespace BudgetBuddy.Models;

public class CategoryMonthlyBudget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal BudgetAmount { get; set; }
    public string Currency { get; set; } = "PHP";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}