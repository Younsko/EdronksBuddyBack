using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Text;
using BudgetBuddy.Services;
using BudgetBuddy.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env (si présent)
if (File.Exists(".env"))
{
    foreach (var line in File.ReadAllLines(".env"))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

// Database - MySQL
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 33))
    )
);

// JWT Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? throw new Exception("JWT_SECRET not configured");
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "BudgetBuddy";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "BudgetBuddy";

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(opt => opt.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OcrService>(); 
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<OllamaService>();
builder.Services.AddScoped<ReceiptProcessingService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<MonthlyBudgetService>();
builder.Services.AddHttpClient(); 



// External services
builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<OcrSpaceSettings>(builder.Configuration.GetSection("OcrSpace"));

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Budget Buddy API", 
        Version = "v1",
        Description = "Expense tracking API for students worldwide"
    });

    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { securityScheme, new[] { "Bearer" } }
    });
});

var app = builder.Build();

// Vérification clé OCR
var ocrApiKey = builder.Configuration["OcrSpace:ApiKey"];
if (string.IsNullOrEmpty(ocrApiKey))
{
    Console.WriteLine("⚠️  WARNING: OCR.space API key not configured. OCR features will not work.");
}
else
{
    Console.WriteLine("✅ OCR.space API key configured successfully");
}

// Migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Budget Buddy API v1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
