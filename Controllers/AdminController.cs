using Gamesgiftshop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamesgiftshop.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly string[] AssignableRoles = ["User", "Staff", "Manager", "Accountant", "Admin"];

    private readonly Csi402dbContext _db;

    public AdminController(Csi402dbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        var roleCounts = _db.Users
            .AsNoTracking()
            .GroupBy(user => user.Role)
            .Select(group => new { Role = group.Key, Count = group.Count() })
            .ToDictionary(group => group.Role, group => group.Count);

        int CountRole(string role) => roleCounts.GetValueOrDefault(role);

        var model = new AdminDashboardViewModel
        {
            TotalUsers = roleCounts.Values.Sum(),
            UserCount = CountRole("User"),
            ManagerCount = CountRole("Manager"),
            StaffCount = CountRole("Staff"),
            AccountantCount = CountRole("Accountant"),
            AdminCount = CountRole("Admin"),
            ProductCount = _db.Products.AsNoTracking().Count(),
            TotalOrders = _db.Orders.AsNoTracking().Count(),
            AwaitingReviewOrders = _db.Orders.AsNoTracking().Count(order =>
                order.Status == "รอตรวจสอบสลิป" || order.Status == "รอดำเนินการ"),
            ReadyToPackOrders = _db.Orders.AsNoTracking().Count(order => order.Status == "ชำระเงินสำเร็จแล้ว"),
            PreparingShipmentOrders = _db.Orders.AsNoTracking().Count(order => order.Status == "เตรียมจัดส่ง"),
            ShippedOrders = _db.Orders.AsNoTracking().Count(order => order.Status == "จัดส่งแล้ว"),
            PaidRevenue = _db.Orders
                .AsNoTracking()
                .Where(order => order.Status == "ชำระเงินสำเร็จแล้ว" ||
                    order.Status == "เตรียมจัดส่ง" ||
                    order.Status == "จัดส่งแล้ว" ||
                    order.Status == "สำเร็จ")
                .Select(order => (decimal?)order.TotalAmount)
                .Sum() ?? 0m
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Users(string? search, string? role)
    {
        var usersQuery = _db.Users.AsNoTracking();
        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrEmpty(trimmedSearch))
        {
            usersQuery = usersQuery.Where(user =>
                user.Username.Contains(trimmedSearch) || user.Email.Contains(trimmedSearch));
        }

        if (role != null && AssignableRoles.Contains(role))
            usersQuery = usersQuery.Where(user => user.Role == role);

        var users = usersQuery.OrderBy(user => user.Username).ToList();
        var orderCounts = _db.Orders.AsNoTracking()
            .GroupBy(order => order.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionary(group => group.UserId, group => group.Count);

        return View(new AdminUsersViewModel
        {
            Search = trimmedSearch,
            RoleFilter = role != null && AssignableRoles.Contains(role) ? role : null,
            CurrentUsername = User.Identity?.Name,
            Users = users.Select(user => new AdminUserRow
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                OrderCount = orderCounts.GetValueOrDefault(user.UserId)
            }).ToList()
        });
    }

    [HttpGet]
    public IActionResult CreateUser(string? search, string? roleFilter)
    {
        return View("~/Views/Shared/UserManagementForm.cshtml", new UserManagementFormViewModel
        {
            Role = "User",
            AvailableRoles = AssignableRoles.ToList(),
            Search = search,
            RoleFilter = roleFilter
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateUser(UserManagementFormViewModel model)
    {
        model.AvailableRoles = AssignableRoles.ToList();
        model.IsEdit = false;
        model.Username = model.Username?.Trim() ?? "";
        model.Email = model.Email?.Trim() ?? "";
        model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            ModelState.AddModelError(nameof(model.Password), "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร");
        if (!model.AvailableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "สิทธิ์ที่เลือกไม่ถูกต้อง");
        if (_db.Users.Any(user => user.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "ชื่อผู้ใช้นี้มีผู้ใช้งานแล้ว");
        if (_db.Users.Any(user => user.Email == model.Email))
            ModelState.AddModelError(nameof(model.Email), "อีเมลนี้มีผู้ใช้งานแล้ว");

        if (!ModelState.IsValid)
            return View("~/Views/Shared/UserManagementForm.cshtml", model);

        _db.Users.Add(new User
        {
            Username = model.Username,
            Email = model.Email,
            Phone = model.Phone,
            Password = model.Password!,
            Role = model.Role
        });
        _db.SaveChanges();

        TempData["AdminUserSuccess"] = "เพิ่มบัญชีผู้ใช้เรียบร้อยแล้ว";
        return RedirectToAction(nameof(Users), new { search = model.Search, role = model.RoleFilter });
    }

    [HttpGet]
    public IActionResult EditUser(int id, string? search, string? roleFilter)
    {
        var user = _db.Users.AsNoTracking().FirstOrDefault(item => item.UserId == id);
        if (user == null)
            return NotFound();
        if (user.Username == User.Identity?.Name)
        {
            TempData["AdminUserError"] = "ไม่สามารถแก้ไขบัญชีที่กำลังใช้งานอยู่ได้";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }

        return View("~/Views/Shared/UserManagementForm.cshtml", new UserManagementFormViewModel
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            IsEdit = true,
            AvailableRoles = AssignableRoles.ToList(),
            Search = search,
            RoleFilter = roleFilter
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditUser(UserManagementFormViewModel model)
    {
        var user = _db.Users.FirstOrDefault(item => item.UserId == model.UserId);
        if (user == null)
            return NotFound();
        if (user.Username == User.Identity?.Name)
        {
            TempData["AdminUserError"] = "ไม่สามารถแก้ไขบัญชีที่กำลังใช้งานอยู่ได้";
            return RedirectToAction(nameof(Users), new { search = model.Search, role = model.RoleFilter });
        }

        model.IsEdit = true;
        model.AvailableRoles = AssignableRoles.ToList();
        model.Username = model.Username?.Trim() ?? "";
        model.Email = model.Email?.Trim() ?? "";
        model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

        if (!string.IsNullOrEmpty(model.Password) && model.Password.Length < 6)
            ModelState.AddModelError(nameof(model.Password), "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร");
        if (!model.AvailableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "สิทธิ์ที่เลือกไม่ถูกต้อง");
        if (_db.Users.Any(item => item.UserId != model.UserId && item.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "ชื่อผู้ใช้นี้มีผู้ใช้งานแล้ว");
        if (_db.Users.Any(item => item.UserId != model.UserId && item.Email == model.Email))
            ModelState.AddModelError(nameof(model.Email), "อีเมลนี้มีผู้ใช้งานแล้ว");
        if (user.Role == "Admin" && model.Role != "Admin" &&
            _db.Users.Count(item => item.Role == "Admin") <= 1)
            ModelState.AddModelError(nameof(model.Role), "ต้องมีบัญชี Admin อย่างน้อยหนึ่งบัญชี");

        if (!ModelState.IsValid)
            return View("~/Views/Shared/UserManagementForm.cshtml", model);

        var roleChanged = user.Role != model.Role;
        user.Username = model.Username;
        user.Email = model.Email;
        user.Phone = model.Phone;
        user.Role = model.Role;
        if (!string.IsNullOrEmpty(model.Password))
            user.Password = model.Password;
        _db.SaveChanges();

        TempData["AdminUserSuccess"] = roleChanged
            ? "แก้ไขบัญชีเรียบร้อยแล้ว ผู้ใช้ต้องเข้าสู่ระบบใหม่เพื่อรับสิทธิ์ใหม่"
            : "แก้ไขบัญชีผู้ใช้เรียบร้อยแล้ว";
        return RedirectToAction(nameof(Users), new { search = model.Search, role = model.RoleFilter });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteUser(int userId, string? search, string? roleFilter)
    {
        var user = _db.Users.FirstOrDefault(item => item.UserId == userId);
        if (user == null)
            return NotFound();
        if (user.Username == User.Identity?.Name)
        {
            TempData["AdminUserError"] = "ไม่สามารถลบบัญชีที่กำลังใช้งานอยู่ได้";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }
        if (user.Role == "Admin" && _db.Users.Count(item => item.Role == "Admin") <= 1)
        {
            TempData["AdminUserError"] = "ต้องมีบัญชี Admin อย่างน้อยหนึ่งบัญชี";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }
        if (_db.Orders.AsNoTracking().Any(order => order.UserId == user.UserId))
        {
            TempData["AdminUserError"] = "ลบบัญชีนี้ไม่ได้ เนื่องจากมีประวัติคำสั่งซื้อ ระบบจะเก็บบัญชีไว้เพื่อรักษาประวัติ";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }

        var addresses = _db.Useraddresses.Where(address => address.UserId == user.UserId).ToList();
        _db.Useraddresses.RemoveRange(addresses);
        _db.Users.Remove(user);
        _db.SaveChanges();

        TempData["AdminUserSuccess"] = "ลบบัญชีผู้ใช้เรียบร้อยแล้ว";
        return RedirectToAction(nameof(Users), new { search, role = roleFilter });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateUserRole(int userId, string newRole, string? search, string? roleFilter)
    {
        if (!AssignableRoles.Contains(newRole))
            return BadRequest();

        var user = _db.Users.FirstOrDefault(item => item.UserId == userId);
        if (user == null)
            return NotFound();

        if (user.Username == User.Identity?.Name)
        {
            TempData["AdminUserError"] = "ไม่สามารถเปลี่ยนสิทธิ์บัญชีที่กำลังใช้งานอยู่ได้";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }

        if (user.Role == "Admin" && newRole != "Admin" &&
            _db.Users.Count(item => item.Role == "Admin") <= 1)
        {
            TempData["AdminUserError"] = "ต้องมีบัญชี Admin อย่างน้อยหนึ่งบัญชี";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }

        user.Role = newRole;
        _db.SaveChanges();

        TempData["AdminUserSuccess"] = "อัปเดตสิทธิ์บัญชีเรียบร้อยแล้ว ผู้ใช้จะได้รับสิทธิ์ใหม่เมื่อเข้าสู่ระบบอีกครั้ง";
        return RedirectToAction(nameof(Users), new { search, role = roleFilter });
    }
}
