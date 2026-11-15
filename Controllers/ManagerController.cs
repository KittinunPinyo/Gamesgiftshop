using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Gamesgiftshop.Models;

namespace Gamesgiftshop.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly Csi402dbContext _db;
        private static readonly string[] PromotionTypes = { "Coupon", "Tier", "FlashSale", "BuyXGetY" };

        public ManagerController(Csi402dbContext db)
        {
            _db = db;
        }

        // ==========================================
        // 1. จัดการบิลคำสั่งซื้อ (Order Management)
        // ==========================================
        [HttpGet]
        public IActionResult Dashboard()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");
            
            var allOrders = _db.Orders.OrderByDescending(o => o.OrderDate).ToList();
            ViewBag.Users = _db.Users.ToList();
            return View(allOrders);
        }

        [HttpPost]
        public IActionResult UpdateOrderStatus(int orderId, string newStatus)
        {
            var order = _db.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order != null) 
            { 
                order.Status = newStatus; 
                _db.SaveChanges(); 
            }
            return RedirectToAction("Dashboard");
        }

        // ==========================================
        // 2. จัดการแคตตาล็อกสินค้า (Products Management)
        // ==========================================
        [HttpGet]
        public IActionResult Products()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");
            
            var products = _db.Products.OrderByDescending(p => p.ProductId).ToList();
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");
            
            // ดึงชื่อหมวดหมู่ทั้งหมดที่มีอยู่ในระบบ (แบบไม่ซ้ำกัน) ส่งไปให้หน้าเว็บแสดงผลใน Dropdown
            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product, List<string> ImageUrls)
        {
            if (ModelState.IsValid)
            {
                // ==========================================
                // จัดการรูปลิงก์หลายรูป (เชื่อมด้วยเครื่องหมาย ,)
                // ==========================================
                if (ImageUrls != null && ImageUrls.Any())
                {
                    // กรองเอาเฉพาะช่องที่ผู้ใช้กรอกข้อมูลมาจริงๆ (ไม่เอาช่องว่าง)
                    var validUrls = ImageUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
                    
                    if (validUrls.Any())
                    {
                        // นำลิงก์มาต่อกันด้วยเครื่องหมาย , แล้วเซฟลงคอลัมน์ ImageUrl
                        product.ImageUrl = string.Join(",", validUrls);
                    }
                }

                _db.Products.Add(product);
                _db.SaveChanges();
                return RedirectToAction(nameof(Products)); // เสร็จแล้วกลับไปหน้า Products
            }
            
            // ถ้า Validate ไม่ผ่าน ให้ดึง Category กลับไปโชว์ใหม่ด้วยเพื่อป้องกัน Error หน้าเว็บ
            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            return View(product);
        }
        
        // ==========================================
        // 3. แก้ไขสินค้า (Edit Product)
        // ==========================================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");
            
            // ดึงข้อมูลสินค้าที่ต้องการแก้ไขจาก Database
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            // ดึงชื่อหมวดหมู่ทั้งหมดส่งไปให้ Dropdown
            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Product data, List<string> ImageUrls)
        {
            if (id != data.ProductId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // 1. ดึงข้อมูลสินค้าเดิมจากฐานข้อมูล
                var existingProduct = _db.Products.FirstOrDefault(p => p.ProductId == id);
                
                if (existingProduct == null)
                {
                    return NotFound();
                }

                existingProduct.Name = data.Name;
                existingProduct.Category = data.Category;
                existingProduct.Price = data.Price;
                existingProduct.OriginalPrice = data.OriginalPrice;
                existingProduct.StockQuantity = data.StockQuantity;
                existingProduct.Description = data.Description;
                existingProduct.WarrantyInfo = data.WarrantyInfo;

                // 3. จัดการรูปลิงก์หลายรูป
                if (ImageUrls != null && ImageUrls.Any())
                {
                    var validUrls = ImageUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
                    if (validUrls.Any())
                    {
                        existingProduct.ImageUrl = string.Join(",", validUrls);
                    }
                    else
                    {
                        existingProduct.ImageUrl = null;
                    }
                }
                else 
                {
                    existingProduct.ImageUrl = null;
                }

                _db.SaveChanges();
                return RedirectToAction(nameof(Products));
            }

            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            return View(data);
        }
        
        [HttpPost]
        public IActionResult DeleteProduct(int id)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            if (product != null)
            {
                _db.Products.Remove(product);
                _db.SaveChanges();
            }
            return RedirectToAction("Products");
        }

        [HttpGet]
        [Authorize(Roles = "Manager")]
        public IActionResult Promotions()
        {
            ViewBag.Categories = _db.Products
                .Where(product => product.Category != null)
                .Select(product => product.Category!)
                .Distinct()
                .OrderBy(category => category)
                .ToList();
            return View(_db.Promotions.OrderByDescending(promotion => promotion.PromotionId).ToList());
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePromotion(Promotion promotion)
        {
            promotion.Name = promotion.Name?.Trim() ?? "";
            promotion.Code = string.IsNullOrWhiteSpace(promotion.Code)
                ? null
                : promotion.Code.Trim().ToUpperInvariant();
            promotion.Category = string.IsNullOrWhiteSpace(promotion.Category)
                ? null
                : promotion.Category.Trim();

            if (string.IsNullOrWhiteSpace(promotion.Name) || !PromotionTypes.Contains(promotion.Type))
            {
                ModelState.AddModelError("", "กรุณาระบุชื่อและประเภทโปรโมชั่นให้ถูกต้อง");
            }

            if (promotion.StartsAt.HasValue && promotion.EndsAt.HasValue &&
                promotion.StartsAt.Value >= promotion.EndsAt.Value)
            {
                ModelState.AddModelError(nameof(promotion.EndsAt), "เวลาสิ้นสุดต้องอยู่หลังเวลาเริ่มต้น");
            }

            switch (promotion.Type)
            {
                case "Coupon":
                    if (string.IsNullOrWhiteSpace(promotion.Code))
                        ModelState.AddModelError(nameof(promotion.Code), "กรุณากำหนดโค้ดส่วนลด");
                    if ((promotion.DiscountPercent ?? 0) <= 0 && (promotion.DiscountAmount ?? 0) <= 0)
                        ModelState.AddModelError("", "โค้ดส่วนลดต้องมีจำนวนเงินหรือเปอร์เซ็นต์ส่วนลด");
                    if ((promotion.DiscountPercent ?? 0) > 0 && (promotion.DiscountAmount ?? 0) > 0)
                        ModelState.AddModelError("", "เลือกส่วนลดเป็นเปอร์เซ็นต์หรือจำนวนเงินอย่างใดอย่างหนึ่ง");
                    if ((promotion.DiscountPercent ?? 0) > 100)
                        ModelState.AddModelError(nameof(promotion.DiscountPercent), "ส่วนลดต้องไม่เกิน 100%");
                    break;
                case "Tier":
                case "FlashSale":
                    if ((promotion.DiscountPercent ?? 0) <= 0 || promotion.DiscountPercent > 100)
                        ModelState.AddModelError(nameof(promotion.DiscountPercent), "กรุณากำหนดส่วนลดตั้งแต่ 0.01 ถึง 100%");
                    if (promotion.Type == "Tier" && (promotion.MinimumOrderAmount ?? 0) <= 0)
                        ModelState.AddModelError(nameof(promotion.MinimumOrderAmount), "กรุณากำหนดยอดซื้อขั้นต่ำ");
                    break;
                case "BuyXGetY":
                    if ((promotion.BuyQuantity ?? 0) < 1 || (promotion.FreeQuantity ?? 0) < 1)
                        ModelState.AddModelError("", "กรุณากำหนดจำนวนซื้อและจำนวนสินค้าแถมอย่างน้อย 1 ชิ้น");
                    if (string.IsNullOrWhiteSpace(promotion.Category))
                        ModelState.AddModelError(nameof(promotion.Category), "กรุณาเลือกหมวดหมู่สินค้า");
                    break;
            }

            if (promotion.Code != null && _db.Promotions.Any(existing => existing.Code == promotion.Code))
                ModelState.AddModelError(nameof(promotion.Code), "โค้ดนี้ถูกใช้งานแล้ว");

            if (!ModelState.IsValid)
            {
                ViewBag.Error = "ตรวจสอบข้อมูลโปรโมชั่นอีกครั้ง";
                ViewBag.Categories = _db.Products
                    .Where(product => product.Category != null)
                    .Select(product => product.Category!)
                    .Distinct()
                    .OrderBy(category => category)
                    .ToList();
                return View("Promotions", _db.Promotions.OrderByDescending(item => item.PromotionId).ToList());
            }

            promotion.IsActive = true;
            _db.Promotions.Add(promotion);
            _db.SaveChanges();
            TempData["PromotionSuccess"] = "สร้างโปรโมชั่นเรียบร้อยแล้ว";
            return RedirectToAction(nameof(Promotions));
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult TogglePromotion(int id)
        {
            var promotion = _db.Promotions.FirstOrDefault(item => item.PromotionId == id);
            if (promotion == null) return NotFound();

            promotion.IsActive = !promotion.IsActive;
            _db.SaveChanges();
            return RedirectToAction(nameof(Promotions));
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePromotion(int id)
        {
            var promotion = _db.Promotions.FirstOrDefault(item => item.PromotionId == id);
            if (promotion == null) return NotFound();

            _db.Promotions.Remove(promotion);
            _db.SaveChanges();
            return RedirectToAction(nameof(Promotions));
        }

        [HttpGet]
        [Authorize(Roles = "Manager")]
        public IActionResult Reports(string? startDate, string? endDate)
        {
            var today = DateTime.Today;
            var start = today.AddDays(-29);
            var end = today;

            if (!string.IsNullOrWhiteSpace(startDate))
            {
                if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
                {
                    ViewBag.Error = "รูปแบบวันที่ไม่ถูกต้อง";
                    return View(new SalesReportViewModel { StartDate = start, EndDate = end });
                }
                start = parsedStart;
            }

            if (!string.IsNullOrWhiteSpace(endDate))
            {
                if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
                {
                    ViewBag.Error = "รูปแบบวันที่ไม่ถูกต้อง";
                    return View(new SalesReportViewModel { StartDate = start, EndDate = end });
                }
                end = parsedEnd;
            }

            start = start.Date;
            end = end.Date;
            var report = new SalesReportViewModel { StartDate = start, EndDate = end };

            if (start > end || end == DateTime.MaxValue.Date)
            {
                ViewBag.Error = "ช่วงวันที่ไม่ถูกต้อง";
                return View(report);
            }

            var endExclusive = end.AddDays(1);
            var orders = _db.Orders
                .Where(order => order.OrderDate.HasValue &&
                    order.OrderDate.Value >= start &&
                    order.OrderDate.Value < endExclusive &&
                    (order.Status == "ชำระเงินสำเร็จแล้ว" ||
                     order.Status == "เตรียมจัดส่ง" ||
                     order.Status == "จัดส่งแล้ว" ||
                     order.Status == "สำเร็จ"))
                .ToList();

            report.PaidOrderCount = orders.Count;
            report.NetSales = orders.Sum(order => order.TotalAmount);
            report.AverageOrderAmount = orders.Count == 0 ? 0 : report.NetSales / orders.Count;
            report.DailySales = orders
                .GroupBy(order => order.OrderDate!.Value.Date)
                .Select(group => new DailySalesReportRow
                {
                    Date = group.Key,
                    OrderCount = group.Count(),
                    NetSales = group.Sum(order => order.TotalAmount)
                })
                .OrderByDescending(day => day.Date)
                .ToList();

            report.TopProducts = _db.Orderdetails
                .AsNoTracking()
                .Include(detail => detail.Product)
                .Where(detail => detail.Order.OrderDate.HasValue &&
                    detail.Order.OrderDate.Value >= start &&
                    detail.Order.OrderDate.Value < endExclusive &&
                    (detail.Order.Status == "ชำระเงินสำเร็จแล้ว" ||
                     detail.Order.Status == "เตรียมจัดส่ง" ||
                     detail.Order.Status == "จัดส่งแล้ว" ||
                     detail.Order.Status == "สำเร็จ"))
                .ToList()
                .GroupBy(detail => detail.Product?.Name ?? "สินค้าไม่พร้อมใช้งาน")
                .Select(group => new TopProductSalesRow
                {
                    ProductName = group.Key,
                    QuantitySold = group.Sum(detail => detail.Quantity),
                    GrossSales = group.Sum(detail => detail.Price * detail.Quantity)
                })
                .OrderByDescending(product => product.QuantitySold)
                .ThenByDescending(product => product.GrossSales)
                .Take(10)
                .ToList();

            return View(report);
        }

        [HttpGet]
        [Authorize(Roles = "Manager")]
        public IActionResult Users(string? search, string? role)
        {
            var usersQuery = _db.Users.AsNoTracking();
            var trimmedSearch = search?.Trim();
            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                usersQuery = usersQuery.Where(user =>
                    user.Username.Contains(trimmedSearch) || user.Email.Contains(trimmedSearch));
            }

            if (role == "User" || role == "Manager" || role == "Staff")
                usersQuery = usersQuery.Where(user => user.Role == role);

            var users = usersQuery.OrderBy(user => user.Username).ToList();
            var orderCounts = _db.Orders.AsNoTracking()
                .GroupBy(order => order.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionary(group => group.UserId, group => group.Count);

            return View(new ManagerUsersViewModel
            {
                Search = trimmedSearch,
                RoleFilter = role,
                CurrentUsername = User.Identity?.Name,
                Users = users.Select(user => new ManagerUserRow
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

        [HttpPost]
        [Authorize(Roles = "Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateUserRole(int userId, string newRole, string? search, string? roleFilter)
        {
            if (newRole != "User" && newRole != "Manager" && newRole != "Staff")
                return BadRequest();

            var user = _db.Users.FirstOrDefault(item => item.UserId == userId);
            if (user == null) return NotFound();

            if (user.Username == User.Identity?.Name && newRole != "Manager")
            {
                TempData["UserManagementError"] = "ไม่สามารถลดสิทธิ์บัญชีที่กำลังใช้งานอยู่ได้";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            if (user.Role == "Manager" && newRole == "User" &&
                _db.Users.Count(item => item.Role == "Manager") <= 1)
            {
                TempData["UserManagementError"] = "ต้องมีผู้จัดการอย่างน้อยหนึ่งบัญชี";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            user.Role = newRole;
            _db.SaveChanges();
            TempData["UserManagementSuccess"] = "อัปเดตสิทธิ์บัญชีเรียบร้อยแล้ว";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }
    }
}