namespace BudgetBuddy.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetBuddy.Models;
using BudgetBuddy.Services;

[ApiController]
[Route("api/budgets/monthly")]
[Authorize]
public class MonthlyBudgetsController : ControllerBase
{
    private readonly MonthlyBudgetService _budgetService;
    private readonly ILogger<MonthlyBudgetsController> _logger;

    public MonthlyBudgetsController(
        MonthlyBudgetService budgetService,
        ILogger<MonthlyBudgetsController> logger)
    {
        _budgetService = budgetService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

    [HttpGet("{year}/{month}")]
    [ProducesResponseType(typeof(List<MonthlyBudgetDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<List<MonthlyBudgetDto>>> GetMonthlyBudgets(int year, int month)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { error = "Invalid year" });
        
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Invalid month (must be 1-12)" });

        try
        {
            var userId = GetUserId();
            var budgets = await _budgetService.GetMonthlyBudgetsAsync(userId, year, month);
            return Ok(budgets);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get monthly budgets error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while fetching monthly budgets" });
        }
    }

    [HttpPost("init")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> InitializeMonthlyBudgets([FromBody] InitMonthlyBudgetsDto dto)
    {
        if (dto.Year < 2000 || dto.Year > 2100)
            return BadRequest(new { error = "Invalid year" });
        
        if (dto.Month < 1 || dto.Month > 12)
            return BadRequest(new { error = "Invalid month (must be 1-12)" });

        try
        {
            var userId = GetUserId();
            await _budgetService.InitializeMonthlyBudgetsAsync(userId, dto.Year, dto.Month);
            
            return Ok(new { 
                message = $"Monthly budgets initialized for {dto.Year}/{dto.Month}",
                year = dto.Year,
                month = dto.Month
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Initialize monthly budgets error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while initializing monthly budgets" });
        }
    }

    [HttpPut("{categoryId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateMonthlyBudget(int categoryId, [FromBody] UpdateMonthlyBudgetDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var now = DateTime.UtcNow;
            
            var updated = await _budgetService.UpdateMonthlyBudgetAsync(
                userId, categoryId, now.Year, now.Month, dto.BudgetAmount);

            if (updated == null)
                return NotFound(new { error = "Budget not found for current month" });

            return Ok(new { 
                message = "Budget updated successfully",
                categoryId = categoryId,
                year = updated.Year,
                month = updated.Month,
                newBudgetAmount = updated.BudgetAmount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update monthly budget error: {ex.Message}");
            return StatusCode(500, new { error = "An error occurred while updating budget" });
        }
    }
}