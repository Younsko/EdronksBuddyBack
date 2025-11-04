using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BudgetBuddy.Models;

public class RegisterDto
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers and underscores")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string PreferredCurrency { get; set; } = "EUR";
}

public class UserUpdateDto
{
    [StringLength(100, MinimumLength = 2)]
    public string? Name { get; set; }
    public string? ProfilePhotoUrl { get; set; }
}

// --- Categories ---
public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal MonthlyBudget { get; set; }
    public decimal SpentThisMonth { get; set; }
    public decimal RemainingBudget => MonthlyBudget - SpentThisMonth;
    public int TransactionCount { get; set; }
}

public class CategoryCreateDto
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be hex format (#RRGGBB)")]
    public string Color { get; set; } = "#000000";

    [Range(0, 1000000)]
    public decimal MonthlyBudget { get; set; }
}

// --- Transactions ---
public class TransactionDto
{
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReceiptImageUrl { get; set; }
    public string TransactionDate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TransactionCreateDto
{
    public int? CategoryId { get; set; }

    [Required]
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression(@"^[A-Z]{3}$")]
    public string Currency { get; set; } = "EUR";

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Date { get; set; } = string.Empty;
    
    public string? ReceiptImage { get; set; } // Base64
    public string? ReceiptImageUrl { get; set; } // URL
}

// --- OCR ---
public class OcrResponseDto
{
    public decimal? Amount { get; set; }       // decimal? pour calculs
    public string? Currency { get; set; }      // 3 lettres ISO
    public string? Description { get; set; }  // texte concis
    public string? Date { get; set; }         // format DD-MM-YYYY
    public string? RawText { get; set; }      // texte brut OCR
}

// --- Ollama OCR (multi-langues) ---
public class OllamaOcrRequestDto
{
    public string RawText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class OllamaOcrResponseDto
{

    public decimal? Amount { get; set; }
    public string? AmountRaw { get; set; }

    public string? Currency { get; set; }
    public string? Description { get; set; }
    public string? Date { get; set; }
    public string? RawText { get; set; }
    public string? ReceiptImageUrl { get; set; }
    public string? CategoryName { get; set; }
}
public class OcrPreviewRequest
{
    [Required]
    public string ImageUrl { get; set; } = string.Empty;
}

// --- OCR.space ---
public class OcrSpaceRequestDto
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? Language { get; set; } = "eng"; // permet multi-langues
    public bool IsOverlayRequired { get; set; } = true;
}

// --- Stats ---
public class StatsDto
{
    public decimal TotalSpentThisMonth { get; set; }
    public decimal TotalBudgetThisMonth { get; set; }
    public decimal RemainingBudget => TotalBudgetThisMonth - TotalSpentThisMonth;
    public decimal BudgetUsagePercentage => TotalBudgetThisMonth > 0
        ? (TotalSpentThisMonth / TotalBudgetThisMonth) * 100
        : 0;
    public int TotalTransactions { get; set; }
    public List<CategorySpendingDto> ByCategory { get; set; } = new();
    public List<CurrencySpendingDto> ByCurrency { get; set; } = new();
    public List<DailySpendingDto> DailySpending { get; set; } = new();
}

public class CategorySpendingDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Spent { get; set; }
    public decimal Budget { get; set; }
    public decimal Percentage => Budget > 0 ? (Spent / Budget) * 100 : 0;
    public int TransactionCount { get; set; }
}

public class CurrencySpendingDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ConvertedToPreferred { get; set; }
}

public class DailySpendingDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

// --- User profile ---
public class UserProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PreferredCurrency { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TotalCategories { get; set; }
    public int TotalTransactions { get; set; }
}

// --- Password / Delete ---
public class PasswordChangeDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class DeleteAccountDto
{
    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^DELETE_ZONE1$", ErrorMessage = "Must type DELETE_ZONE1 exactly")]
    public string Confirmation { get; set; } = string.Empty;
}

public class UserSettingsDto
{
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be 3-letter code (e.g. USD, EUR)")]
    public string? PreferredCurrency { get; set; }
}
