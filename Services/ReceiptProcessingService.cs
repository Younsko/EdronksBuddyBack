namespace BudgetBuddy.Services;

using BudgetBuddy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class ReceiptProcessingService
{
    private readonly OcrService _ocrService;
    private readonly OllamaService _ollamaService;
    private readonly AppDbContext _db;
    private readonly ILogger<ReceiptProcessingService> _logger;

    public ReceiptProcessingService(
        OcrService ocrService, 
        OllamaService ollamaService,
        AppDbContext db,
        ILogger<ReceiptProcessingService> logger)
    {
        _ocrService = ocrService;
        _ollamaService = ollamaService;
        _db = db;
        _logger = logger;
    }

    // ✅ NOUVELLE signature avec userId pour récupérer les catégories
    public async Task<OllamaOcrResponseDto> ProcessReceiptAsync(string imageUrl, int userId)
    {
        _logger.LogInformation($"Starting receipt processing for image: {imageUrl}");

        try
        {
            // Step 1: Extract text using OCR.space
            var rawText = await _ocrService.ExtractTextFromImageAsync(imageUrl);
            
            if (string.IsNullOrEmpty(rawText))
            {
                _logger.LogWarning("No text extracted from image");
                return new OllamaOcrResponseDto
                {
                    RawText = null,
                    ReceiptImageUrl = imageUrl,
                    Amount = null,
                    Currency = null,
                    Description = "No text found in receipt",
                    Date = DateTime.Now.ToString("dd-MM-yyyy"),
                    CategoryName = "Miscellaneous"
                };
            }

            _logger.LogInformation($"Successfully extracted {rawText.Length} characters from receipt");

            // ✅ Step 1.5: Get user's categories
            var userCategories = await _db.Categories
                .Where(c => c.UserId == userId)
                .Select(c => c.Name)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {userCategories.Count} categories for user {userId}");

            // Step 2: Process with Ollama + categories
            var result = await _ollamaService.ProcessReceiptTextAsync(rawText, userCategories, imageUrl);
            
            _logger.LogInformation($"Ollama processing completed - Amount: {result.Amount}, Currency: {result.Currency}, Category: {result.CategoryName}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Receipt processing error: {ex.Message}");
            throw;
        }
    }
}