namespace Gamesgiftshop.Models;

public class AccountingDashboardViewModel
{
    public DateTime Month { get; set; }

    public int PaidOrderCount { get; set; }

    public decimal Revenue { get; set; }

    public decimal ExpenseTotal { get; set; }

    public decimal NetIncome { get; set; }

    public List<Order> SalesTransactions { get; set; } = new();

    public List<AccountingExpense> Expenses { get; set; } = new();
}
