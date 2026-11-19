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
using Gamesgiftshop.Services;

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
            if (string.IsNullOrWhiteSpace(data.Phone))
            {
                ViewBag.Error = "กรุณากรอกเบอร์โทรศัพท์";
                return View(data);
            }

            data.Phone = data.Phone.Trim();
            if (data.Phone.Length > 20)
            {
                ViewBag.Error = "เบอร์โทรศัพท์ต้องไม่เกิน 20 ตัวอักษร";
                return View(data);
            }

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

            if (user.Role == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            if (user.Role == "Manager")
            {
                return RedirectToAction("Dashboard", "Manager");
            }

            if (user.Role == "Staff")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            if (user.Role == "Accountant")
            {
                return RedirectToAction("Dashboard", "Accounting");
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
                ViewBag.FavoriteProducts = _db.UserFavorites
                    .AsNoTracking()
                    .Where(favorite => favorite.UserId == currentUser.UserId)
                    .Include(favorite => favorite.Product)
                    .OrderByDescending(favorite => favorite.CreatedAt)
                    .Select(favorite => favorite.Product)
                    .ToList();
                
                // ⭐️ ดึงข้อมูลประวัติคำสั่งซื้อ พร้อม Include รายละเอียดสินค้าและชื่อสินค้า ⭐️
                myOrders = _db.Orders
                    .Include(o => o.Orderdetails)
                        .ThenInclude(od => od.Product)
                    .Where(o => o.UserId == currentUser.UserId)
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();
                ViewBag.RefundRequests = _db.RefundRequests
                    .AsNoTracking()
                    .Where(request => request.UserId == currentUser.UserId)
                    .OrderByDescending(request => request.RequestedAt)
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

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> RequestRefund(int orderId, string? reason, IFormFile? evidence)
        {
            var user = _db.Users
                .AsNoTracking()
                .FirstOrDefault(item => item.Username == User.Identity!.Name);
            if (user == null)
                return RedirectToAction(nameof(Login));

            using var transaction = _db.Database.BeginTransaction();
            var order = _db.Orders
                .FromSqlInterpolated($"SELECT * FROM orders WHERE OrderId = {orderId} AND UserID = {user.UserId} FOR UPDATE")
                .AsEnumerable()
                .FirstOrDefault();
            if (order == null)
                return NotFound();

            reason = reason?.Trim() ?? "";
            if (reason.Length is < 5 or > 1000)
            {
                TempData["RefundRequestError"] = "กรุณาระบุเหตุผลการคืนเงินอย่างน้อย 5 ตัวอักษร";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }

            if (evidence == null || evidence.Length < 8 || evidence.Length > 5 * 1024 * 1024)
            {
                TempData["RefundRequestError"] = "กรุณาแนบรูปหลักฐาน JPEG หรือ PNG ขนาดไม่เกิน 5 MB";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }

            var refundableStatus = order.Status == "ชำระเงินสำเร็จแล้ว" ||
                order.Status == "เตรียมจัดส่ง" ||
                order.Status == "จัดส่งแล้ว" ||
                order.Status == "สำเร็จ";
            if (!refundableStatus || !order.OrderDate.HasValue ||
                order.OrderDate.Value < DateTime.Now.AddDays(-7) ||
                order.OrderDate.Value > DateTime.Now)
            {
                TempData["RefundRequestError"] = "คำสั่งซื้อนี้ไม่อยู่ในเงื่อนไขการขอคืนเงินภายใน 7 วัน";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }

            var hasActiveOrCompletedRequest = _db.RefundRequests.Any(request =>
                request.OrderId == orderId &&
                request.Status != RefundRequest.RejectedStatus);
            if (hasActiveOrCompletedRequest)
            {
                TempData["RefundRequestError"] = "คำสั่งซื้อนี้มีคำขอคืนเงินที่กำลังตรวจสอบหรือดำเนินการแล้ว";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }

            using var evidenceStream = new MemoryStream();
            await evidence.CopyToAsync(evidenceStream);
            var bytes = evidenceStream.ToArray();
            var isJpeg = bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
            var isPng = bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e &&
                bytes[3] == 0x47 && bytes[4] == 0x0d && bytes[5] == 0x0a && bytes[6] == 0x1a && bytes[7] == 0x0a;
            if (bytes.Length != evidence.Length || (!isJpeg && !isPng))
            {
                TempData["RefundRequestError"] = "รูปหลักฐานต้องเป็นไฟล์ JPEG หรือ PNG ที่ถูกต้อง";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }

            _db.RefundRequests.Add(new RefundRequest
            {
                OrderId = order.OrderId,
                UserId = user.UserId,
                Reason = reason,
                Evidence = new RefundEvidence { Data = bytes, ContentType = isJpeg ? "image/jpeg" : "image/png" },
                Status = RefundRequest.PendingStatus,
                RequestedAt = DateTime.Now
            });
            _db.SaveChanges();
            transaction.Commit();

            TempData["RefundRequestSuccess"] = $"ส่งคำขอคืนเงินของออเดอร์ #{orderId} แล้ว";
            return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitReturnTracking(int id, string? trackingNumber)
        {
            trackingNumber = trackingNumber?.Trim() ?? "";
            if (trackingNumber.Length is < 1 or > 100)
            {
                TempData["RefundRequestError"] = "กรุณาระบุเลขพัสดุไม่เกิน 100 ตัวอักษร";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }

            var username = User.Identity?.Name;
            var updated = _db.RefundRequests
                .Where(request => request.RefundRequestId == id &&
                    request.User.Username == username &&
                    request.Status == RefundRequest.AwaitingReturnStatus)
                .ExecuteUpdate(updates => updates
                    .SetProperty(request => request.ReturnTrackingNumber, trackingNumber)
                    .SetProperty(request => request.Status, RefundRequest.ReturnShippedStatus));
            if (updated == 0)
            {
                TempData["RefundRequestError"] = "ไม่พบคำขอที่รอส่งคืนสินค้าหรือสถานะมีการเปลี่ยนแปลงแล้ว";
                return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
            }
            TempData["RefundRequestSuccess"] = "บันทึกเลขพัสดุแล้ว รอฝ่ายบัญชีตรวจรับสินค้า";
            return RedirectToAction(nameof(Profile), new { panel = "orders-panel" });
        }

        [Authorize(Roles = "User")]
        [HttpGet]
        public IActionResult RefundReceipt(int id)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return Challenge();

            var request = _db.RefundRequests
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Order)
                    .ThenInclude(order => order.Orderdetails)
                        .ThenInclude(detail => detail.Product)
                .FirstOrDefault(item =>
                    item.RefundRequestId == id &&
                    item.User.Username == username &&
                    item.Status == RefundRequest.RefundedStatus);
            if (request == null)
                return NotFound();

            return File(
                AccountingReportExporter.CreateRefundReceipt(request),
                "application/pdf",
                $"refund-receipt-{request.RefundRequestId:D6}.pdf");
        }

        [Authorize(Roles = "Admin,Manager,Staff,Accountant")]
        [HttpGet]
        public IActionResult StaffProfile()
        {
            var profile = _db.Users
                .AsNoTracking()
                .Where(user => user.Username == User.Identity!.Name)
                .Select(user => new StaffProfileViewModel
                {
                    Username = user.Username,
                    Email = user.Email,
                    Phone = user.Phone,
                    RoleName = user.Role == "Admin" ? "ผู้ดูแลระบบ" :
                        user.Role == "Manager" ? "ฝ่ายจัดการ" :
                        user.Role == "Staff" ? "ฝ่ายปฏิบัติการ" :
                        "ฝ่ายบัญชีและการเงิน"
                })
                .FirstOrDefault();

            if (profile == null)
            {
                return NotFound();
            }

            return View(profile);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleFavorite(int productId, string? returnUrl)
        {
            var userId = _db.Users
                .AsNoTracking()
                .Where(user => user.Username == User.Identity!.Name)
                .Select(user => (int?)user.UserId)
                .FirstOrDefault();
            if (!userId.HasValue) return Forbid();
            if (!_db.Products.AsNoTracking().Any(product => product.ProductId == productId))
                return NotFound();

            var favorite = _db.UserFavorites.Find(userId.Value, productId);
            if (favorite == null)
            {
                _db.UserFavorites.Add(new UserFavorite
                {
                    UserId = userId.Value,
                    ProductId = productId,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                _db.UserFavorites.Remove(favorite);
            }

            _db.SaveChanges();
            if (Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl!);

            return RedirectToAction(nameof(Profile));
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
                   status == "ชำระเงินไม่ผ่าน" ||
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
            var currentRole = currentUser?.Role ??
                User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ??
                "User";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username ?? "Customer"),
                new Claim(ClaimTypes.Email, currentEmail),
                new Claim(ClaimTypes.Role, currentRole)
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