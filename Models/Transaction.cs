namespace BudgetBuddy.Models;

public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public int? CategoryId { get; set; } // <- nullable ici
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = string.Empty;
    public string? ReceiptImageUrl { get; set; } // Stocke le base64 de l'image
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Category? Category { get; set; } // <- nullable aussi
}