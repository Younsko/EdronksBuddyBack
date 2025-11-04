namespace BudgetBuddy.Services;

using BudgetBuddy.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

public class OcrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OcrService> _logger;
    private readonly string _ocrSpaceApiKey;
    private readonly string _language;

    public OcrService(HttpClient httpClient, ILogger<OcrService> logger, IOptions<OcrSpaceSettings> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ocrSpaceApiKey = options.Value.ApiKey ?? throw new ArgumentNullException(nameof(options.Value.ApiKey));
        _language = options.Value.Language ?? "eng";
    }

    /// <summary>
    /// Extract text from receipt via URL
    /// </summary>
    public async Task<string> ExtractTextFromImageAsync(string imageUrl)
    {
        try
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(_ocrSpaceApiKey), "apikey" },
                { new StringContent(imageUrl), "url" },
                { new StringContent(_language), "language" },
                { new StringContent("true"), "isOverlayRequired" },
                { new StringContent("2"), "OCREngine" }
            };

            var response = await _httpClient.PostAsync("https://api.ocr.space/parse/image", formData);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"OCR.space API error: {response.StatusCode}");
                return string.Empty;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return ParseOcrResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError($"OCR.space URL processing error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Extract text from uploaded file (base64 or stream)
    /// </summary>
    public async Task<string> ExtractTextFromFileAsync(IFormFile file)
    {
        try
        {
            // Vérifier le type de fichier
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/tiff", "image/bmp", "application/pdf" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                _logger.LogWarning($"Unsupported file type: {file.ContentType}");
                return string.Empty;
            }

            // Vérifier la taille (max 5MB pour OCR.space)
            if (file.Length > 5 * 1024 * 1024)
            {
                _logger.LogWarning($"File too large: {file.Length} bytes");
                return string.Empty;
            }

            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(_ocrSpaceApiKey), "apikey");
            formData.Add(new StringContent(_language), "language");
            formData.Add(new StringContent("true"), "isOverlayRequired");
            formData.Add(new StringContent("2"), "OCREngine");

            // Ajouter le fichier
            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            formData.Add(fileContent, "file", file.FileName);

            var response = await _httpClient.PostAsync("https://api.ocr.space/parse/image", formData);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"OCR.space API error: {response.StatusCode}");
                return string.Empty;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return ParseOcrResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError($"OCR.space file processing error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Extract text from base64 image
    /// </summary>
    public async Task<string> ExtractTextFromBase64Async(string base64Image)
    {
        try
        {
            // Valider le format base64
            if (!base64Image.StartsWith("data:"))
            {
                _logger.LogWarning("Invalid base64 format - missing data: prefix");
                return string.Empty;
            }

            var formData = new MultipartFormDataContent
            {
                { new StringContent(_ocrSpaceApiKey), "apikey" },
                { new StringContent(base64Image), "base64Image" },
                { new StringContent(_language), "language" },
                { new StringContent("true"), "isOverlayRequired" },
                { new StringContent("2"), "OCREngine" }
            };

            var response = await _httpClient.PostAsync("https://api.ocr.space/parse/image", formData);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"OCR.space API error: {response.StatusCode}");
                return string.Empty;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return ParseOcrResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError($"OCR.space base64 processing error: {ex.Message}");
            return string.Empty;
        }
    }

    private string ParseOcrResponse(string responseContent)
    {
        try
        {
            var ocrResult = JsonSerializer.Deserialize<OcrSpaceResponse>(responseContent);

            if (ocrResult?.IsErroredOnProcessing == true)
            {
                _logger.LogError($"OCR processing error: {ocrResult.ErrorMessage}");
                return string.Empty;
            }

            var extractedText = ocrResult?.ParsedResults?.FirstOrDefault()?.ParsedText;

            if (!string.IsNullOrEmpty(extractedText))
            {
                _logger.LogInformation($"OCR extracted text length: {extractedText.Length}");
                return extractedText;
            }

            _logger.LogWarning("No text found in OCR result");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError($"OCR response parsing error: {ex.Message}");
            return string.Empty;
        }
    }

    // Classes internes pour la désérialisation
    private class OcrSpaceResponse
    {
        public List<OcrParsedResult> ParsedResults { get; set; } = new();
        public int OCRExitCode { get; set; }
        public bool IsErroredOnProcessing { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private class OcrParsedResult
    {
        public string ParsedText { get; set; } = string.Empty;
        public int FileParseExitCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}