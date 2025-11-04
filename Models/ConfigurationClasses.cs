namespace BudgetBuddy.Models;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "gpt-oss:120b-cloud";
}

public class OcrSpaceSettings
{
    public string ApiKey { get; set; } = string.Empty; 
    public string Language { get; set; } = "eng";
}
