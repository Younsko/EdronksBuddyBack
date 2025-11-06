namespace BudgetBuddy.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetBuddy.Models;
using BudgetBuddy.Services;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OcrService _ocrService;
    private readonly CategoryService _categoryService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        AppDbContext db, 
        OcrService ocrService, 
        CategoryService categoryService,
        ILogger<TransactionsController> logger)
    {
        _db = db;
        _ocrService = ocrService;
        _categoryService = categoryService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

    // --- GET all transactions with pagination ---
    [HttpGet]
    [ProducesResponseType(typeof(List<TransactionDto>), 200)]
    public async Task<ActionResult<object>> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var userId = GetUserId();

        try
        {
            var query = _db.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Category);

            var total = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionDto
                {
                    Id = t.Id,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category != null ? t.Category.Name : null,
                    CategoryColor = t.Category != null ? t.Category.Color : null,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Description = t.Description,
                    ReceiptImageUrl = t.ReceiptImageUrl,
                    TransactionDate = t.TransactionDate.ToString("dd-MM-yyyy"),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                data = transactions,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get transactions error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while fetching transactions" });
        }
    }

    // --- GET single transaction ---
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TransactionDto>> GetTransaction(int id)
    {
        var userId = GetUserId();

        var transaction = await _db.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
            return NotFound(new { error = "Transaction not found" });

        if (transaction.UserId != userId)
            return Forbid();

        return Ok(new TransactionDto
        {
            Id = transaction.Id,
            CategoryId = transaction.CategoryId,
            CategoryName = transaction.Category?.Name,
            CategoryColor = transaction.Category?.Color,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Description = transaction.Description,
            ReceiptImageUrl = transaction.ReceiptImageUrl,
            TransactionDate = transaction.TransactionDate.ToString("dd-MM-yyyy"),
            CreatedAt = transaction.CreatedAt
        });
    }
/// <summary>
/// Process receipt image through OCR + Ollama pipeline
/// </summary>
[HttpPost("process-receipt")]
[ProducesResponseType(typeof(OllamaOcrResponseDto), 200)]
[ProducesResponseType(400)]
public async Task<ActionResult<OllamaOcrResponseDto>> ProcessReceipt([FromBody] OcrSpaceRequestDto request)
{
    if (string.IsNullOrEmpty(request.ImageUrl))
        return BadRequest(new { error = "Image URL is required" });

    try
    {
        var receiptProcessingService = HttpContext.RequestServices.GetRequiredService<ReceiptProcessingService>();
        var userId = GetUserId();
        var result = await receiptProcessingService.ProcessReceiptAsync(request.ImageUrl, userId);        
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Process receipt error: {ex.Message}");
        return StatusCode(500, new { error = "An error occurred while processing receipt" });
    }
}
// --- CREATE transaction ---
[HttpPost]
[ProducesResponseType(typeof(TransactionDto), 201)]
[ProducesResponseType(400)]
public async Task<ActionResult<TransactionDto>> CreateTransaction(TransactionCreateDto dto)
{
    _logger.LogInformation($"Received DTO - Date: {dto.Date}, Amount: {dto.Amount}, Description: {dto.Description}");

    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var userId = GetUserId();

    try
    {
        if (dto.CategoryId.HasValue && dto.CategoryId.Value > 0)
        {
            if (!await _categoryService.UserOwnsCategoryAsync(userId, dto.CategoryId.Value))
                return BadRequest(new { error = "Category not found or access denied" });
        }

        if (string.IsNullOrEmpty(dto.Date))
            return BadRequest(new { error = "Date is required" });

        if (!DateTime.TryParseExact(dto.Date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var transactionDate))
        {
            _logger.LogError($"❌ Failed to parse date: {dto.Date}");
            return BadRequest(new { error = "Invalid date format. Use DD-MM-YYYY" });
        }

        string? receiptImageUrl = null;
        decimal? ocrAmount = null;
        string? ocrCurrency = null;
        string? ocrDescription = null;
        int? ocrCategoryId = dto.CategoryId;

        if (!string.IsNullOrEmpty(dto.ReceiptImage))
        {
            receiptImageUrl = dto.ReceiptImage;

            var receiptProcessingService = HttpContext.RequestServices.GetRequiredService<ReceiptProcessingService>();
            var ocrResult = await receiptProcessingService.ProcessReceiptAsync(dto.ReceiptImage, userId);
            ocrAmount = ocrResult.Amount;
            ocrCurrency = ocrResult.Currency;
            ocrDescription = ocrResult.Description;

            if (!string.IsNullOrEmpty(ocrResult.CategoryName))
            {
                var category = await _db.Categories
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == ocrResult.CategoryName);

                if (category != null)
                {
                    ocrCategoryId = category.Id;
                    _logger.LogInformation($"Auto-assigned category: {category.Name} (ID: {category.Id})");
                }
            }
        }

        // ✅ Déterminer les valeurs finales
        var finalAmount = dto.Amount != 0 ? dto.Amount : ocrAmount ?? 0;
        var finalCurrency = !string.IsNullOrEmpty(dto.Currency) ? dto.Currency.ToUpper() : ocrCurrency ?? "PHP";
        var finalDescription = !string.IsNullOrEmpty(dto.Description) ? dto.Description : ocrDescription ?? "Purchase receipt";

        // ✅ NOUVEAU : Valider la devise
        var currencyService = HttpContext.RequestServices.GetRequiredService<CurrencyService>();
        if (!currencyService.IsValidCurrency(finalCurrency))
        {
            return BadRequest(new { error = $"Unsupported currency: {finalCurrency}. Supported: PHP, EUR, USD, GBP, CAD, CHF, JPY, AUD" });
        }

        var transaction = new Transaction
        {
            UserId = userId,
            CategoryId = ocrCategoryId,
            
            // ✅ NOUVEAU : Montants avec conversion
            OriginalAmount = finalAmount,
            OriginalCurrency = finalCurrency,
            AmountPHP = 0, // Sera calculé juste après
            
            // Compatibilité (déprécié)
            Amount = finalAmount,
            Currency = finalCurrency,
            
            Description = finalDescription,
            ReceiptImageUrl = receiptImageUrl,
            TransactionDate = transactionDate,
            CreatedAt = DateTime.UtcNow
        };

        // ✅ NOUVEAU : Convertir en PHP
        transaction.AmountPHP = await currencyService.ConvertAsync(
            transaction.OriginalAmount, 
            transaction.OriginalCurrency, 
            "PHP"
        );

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
        await _db.Entry(transaction).Reference(t => t.Category).LoadAsync();

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, new TransactionDto
        {
            Id = transaction.Id,
            CategoryId = transaction.CategoryId,
            CategoryName = transaction.Category?.Name,
            CategoryColor = transaction.Category?.Color,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Description = transaction.Description,
            ReceiptImageUrl = transaction.ReceiptImageUrl,
            TransactionDate = transaction.TransactionDate.ToString("dd-MM-yyyy"),
            CreatedAt = transaction.CreatedAt
        });
    }
    catch (Exception ex)
    {
        _logger.LogError($"Create transaction error: {ex.Message}");
        return StatusCode(500, new { error = "An error occurred while creating transaction" });
    }
}

    // --- UPDATE transaction ---
 [HttpPut("{id}")]
[ProducesResponseType(typeof(TransactionDto), 200)]
[ProducesResponseType(403)]
[ProducesResponseType(404)]
public async Task<ActionResult<TransactionDto>> UpdateTransaction(int id, TransactionCreateDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var userId = GetUserId();
    var transaction = await _db.Transactions
        .Include(t => t.Category)
        .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

    if (transaction == null)
        return NotFound(new { error = "Transaction not found or access denied" });

    try
    {
        if (dto.CategoryId.HasValue && dto.CategoryId.Value > 0)
        {
            if (!await _categoryService.UserOwnsCategoryAsync(userId, dto.CategoryId.Value))
                return BadRequest(new { error = "Category not found or access denied" });
        }

        var currencyService = HttpContext.RequestServices.GetRequiredService<CurrencyService>();
        if (!currencyService.IsValidCurrency(dto.Currency))
        {
            return BadRequest(new { error = $"Unsupported currency: {dto.Currency}" });
        }

        transaction.CategoryId = dto.CategoryId.HasValue && dto.CategoryId.Value > 0 ? dto.CategoryId.Value : null;
        
        transaction.OriginalAmount = dto.Amount;
        transaction.OriginalCurrency = dto.Currency.ToUpper();
        transaction.AmountPHP = await currencyService.ConvertAsync(dto.Amount, dto.Currency.ToUpper(), "PHP");
        transaction.Amount = dto.Amount;
        transaction.Currency = dto.Currency.ToUpper();
        transaction.Description = dto.Description;

        if (string.IsNullOrEmpty(dto.Date))
            return BadRequest(new { error = "Date is required" });

        if (!DateTime.TryParseExact(dto.Date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
            return BadRequest(new { error = "Invalid date format. Use DD-MM-YYYY" });

        transaction.TransactionDate = parsedDate;

        if (!string.IsNullOrEmpty(dto.ReceiptImage))
            transaction.ReceiptImageUrl = dto.ReceiptImage;

        await _db.SaveChangesAsync();

        return Ok(new TransactionDto
        {
            Id = transaction.Id,
            CategoryId = transaction.CategoryId,
            CategoryName = transaction.Category?.Name,
            CategoryColor = transaction.Category?.Color,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Description = transaction.Description,
            ReceiptImageUrl = transaction.ReceiptImageUrl,
            TransactionDate = transaction.TransactionDate.ToString("dd-MM-yyyy"),
            CreatedAt = transaction.CreatedAt
        });
    }
    catch (Exception ex)
    {
        _logger.LogError($"Update transaction error: {ex.Message}");
        return StatusCode(500, new { error = "An error occurred while updating transaction" });
    }
}

    // --- DELETE transaction ---
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var userId = GetUserId();

        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (transaction == null)
            return NotFound(new { error = "Transaction not found or access denied" });

        try
        {
            _db.Transactions.Remove(transaction);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Transaction deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Delete transaction error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while deleting transaction" });
        }
    }

    // --- GET transactions by month ---
    [HttpGet("month/{year}/{month}")]
    [ProducesResponseType(typeof(List<TransactionDto>), 200)]
    public async Task<ActionResult<object>> GetTransactionsByMonth(int year, int month, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (year < 2000 || year > 2100) 
            return BadRequest(new { error = "Invalid year" });
        if (month < 1 || month > 12) 
            return BadRequest(new { error = "Invalid month" });

        var userId = GetUserId();

        try
        {
            var query = _db.Transactions
                .Where(t => t.UserId == userId
                            && t.TransactionDate.Year == year
                            && t.TransactionDate.Month == month)
                .Include(t => t.Category);

            var total = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionDto
                {
                    Id = t.Id,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category != null ? t.Category.Name : null,
                    CategoryColor = t.Category != null ? t.Category.Color : null,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Description = t.Description,
                    ReceiptImageUrl = t.ReceiptImageUrl,
                    TransactionDate = t.TransactionDate.ToString("dd-MM-yyyy"),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                data = transactions,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get transactions by month error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while fetching transactions" });
        }
    }

    // --- OCR Preview by URL ---
    [HttpPost("ocr-preview")]
    [ProducesResponseType(typeof(OcrResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<OcrResponseDto>> OcrPreview([FromBody] OcrPreviewRequest request)
    {
        if (string.IsNullOrEmpty(request.ImageUrl))
            return BadRequest(new { error = "Image URL required" });

        try
        {
var receiptProcessingService = HttpContext.RequestServices.GetRequiredService<ReceiptProcessingService>();
            var userId = GetUserId();
            var result = await receiptProcessingService.ProcessReceiptAsync(request.ImageUrl, userId);      
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"OCR preview error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred during OCR processing" });
        }
    }
}

// --- Request model for OCR preview ---
public class OcrPreviewRequest
{
    public string ImageUrl { get; set; } = string.Empty;
}
