namespace Gamesgiftshop.Models;

public class AccountingExpense
{
    public int AccountingExpenseId { get; set; }

    public string Description { get; set; } = "";

    public string Category { get; set; } = "";

    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
