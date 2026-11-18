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

public sealed class AccountingPaymentViewModel
{
    public int PendingCount => Orders.Count;

    public List<AccountingPaymentOrder> Orders { get; set; } = new();
}

public sealed class AccountingPaymentOrder
{
    public int OrderId { get; set; }

    public DateTime? OrderDate { get; set; }

    public string CustomerName { get; set; } = "";

    public decimal TotalAmount { get; set; }

    public string? Status { get; set; }

    public string? PaymentSlipUrl { get; set; }

    public List<string> ProductSummary { get; set; } = new();
}
