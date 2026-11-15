namespace Gamesgiftshop.Models;

public sealed class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }

    public int UserCount { get; set; }

    public int ManagerCount { get; set; }

    public int StaffCount { get; set; }

    public int AccountantCount { get; set; }

    public int AdminCount { get; set; }

    public int ProductCount { get; set; }

    public int TotalOrders { get; set; }

    public int AwaitingReviewOrders { get; set; }

    public int ReadyToPackOrders { get; set; }

    public int PreparingShipmentOrders { get; set; }

    public int ShippedOrders { get; set; }

    public decimal PaidRevenue { get; set; }
}

public sealed class AdminUsersViewModel
{
    public string? Search { get; set; }

    public string? RoleFilter { get; set; }

    public string? CurrentUsername { get; set; }

    public List<AdminUserRow> Users { get; set; } = new();
}

public sealed class AdminUserRow
{
    public int UserId { get; set; }

    public string Username { get; set; } = "";

    public string Email { get; set; } = "";

    public string? Phone { get; set; }

    public string Role { get; set; } = "";

    public int OrderCount { get; set; }
}
