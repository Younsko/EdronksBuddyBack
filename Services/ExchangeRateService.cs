namespace BudgetBuddy.Services;
using System.Net.Http.Json;

public class ExchangeRateService
{
    private readonly HttpClient _http;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(HttpClient http, ILogger<ExchangeRateService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<decimal> ConvertCurrency(decimal amount, string fromCurrency, string toCurrency)
    {
        if (fromCurrency == toCurrency) return amount;

        try
        {
            var url = $"https://api.exchangerate-api.com/v4/latest/{fromCurrency}";
            var response = await _http.GetFromJsonAsync<dynamic>(url);
            
            if (response?.rates != null && response.rates[toCurrency] != null)
            {
                decimal rate = (decimal)(double)response.rates[toCurrency];
                return amount * rate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exchange rate error: {ex.Message}");
        }

        return amount; // Fallback
    }
}