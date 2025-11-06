namespace BudgetBuddy.Services;

using BudgetBuddy.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

public class CurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly ILogger<CurrencyService> _logger;
    
    public static readonly string[] SupportedCurrencies = 
        { "PHP", "EUR", "USD", "GBP", "CAD", "CHF", "JPY", "AUD" };

    private static readonly Dictionary<string, (decimal Rate, DateTime Expiry)> _rateCache = new();
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

    public CurrencyService(HttpClient httpClient, AppDbContext db, ILogger<CurrencyService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;
    }

    public bool IsValidCurrency(string currency)
    {
        return SupportedCurrencies.Contains(currency?.ToUpper());
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency)
    {
        fromCurrency = fromCurrency.ToUpper();
        toCurrency = toCurrency.ToUpper();

        if (!IsValidCurrency(fromCurrency) || !IsValidCurrency(toCurrency))
        {
            _logger.LogWarning($"Invalid currency: {fromCurrency} → {toCurrency}");
            return amount;
        }

        if (fromCurrency == toCurrency)
            return amount;

        try
        {
            var rate = await GetRateAsync(fromCurrency, toCurrency);
            return amount * rate;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Conversion error {fromCurrency}→{toCurrency}: {ex.Message}");
            return amount; 
        }
    }

    private async Task<decimal> GetRateAsync(string from, string to)
    {
        var cacheKey = $"{from}_{to}";

        if (_rateCache.TryGetValue(cacheKey, out var cached) && cached.Expiry > DateTime.UtcNow)
        {
            _logger.LogDebug($"Using cached rate {from}→{to}: {cached.Rate}");
            return cached.Rate;
        }

        var dbRate = await _db.ExchangeRates
            .Where(r => r.FromCurrency == from && r.ToCurrency == to)
            .Where(r => r.LastUpdated > DateTime.UtcNow.AddHours(-1))
            .FirstOrDefaultAsync();

        if (dbRate != null)
        {
            _logger.LogDebug($"Using DB rate {from}→{to}: {dbRate.Rate}");
            _rateCache[cacheKey] = (dbRate.Rate, DateTime.UtcNow.Add(CacheExpiry));
            return dbRate.Rate;
        }

        var rate = await FetchRateFromApiAsync(from, to);

        await SaveRateToDbAsync(from, to, rate);

        _rateCache[cacheKey] = (rate, DateTime.UtcNow.Add(CacheExpiry));

        return rate;
    }

    private async Task<decimal> FetchRateFromApiAsync(string from, string to)
    {
        try
        {
            var url = $"https://api.exchangerate-api.com/v4/latest/{from}";
            var response = await _httpClient.GetFromJsonAsync<ExchangeRateApiResponse>(url);

            if (response?.Rates != null && response.Rates.TryGetValue(to, out var rate))
            {
                _logger.LogInformation($"Fetched rate {from}→{to}: {rate}");
                return (decimal)rate;
            }

            throw new Exception($"Rate not found for {from}→{to}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"API fetch error: {ex.Message}");
            throw;
        }
    }

    private async Task SaveRateToDbAsync(string from, string to, decimal rate)
    {
        try
        {
            var existing = await _db.ExchangeRates
                .FirstOrDefaultAsync(r => r.FromCurrency == from && r.ToCurrency == to);

            if (existing != null)
            {
                existing.Rate = rate;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                _db.ExchangeRates.Add(new ExchangeRate
                {
                    FromCurrency = from,
                    ToCurrency = to,
                    Rate = rate,
                    LastUpdated = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save rate to DB: {ex.Message}");
        }
    }

    public async Task UpdateAllRatesAsync()
    {
        _logger.LogInformation("Updating all exchange rates...");

        foreach (var from in SupportedCurrencies)
        {
            foreach (var to in SupportedCurrencies)
            {
                if (from != to)
                {
                    try
                    {
                        var rate = await FetchRateFromApiAsync(from, to);
                        await SaveRateToDbAsync(from, to, rate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to update {from}→{to}: {ex.Message}");
                    }
                }
            }
        }

        _logger.LogInformation("Exchange rates updated successfully");
    }

    private class ExchangeRateApiResponse
    {
        public Dictionary<string, double> Rates { get; set; } = new();
    }
}