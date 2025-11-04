namespace BudgetBuddy.Services;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BudgetBuddy.Models;
using Microsoft.Extensions.Logging;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _ollamaBaseUrl;

    public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
    }

    // ✅ NOUVELLE signature avec catégories
    public async Task<OllamaOcrResponseDto> ProcessReceiptTextAsync(
        string rawText, 
        List<string> availableCategories,
        string? imageUrl = null)
    {
        try
        {
            var prompt = BuildOcrPrompt(rawText, availableCategories);

            var requestBody = new
            {
                model = "gpt-oss:120b-cloud",
                prompt = prompt,
                stream = false,
                options = new { temperature = 0.1 }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/generate", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"Ollama raw response: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Ollama API error: {response.StatusCode}");
                return CreateFallbackResponse(rawText, imageUrl);
            }

            try
            {
                using var doc = JsonDocument.Parse(responseContent.Trim());
                var responseString = doc.RootElement.GetProperty("response").GetString() ?? "{}";

                responseString = responseString.Replace("```json", "").Replace("```", "").Trim();

                if (responseString.StartsWith("\"") && responseString.EndsWith("\""))
                {
                    responseString = JsonSerializer.Deserialize<string>(responseString)!;
                }

                _logger.LogInformation($"Cleaned Ollama JSON response: {responseString}");

                return ParseOllamaResponse(responseString, rawText, imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ollama JSON parsing error: {ex.Message}");
                return CreateFallbackResponse(rawText, imageUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ollama processing error: {ex.Message}");
            return CreateFallbackResponse(rawText, imageUrl);
        }
    }

    // ✅ NOUVEAU prompt avec catégories
    private string BuildOcrPrompt(string rawText, List<string> availableCategories)
    {
        var categoriesJson = JsonSerializer.Serialize(availableCategories);
        
        return $@"
Analyze this receipt text and extract the following fields.
Return ONLY valid JSON with no extra text.

RECEIPT TEXT:
{rawText}

AVAILABLE CATEGORIES:
{categoriesJson}

REQUIRED FORMAT (exact JSON structure):
{{
  ""amount"": <number or null>,
  ""currency"": ""<3-letter code or null>"",
  ""description"": ""<store name or main item>"",
  ""date"": ""DD-MM-YYYY or null"",
  ""categoryName"": ""<exact match from available categories>""
}}

RULES:
- amount: Total amount paid as a number (e.g., 8.10). Use dot for decimals. Return null if not found.
- currency: 3-letter ISO code (USD, EUR, GBP, etc.). Return null if not found, default to EUR.
- description: Concise description of the purchase (store name or main items). Max 200 chars. Never null.
- date: Transaction date in DD-MM-YYYY format. Return null if not found.
- categoryName: Choose the MOST appropriate category from the available list based on the receipt context.
  * For restaurants, fast food, cafes, groceries → ""Food & Dining""
  * For Uber, taxis, gas, parking → ""Transportation""
  * For clothing, electronics, general shopping → ""Shopping""
  * For movies, games, subscriptions → ""Entertainment""
  * For doctor, pharmacy, medical → ""Healthcare""
  * For rent, mortgage, utilities → ""Housing""
  * For electricity, water, internet → ""Utilities""
  * For books, courses, tuition → ""Education""
  * For hotels, flights, tourism → ""Travel""
  * For haircut, cosmetics, gym → ""Personal Care""
  * For charity, presents → ""Gifts & Donations""
  * For anything unclear → ""Miscellaneous""
  
  IMPORTANT: Return the EXACT category name from the available list. If no match, return ""Miscellaneous"".

CRITICAL: Return ONLY the JSON object. No markdown, no explanation, no extra text.";
    }

    private OllamaOcrResponseDto ParseOllamaResponse(string ollamaResponse, string rawText, string? imageUrl)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(ollamaResponse);
            var root = jsonDoc.RootElement;

            var result = new OllamaOcrResponseDto
            {
                RawText = rawText,
                ReceiptImageUrl = imageUrl
            };

            // Parse amount
            if (root.TryGetProperty("amount", out var amountElement))
            {
                if (amountElement.ValueKind == JsonValueKind.Number)
                {
                    result.Amount = amountElement.GetDecimal();
                }
                else if (amountElement.ValueKind == JsonValueKind.String)
                {
                    var amountStr = amountElement.GetString()?.Replace(",", ".").Trim();
                    if (!string.IsNullOrEmpty(amountStr) && 
                        decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, 
                                       System.Globalization.CultureInfo.InvariantCulture, out var dec))
                    {
                        result.Amount = dec;
                    }
                }
            }

            // Parse currency
            if (root.TryGetProperty("currency", out var currencyElement))
            {
                result.Currency = currencyElement.GetString()?.ToUpper();
            }
            
            if (string.IsNullOrEmpty(result.Currency))
            {
                result.Currency = "EUR";
            }

            // Parse description
            if (root.TryGetProperty("description", out var descElement))
            {
                result.Description = descElement.GetString();
            }

            if (!string.IsNullOrEmpty(result.Description) && result.Description.Length > 200)
            {
                result.Description = result.Description[..200];
            }

            if (string.IsNullOrEmpty(result.Description))
            {
                result.Description = "Purchase receipt";
            }

            // Parse date
            if (root.TryGetProperty("date", out var dateElement))
            {
                result.Date = dateElement.GetString();
            }

            if (string.IsNullOrEmpty(result.Date))
            {
                result.Date = ParseDateFromText(rawText);
            }

            if (string.IsNullOrEmpty(result.Date))
            {
                result.Date = DateTime.Now.ToString("dd-MM-yyyy");
            }

            // ✅ NOUVEAU : Parse category
            if (root.TryGetProperty("categoryName", out var categoryElement))
            {
                result.CategoryName = categoryElement.GetString();
            }

            _logger.LogInformation($"✅ Parsed receipt: Amount={result.Amount}, Currency={result.Currency}, Description={result.Description}, Date={result.Date}, Category={result.CategoryName}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to parse Ollama response DTO: {ex.Message}\nResponse: {ollamaResponse}");
            return CreateFallbackResponse(rawText, imageUrl);
        }
    }

    private string? ParseDateFromText(string rawText)
    {
        var datePatterns = new[]
        {
            @"\b(\d{1,2})[./-](\d{1,2})[./-](\d{2,4})\b",
            @"\b(\d{4})[./-](\d{1,2})[./-](\d{1,2})\b",
            @"\b(\d{1,2})\s+([A-Za-zéû]+)\s+(\d{4})\b"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(rawText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                try
                {
                    int day, month, year;

                    if (pattern == datePatterns[2])
                    {
                        day = int.Parse(match.Groups[1].Value);
                        month = ParseMonthName(match.Groups[2].Value);
                        year = int.Parse(match.Groups[3].Value);
                    }
                    else if (pattern == datePatterns[0])
                    {
                        day = int.Parse(match.Groups[1].Value);
                        month = int.Parse(match.Groups[2].Value);
                        year = int.Parse(match.Groups[3].Value);
                        
                        if (year < 100)
                            year += year < 50 ? 2000 : 1900;
                    }
                    else
                    {
                        year = int.Parse(match.Groups[1].Value);
                        month = int.Parse(match.Groups[2].Value);
                        day = int.Parse(match.Groups[3].Value);
                    }

                    if (month >= 1 && month <= 12 && day >= 1 && day <= 31 && year >= 2000 && year <= 2100)
                    {
                        return $"{day:D2}-{month:D2}-{year}";
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        return null;
    }

    private int ParseMonthName(string monthName)
    {
        var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"january",1}, {"jan",1}, {"janvier",1},
            {"february",2}, {"feb",2}, {"février",2}, {"fev",2},
            {"march",3}, {"mar",3}, {"mars",3},
            {"april",4}, {"apr",4}, {"avril",4}, {"avr",4},
            {"may",5}, {"mai",5},
            {"june",6}, {"jun",6}, {"juin",6},
            {"july",7}, {"jul",7}, {"juillet",7},
            {"august",8}, {"aug",8}, {"août",8}, {"aout",8},
            {"september",9}, {"sep",9}, {"sept",9}, {"septembre",9},
            {"october",10}, {"oct",10}, {"octobre",10},
            {"november",11}, {"nov",11}, {"novembre",11},
            {"december",12}, {"dec",12}, {"décembre",12}, {"decembre",12}
        };
        
        return months.TryGetValue(monthName.ToLower(), out var m) ? m : 0;
    }

    private OllamaOcrResponseDto CreateFallbackResponse(string rawText, string? imageUrl)
    {
        return new OllamaOcrResponseDto
        {
            RawText = rawText,
            ReceiptImageUrl = imageUrl,
            Amount = null,
            Currency = "EUR",
            Description = "Purchase receipt",
            Date = DateTime.Now.ToString("dd-MM-yyyy"),
            CategoryName = "Miscellaneous" // ✅ Fallback category
        };
    }
}