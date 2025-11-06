namespace BudgetBuddy.Models;
public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CategoryId { get; set; }

    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = "PHP";
    public decimal AmountPHP { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PHP";

    public string Description { get; set; } = string.Empty;
    public string? ReceiptImageUrl { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Category? Category { get; set; }
}