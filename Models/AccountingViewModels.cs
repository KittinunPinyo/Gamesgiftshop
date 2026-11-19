namespace Gamesgiftshop.Models;

public class AccountingDashboardViewModel
{
    public string Period { get; set; } = "month";

    public string PeriodLabel { get; set; } = "";

    public string DateInputValue { get; set; } = "";

    public DateTime SelectedDate { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEndExclusive { get; set; }

    public int PaidOrderCount { get; set; }

    public decimal Revenue { get; set; }

    public decimal ExpenseTotal { get; set; }

    public decimal RefundTotal { get; set; }

    public decimal NetIncome { get; set; }

    public List<Order> SalesTransactions { get; set; } = new();

    public List<AccountingExpense> Expenses { get; set; } = new();

    public List<RefundRequest> RefundTransactions { get; set; } = new();

    public List<AccountingLedgerEntry> LedgerEntries { get; set; } = new();
}

public sealed class AccountingLedgerEntry
{
    public DateTime Date { get; set; }

    public string Type { get; set; } = "";

    public string Reference { get; set; } = "";

    public string Description { get; set; } = "";

    public decimal Amount { get; set; }
}

public sealed class AccountingRefundViewModel
{
    public List<RefundRequest> Requests { get; set; } = new();

    public int PendingCount => Requests.Count(request =>
        request.Status == RefundRequest.PendingStatus ||
        request.Status == RefundRequest.ReturnShippedStatus ||
        request.Status == RefundRequest.ReturnReceivedStatus);
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
