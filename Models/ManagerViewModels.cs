namespace Gamesgiftshop.Models;

public class SalesReportViewModel
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int PaidOrderCount { get; set; }

    public decimal NetSales { get; set; }

    public decimal AverageOrderAmount { get; set; }

    public List<DailySalesReportRow> DailySales { get; set; } = new();

    public List<TopProductSalesRow> TopProducts { get; set; } = new();
}

public class DailySalesReportRow
{
    public DateTime Date { get; set; }

    public int OrderCount { get; set; }

    public decimal NetSales { get; set; }
}

public class TopProductSalesRow
{
    public string ProductName { get; set; } = "";

    public int QuantitySold { get; set; }

    public decimal GrossSales { get; set; }
}

public class ManagerUsersViewModel
{
    public string? Search { get; set; }

    public string? RoleFilter { get; set; }

    public string? CurrentUsername { get; set; }

    public List<ManagerUserRow> Users { get; set; } = new();
}

public class ManagerUserRow
{
    public int UserId { get; set; }

    public string Username { get; set; } = "";

    public string Email { get; set; } = "";

    public string? Phone { get; set; }

    public string Role { get; set; } = "";

    public int OrderCount { get; set; }
}
