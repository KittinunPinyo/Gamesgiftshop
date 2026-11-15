using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Gamesgiftshop.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Gamesgiftshop.Controllers
{
    public class AccountController : Controller
    {
        private readonly Csi402dbContext _db;

        public AccountController(Csi402dbContext db)
        {
            _db = db;
        }

        // ==========================================
        // 1. สมัครสมาชิก (Register)
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(User data)
        {
            // ⭐️ ดักจับตรวจสอบรหัสผ่าน ต้องมีอย่างน้อย 6 ตัวอักษร
            if (string.IsNullOrEmpty(data.Password) || data.Password.Length < 6)
            {
                ViewBag.Error = "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร";
                return View(data);
            }

            bool isDuplicate = _db.Users.Any(u => u.Username == data.Username || u.Email == data.Email);
            
            if (isDuplicate)
            {
                ViewBag.Error = "ชื่อผู้ใช้งานหรืออีเมลนี้มีผู้ใช้งานแล้ว กรุณาใช้ชื่ออื่น";
                return View(data);
            }

            data.Role = "User";
            _db.Users.Add(data);
            _db.SaveChanges();
            
            return RedirectToAction("Login");
        }

        // ==========================================
        // 2. เข้าสู่ระบบ (Login)
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string Username, string Password, bool RememberMe)
        {
            var user = _db.Users.FirstOrDefault(u => 
                (u.Username == Username || u.Email == Username) && 
                u.Password == Password);
            
            if (user == null)
            {
                ViewBag.Error = "ชื่อผู้ใช้งาน อีเมล หรือรหัสผ่านไม่ถูกต้อง";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = RememberMe };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                new ClaimsPrincipal(claimsIdentity), 
                authProperties);

            if (user.Role == "Manager")
            {
                return RedirectToAction("Dashboard", "Manager");
            }

            if (user.Role == "Staff")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 3. หน้าโปรไฟล์ (Profile)
        // ==========================================
        [HttpGet]
        public IActionResult Profile()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var currentUser = _db.Users.FirstOrDefault(u => u.Username == User.Identity.Name);
            var myOrders = new List<Order>();

            if (currentUser != null)
            {
                ViewBag.User = currentUser;
                ViewBag.Phone = currentUser.Phone;
                ViewBag.Addresses = _db.Useraddresses.Where(a => a.UserId == currentUser.UserId).ToList();
                
                // ⭐️ ดึงข้อมูลประวัติคำสั่งซื้อ พร้อม Include รายละเอียดสินค้าและชื่อสินค้า ⭐️
                myOrders = _db.Orders
                    .Include(o => o.Orderdetails)
                        .ThenInclude(od => od.Product)
                    .Where(o => o.UserId == currentUser.UserId)
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();
                var eligibleOrders = myOrders
                    .Where(order => !IsCancelledOrder(order.Status))
                    .ToList();
                ViewBag.LoyaltyPoints = Math.Max(0,
                    (int)Math.Floor(eligibleOrders.Sum(order => order.TotalAmount) / 100m) -
                    eligibleOrders.Sum(order => order.LoyaltyPointsRedeemed));
            }

            return View(myOrders);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OpenNotification(int notificationId)
        {
            var userId = _db.Users
                .Where(user => user.Username == User.Identity!.Name)
                .Select(user => (int?)user.UserId)
                .FirstOrDefault();
            if (!userId.HasValue) return NotFound();

            var notification = _db.UserNotifications.FirstOrDefault(item =>
                item.UserNotificationId == notificationId && item.UserId == userId.Value);
            if (notification == null) return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                _db.SaveChanges();
            }

            return RedirectToAction(nameof(Profile));
        }

        private static bool IsCancelledOrder(string? status)
        {
            return status == "ยกเลิก" || status == "ยกเลิกแล้ว" ||
                   string.Equals(status, "Cancelled", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Canceled", System.StringComparison.OrdinalIgnoreCase);
        }

        // ==========================================
        // 4. แก้ไขข้อมูลโปรไฟล์ (EditProfile)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EditProfile(string username, string phone, List<string> Addresses, int DefaultAddressIndex = 0)
        {
            var currentUser = _db.Users.FirstOrDefault(u => u.Username == User.Identity.Name);
            
            if (currentUser != null)
            {
                currentUser.Username = username;
                currentUser.Phone = phone;

                // ลบที่อยู่เก่าทิ้งให้หมดก่อน
                var oldAddresses = _db.Useraddresses.Where(a => a.UserId == currentUser.UserId).ToList();
                if (oldAddresses.Any())
                {
                    _db.Useraddresses.RemoveRange(oldAddresses);
                }

                // บันทึกที่อยู่ใหม่ที่ส่งมาจากหน้าเว็บ
                if (Addresses != null && Addresses.Count > 0)
                {
                    for (int i = 0; i < Addresses.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(Addresses[i]))
                        {
                            _db.Useraddresses.Add(new Useraddress
                            {
                                UserId = currentUser.UserId,
                                AddressDetail = Addresses[i],
                                IsDefault = (i == DefaultAddressIndex)
                            });
                        }
                    }
                }

                _db.SaveChanges();
            }

            // อัปเดตคุกกี้ล็อกอินใหม่
            var currentEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? "";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username ?? "Customer"),
                new Claim(ClaimTypes.Email, currentEmail),
                new Claim(ClaimTypes.Role, "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Profile");
        }

        // ==========================================
        // 5. ออกจากระบบ (Logout)
        // ==========================================
        [HttpGet, HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 6. ลืมรหัสผ่าน (ForgotPassword)
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string Username, string Email, string NewPassword)
        {
            if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 6)
            {
                ViewBag.Error = "รหัสผ่านใหม่ต้องมีอย่างน้อย 6 ตัวอักษร";
                return View();
            }

            var user = _db.Users.FirstOrDefault(u => u.Username == Username && u.Email == Email);
            
            if (user == null)
            {
                ViewBag.Error = "ข้อมูลไม่ถูกต้อง! ไม่พบชื่อผู้ใช้งานหรืออีเมลนี้ในระบบ";
                return View();
            }

            user.Password = NewPassword;
            _db.SaveChanges();

            TempData["Success"] = "เปลี่ยนรหัสผ่านสำเร็จ! กรุณาเข้าสู่ระบบด้วยรหัสผ่านใหม่";
            return RedirectToAction("Login");
        }
    }
}