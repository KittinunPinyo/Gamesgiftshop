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

    public bool CanBootstrapAdmin { get; set; }

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

public class UserManagementFormViewModel
{
    public int UserId { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "กรุณากรอกชื่อผู้ใช้")]
    [System.ComponentModel.DataAnnotations.StringLength(50, ErrorMessage = "ชื่อผู้ใช้ต้องไม่เกิน 50 ตัวอักษร")]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "กรุณากรอกอีเมล")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    [System.ComponentModel.DataAnnotations.StringLength(100, ErrorMessage = "อีเมลต้องไม่เกิน 100 ตัวอักษร")]
    public string Email { get; set; } = "";

    [System.ComponentModel.DataAnnotations.StringLength(20, ErrorMessage = "เบอร์โทรศัพท์ต้องไม่เกิน 20 ตัวอักษร")]
    public string? Phone { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(255, ErrorMessage = "รหัสผ่านต้องไม่เกิน 255 ตัวอักษร")]
    public string? Password { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "กรุณาเลือกสิทธิ์")]
    public string Role { get; set; } = "User";

    public bool IsEdit { get; set; }

    public string? Search { get; set; }

    public string? RoleFilter { get; set; }

    public List<string> AvailableRoles { get; set; } = new();
}
