namespace BudgetBuddy.Models;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    
    public DbSet<CategoryMonthlyBudget> CategoryMonthlyBudgets { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes existants
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<Category>()
            .HasOne(c => c.User)
            .WithMany(u => u.Categories)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CategoryMonthlyBudget>()
            .HasIndex(b => new { b.UserId, b.CategoryId, b.Year, b.Month })
            .IsUnique();

        modelBuilder.Entity<CategoryMonthlyBudget>()
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CategoryMonthlyBudget>()
            .HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExchangeRate>()
            .HasIndex(e => new { e.FromCurrency, e.ToCurrency })
            .IsUnique();
    }
}