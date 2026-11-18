using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Gamesgiftshop.Models;

namespace Gamesgiftshop.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerController : Controller
    {
        private readonly Csi402dbContext _db;
        private static readonly string[] PromotionTypes = { "Coupon", "Tier", "FlashSale", "BuyXGetY" };
        private static readonly string[] ProductBadgeTypes = { "", "BestSeller", "New", "Hot", "Discount" };
        private static readonly string[] ClothingSizes = ProductSizeStock.AvailableSizes;

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

        // ==========================================
        // 2. จัดการแคตตาล็อกสินค้า (Products Management)
        // ==========================================
        [HttpGet]
        public IActionResult Products()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");
            
            var products = _db.Products
                .Include(product => product.SizeStocks)
                .OrderByDescending(p => p.ProductId)
                .ToList();
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");
            
            // ดึงชื่อหมวดหมู่ทั้งหมดที่มีอยู่ในระบบ (แบบไม่ซ้ำกัน) ส่งไปให้หน้าเว็บแสดงผลใน Dropdown
            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            ViewBag.SizeStocks = new Dictionary<string, int>();
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product, List<string> ImageUrls, Dictionary<string, int>? sizeStocks)
        {
            Dictionary<string, int>? normalizedSizeStocks = null;
            if (IsClothing(product.Category))
            {
                if (!TryNormalizeSizeStocks(sizeStocks, out normalizedSizeStocks))
                    ModelState.AddModelError(nameof(sizeStocks), "กรุณาระบุสต็อกแต่ละไซซ์เป็นจำนวนเต็มตั้งแต่ 0 ขึ้นไป");
                else
                    product.StockQuantity = normalizedSizeStocks.Values.Sum();
            }

            if (!ProductBadgeTypes.Contains(product.BadgeType ?? ""))
                ModelState.AddModelError(nameof(product.BadgeType), "ป้ายสินค้าไม่ถูกต้อง");
            if (product.BadgeType == "Discount" &&
                (!product.OriginalPrice.HasValue || product.OriginalPrice <= product.Price))
                ModelState.AddModelError(nameof(product.BadgeType), "ป้ายลดราคาต้องกำหนดราคาเต็มสูงกว่าราคาขาย");

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
                if (normalizedSizeStocks != null)
                {
                    _db.ProductSizeStocks.AddRange(normalizedSizeStocks.Select(stock => new ProductSizeStock
                    {
                        Product = product,
                        Size = stock.Key,
                        StockQuantity = stock.Value
                    }));
                }
                AddUserBroadcastNotification(
                    "สินค้าใหม่มาแล้ว!",
                    $"สินค้าใหม่: {product.Name} ราคา ฿{product.Price:N0}");
                _db.SaveChanges();
                return RedirectToAction(nameof(Products)); // เสร็จแล้วกลับไปหน้า Products
            }
            
            // ถ้า Validate ไม่ผ่าน ให้ดึง Category กลับไปโชว์ใหม่ด้วยเพื่อป้องกัน Error หน้าเว็บ
            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            ViewBag.SizeStocks = sizeStocks ?? new Dictionary<string, int>();
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
            var product = _db.Products
                .Include(item => item.SizeStocks)
                .FirstOrDefault(p => p.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            // ดึงชื่อหมวดหมู่ทั้งหมดส่งไปให้ Dropdown
            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            var sizeStocks = ClothingSizes.ToDictionary(
                size => size,
                size => product.SizeStocks.FirstOrDefault(stock => stock.Size == size)?.StockQuantity ?? 0);
            if (product.SizeStocks.Count == 0 && IsClothing(product.Category))
                sizeStocks["L"] = Math.Max(0, product.StockQuantity ?? 0);
            ViewBag.SizeStocks = sizeStocks;
            
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Product data, List<string> ImageUrls, Dictionary<string, int>? sizeStocks)
        {
            if (id != data.ProductId)
            {
                return NotFound();
            }

            Dictionary<string, int>? normalizedSizeStocks = null;
            if (IsClothing(data.Category))
            {
                if (!TryNormalizeSizeStocks(sizeStocks, out normalizedSizeStocks))
                    ModelState.AddModelError(nameof(sizeStocks), "กรุณาระบุสต็อกแต่ละไซซ์เป็นจำนวนเต็มตั้งแต่ 0 ขึ้นไป");
                else
                    data.StockQuantity = normalizedSizeStocks.Values.Sum();
            }

            if (!ProductBadgeTypes.Contains(data.BadgeType ?? ""))
                ModelState.AddModelError(nameof(data.BadgeType), "ป้ายสินค้าไม่ถูกต้อง");
            if (data.BadgeType == "Discount" &&
                (!data.OriginalPrice.HasValue || data.OriginalPrice <= data.Price))
                ModelState.AddModelError(nameof(data.BadgeType), "ป้ายลดราคาต้องกำหนดราคาเต็มสูงกว่าราคาขาย");

            if (ModelState.IsValid)
            {
                // 1. ดึงข้อมูลสินค้าเดิมจากฐานข้อมูล
                var existingProduct = _db.Products
                    .Include(item => item.SizeStocks)
                    .FirstOrDefault(p => p.ProductId == id);
                
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
                existingProduct.BadgeType = data.BadgeType;

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

                if (normalizedSizeStocks != null)
                {
                    var removedSizes = existingProduct.SizeStocks
                        .Where(stock => !normalizedSizeStocks.ContainsKey(stock.Size))
                        .ToList();
                    _db.ProductSizeStocks.RemoveRange(removedSizes);

                    foreach (var stock in normalizedSizeStocks)
                    {
                        var existingSizeStock = existingProduct.SizeStocks
                            .FirstOrDefault(item => item.Size == stock.Key);
                        if (existingSizeStock == null)
                        {
                            _db.ProductSizeStocks.Add(new ProductSizeStock
                            {
                                ProductId = existingProduct.ProductId,
                                Size = stock.Key,
                                StockQuantity = stock.Value
                            });
                        }
                        else
                        {
                            existingSizeStock.StockQuantity = stock.Value;
                        }
                    }
                }
                else
                {
                    _db.ProductSizeStocks.RemoveRange(existingProduct.SizeStocks);
                }

                _db.SaveChanges();
                return RedirectToAction(nameof(Products));
            }

            ViewBag.Categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            ViewBag.SizeStocks = sizeStocks ?? new Dictionary<string, int>();
            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProductBadge(int id, string? badgeType)
        {
            badgeType = string.IsNullOrWhiteSpace(badgeType) ? null : badgeType.Trim();
            if (!ProductBadgeTypes.Contains(badgeType ?? ""))
                return BadRequest();

            var product = _db.Products.FirstOrDefault(item => item.ProductId == id);
            if (product == null)
                return NotFound();

            if (badgeType == "Discount" &&
                (!product.OriginalPrice.HasValue || product.OriginalPrice <= product.Price))
            {
                TempData["ProductBadgeError"] = $"สินค้า {product.Name} ต้องตั้งราคาเต็มสูงกว่าราคาขายก่อนเลือกป้ายลดราคา";
                return RedirectToAction(nameof(Products));
            }

            product.BadgeType = badgeType;
            _db.SaveChanges();
            TempData["ProductBadgeSuccess"] = $"บันทึกป้ายสินค้า {product.Name} แล้ว";
            return RedirectToAction(nameof(Products));
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
        [Authorize(Roles = "Manager,Admin")]
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
        [Authorize(Roles = "Manager,Admin")]
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
            var promotionMessage = $"มีโปรโมชั่นใหม่: {promotion.Name}";
            if (!string.IsNullOrWhiteSpace(promotion.Code))
                promotionMessage += $" | ใช้โค้ด {promotion.Code}";
            if (promotion.StartsAt.HasValue)
                promotionMessage += $" | เริ่ม {promotion.StartsAt.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
            AddUserBroadcastNotification("โปรโมชั่นใหม่มาแล้ว!", promotionMessage);
            _db.SaveChanges();
            TempData["PromotionSuccess"] = "สร้างโปรโมชั่นเรียบร้อยแล้ว";
            return RedirectToAction(nameof(Promotions));
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Admin")]
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
        [Authorize(Roles = "Manager,Admin")]
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
        [Authorize(Roles = "Manager,Admin")]
        public IActionResult Reports(string? period, string? reportDate, string? startDate, string? endDate)
        {
            var today = DateTime.Today;
            var selectedPeriod = string.IsNullOrWhiteSpace(period) ? "custom" : period.Trim().ToLowerInvariant();
            if (selectedPeriod != "daily" && selectedPeriod != "weekly" &&
                selectedPeriod != "monthly" && selectedPeriod != "custom")
                return BadRequest("ประเภทรายงานไม่ถูกต้อง");

            var anchorDate = today;
            if (selectedPeriod != "custom" && !string.IsNullOrWhiteSpace(reportDate) &&
                !DateTime.TryParseExact(reportDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out anchorDate))
            {
                ViewBag.Error = "รูปแบบวันที่ไม่ถูกต้อง";
                return View(new SalesReportViewModel
                {
                    Period = selectedPeriod,
                    ReportDate = today,
                    StartDate = today,
                    EndDate = today
                });
            }

            anchorDate = anchorDate.Date;
            if ((selectedPeriod == "weekly" && anchorDate > DateTime.MaxValue.Date.AddDays(-6)) ||
                (selectedPeriod == "monthly" && anchorDate.Year == 9999 && anchorDate.Month == 12))
                return BadRequest("วันที่รายงานอยู่นอกช่วงที่รองรับ");

            var start = selectedPeriod switch
            {
                "weekly" => anchorDate.AddDays(-(((int)anchorDate.DayOfWeek + 6) % 7)),
                "monthly" => new DateTime(anchorDate.Year, anchorDate.Month, 1),
                "daily" => anchorDate,
                _ => today.AddDays(-29)
            };
            var end = selectedPeriod switch
            {
                "weekly" => start.AddDays(6),
                "monthly" => start.AddMonths(1).AddDays(-1),
                "daily" => start,
                _ => today
            };

            if (selectedPeriod == "custom")
            {
                if (!string.IsNullOrWhiteSpace(startDate))
                {
                    if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
                    {
                        ViewBag.Error = "รูปแบบวันที่เริ่มต้นไม่ถูกต้อง";
                        return View(new SalesReportViewModel
                        {
                            Period = selectedPeriod,
                            ReportDate = today,
                            StartDate = today.AddDays(-29),
                            EndDate = today
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(endDate))
                {
                    if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
                    {
                        ViewBag.Error = "รูปแบบวันที่สิ้นสุดไม่ถูกต้อง";
                        return View(new SalesReportViewModel
                        {
                            Period = selectedPeriod,
                            ReportDate = today,
                            StartDate = start.Date,
                            EndDate = today
                        });
                    }
                }
            }

            start = start.Date;
            end = end.Date;
            var report = new SalesReportViewModel
            {
                Period = selectedPeriod,
                ReportDate = anchorDate,
                PeriodLabel = selectedPeriod switch
                {
                    "daily" => $"รายวัน · {start:dd/MM/yyyy}",
                    "weekly" => $"รายสัปดาห์ · {start:dd/MM/yyyy} - {end:dd/MM/yyyy}",
                    "monthly" => $"รายเดือน · {start:MM/yyyy}",
                    _ => $"กำหนดช่วงเอง · {start:dd/MM/yyyy} - {end:dd/MM/yyyy}"
                },
                StartDate = start,
                EndDate = end
            };

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
        [Authorize(Roles = "Manager,Admin")]
        public IActionResult Users(string? search, string? role)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Users", "Admin");

            var canBootstrapAdmin = !_db.Users.AsNoTracking().Any(user => user.Role == "Admin");
            var usersQuery = _db.Users.AsNoTracking();
            var trimmedSearch = search?.Trim();
            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                usersQuery = usersQuery.Where(user =>
                    user.Username.Contains(trimmedSearch) || user.Email.Contains(trimmedSearch));
            }

            if (role == "User" || role == "Manager" || role == "Staff" || role == "Accountant" ||
                (role == "Admin" && canBootstrapAdmin))
                usersQuery = usersQuery.Where(user => user.Role == role);

            var users = usersQuery.OrderBy(user => user.Username).ToList();
            var orderCounts = _db.Orders.AsNoTracking()
                .GroupBy(order => order.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionary(group => group.UserId, group => group.Count);

            return View(new ManagerUsersViewModel
            {
                Search = trimmedSearch,
                RoleFilter = role == "Admin" && !canBootstrapAdmin ? null : role,
                CurrentUsername = User.Identity?.Name,
                CanBootstrapAdmin = canBootstrapAdmin,
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

        [HttpGet]
        [Authorize(Roles = "Manager,Admin")]
        public IActionResult CreateUser(string? search, string? roleFilter)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("CreateUser", "Admin", new { search, roleFilter });

            var canBootstrapAdmin = !_db.Users.AsNoTracking().Any(user => user.Role == "Admin");
            var roles = new List<string> { "User", "Staff", "Accountant", "Manager" };
            if (canBootstrapAdmin)
                roles.Add("Admin");

            return View("~/Views/Shared/UserManagementForm.cshtml", new UserManagementFormViewModel
            {
                Role = "User",
                AvailableRoles = roles,
                Search = search,
                RoleFilter = roleFilter
            });
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateUser(UserManagementFormViewModel model)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("CreateUser", "Admin", new { search = model.Search, roleFilter = model.RoleFilter });

            var canBootstrapAdmin = !_db.Users.AsNoTracking().Any(user => user.Role == "Admin");
            model.AvailableRoles = new List<string> { "User", "Staff", "Accountant", "Manager" };
            if (canBootstrapAdmin)
                model.AvailableRoles.Add("Admin");
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
                Username = model.Username.Trim(),
                Email = model.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                Password = model.Password!,
                Role = model.Role
            });
            _db.SaveChanges();

            TempData["UserManagementSuccess"] = "เพิ่มบัญชีผู้ใช้เรียบร้อยแล้ว";
            return RedirectToAction(nameof(Users), new { search = model.Search, role = model.RoleFilter });
        }

        [HttpGet]
        [Authorize(Roles = "Manager,Admin")]
        public IActionResult EditUser(int id, string? search, string? roleFilter)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("EditUser", "Admin", new { id, search, roleFilter });

            var user = _db.Users.AsNoTracking().FirstOrDefault(item => item.UserId == id);
            if (user == null)
                return NotFound();
            if (user.Role == "Admin" || user.Username == User.Identity?.Name)
            {
                TempData["UserManagementError"] = "ไม่สามารถแก้ไขบัญชี Admin หรือบัญชีที่กำลังใช้งานอยู่ได้";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            var roles = new List<string> { "User", "Staff", "Accountant", "Manager" };
            if (!_db.Users.AsNoTracking().Any(item => item.Role == "Admin"))
                roles.Add("Admin");

            return View("~/Views/Shared/UserManagementForm.cshtml", new UserManagementFormViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                IsEdit = true,
                AvailableRoles = roles,
                Search = search,
                RoleFilter = roleFilter
            });
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(UserManagementFormViewModel model)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("EditUser", "Admin", new { id = model.UserId, search = model.Search, roleFilter = model.RoleFilter });

            var user = _db.Users.FirstOrDefault(item => item.UserId == model.UserId);
            if (user == null)
                return NotFound();
            if (user.Role == "Admin" || user.Username == User.Identity?.Name)
            {
                TempData["UserManagementError"] = "ไม่สามารถแก้ไขบัญชี Admin หรือบัญชีที่กำลังใช้งานอยู่ได้";
                return RedirectToAction(nameof(Users), new { search = model.Search, role = model.RoleFilter });
            }

            model.IsEdit = true;
            model.AvailableRoles = new List<string> { "User", "Staff", "Accountant", "Manager" };
            if (!_db.Users.AsNoTracking().Any(item => item.Role == "Admin"))
                model.AvailableRoles.Add("Admin");
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
            if (user.Role == "Manager" && model.Role != "Manager" &&
                _db.Users.Count(item => item.Role == "Manager") <= 1 &&
                !_db.Users.Any(item => item.Role == "Admin") &&
                model.Role != "Admin")
                ModelState.AddModelError(nameof(model.Role), "ต้องมีผู้จัดการหรือ Admin อย่างน้อยหนึ่งบัญชี");

            if (!ModelState.IsValid)
                return View("~/Views/Shared/UserManagementForm.cshtml", model);

            var roleChanged = user.Role != model.Role;
            user.Username = model.Username.Trim();
            user.Email = model.Email.Trim();
            user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            user.Role = model.Role;
            if (!string.IsNullOrEmpty(model.Password))
                user.Password = model.Password;
            _db.SaveChanges();

            TempData["UserManagementSuccess"] = roleChanged
                ? "แก้ไขบัญชีเรียบร้อยแล้ว ผู้ใช้ต้องเข้าสู่ระบบใหม่เพื่อรับสิทธิ์ใหม่"
                : "แก้ไขบัญชีผู้ใช้เรียบร้อยแล้ว";
            return RedirectToAction(nameof(Users), new { search = model.Search, role = model.RoleFilter });
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteUser(int userId, string? search, string? roleFilter)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Users", "Admin", new { search, role = roleFilter });

            var user = _db.Users.FirstOrDefault(item => item.UserId == userId);
            if (user == null)
                return NotFound();
            if (user.Role == "Admin")
            {
                TempData["UserManagementError"] = "Manager ไม่สามารถลบบัญชี Admin ได้";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }
            if (user.Username == User.Identity?.Name)
            {
                TempData["UserManagementError"] = "ไม่สามารถลบบัญชีที่กำลังใช้งานอยู่ได้";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }
            if (user.Role == "Manager" &&
                _db.Users.Count(item => item.Role == "Manager") <= 1 &&
                !_db.Users.Any(item => item.Role == "Admin"))
            {
                TempData["UserManagementError"] = "ต้องมีผู้จัดการหรือ Admin อย่างน้อยหนึ่งบัญชี";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }
            if (_db.Orders.AsNoTracking().Any(order => order.UserId == user.UserId))
            {
                TempData["UserManagementError"] = "ลบบัญชีนี้ไม่ได้ เนื่องจากมีประวัติคำสั่งซื้อ ระบบจะเก็บบัญชีไว้เพื่อรักษาประวัติ";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            var addresses = _db.Useraddresses.Where(address => address.UserId == user.UserId).ToList();
            _db.Useraddresses.RemoveRange(addresses);
            _db.Users.Remove(user);
            _db.SaveChanges();

            TempData["UserManagementSuccess"] = "ลบบัญชีผู้ใช้เรียบร้อยแล้ว";
            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateUserRole(int userId, string newRole, string? search, string? roleFilter)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Users", "Admin", new { search, role = roleFilter });

            var canBootstrapAdmin = !_db.Users.AsNoTracking().Any(item => item.Role == "Admin");
            if (newRole != "User" && newRole != "Manager" && newRole != "Staff" && newRole != "Accountant" &&
                !(newRole == "Admin" && canBootstrapAdmin))
                return BadRequest();

            var user = _db.Users.FirstOrDefault(item => item.UserId == userId);
            if (user == null) return NotFound();

            if (user.Role == "Admin")
            {
                TempData["UserManagementError"] = "จัดการสิทธิ์ Admin ได้จากหน้า Admin เท่านั้น";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            var isSelfBootstrap = user.Username == User.Identity?.Name &&
                newRole == "Admin" &&
                canBootstrapAdmin;
            if (user.Username == User.Identity?.Name && newRole != "Manager" && !isSelfBootstrap)
            {
                TempData["UserManagementError"] = "ไม่สามารถลดสิทธิ์บัญชีที่กำลังใช้งานอยู่ได้";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            if (user.Role == "Manager" && newRole != "Manager" &&
                _db.Users.Count(item => item.Role == "Manager") <= 1 &&
                !_db.Users.Any(item => item.Role == "Admin") &&
                newRole != "Admin")
            {
                TempData["UserManagementError"] = "ต้องมีผู้จัดการหรือ Admin อย่างน้อยหนึ่งบัญชี";
                return RedirectToAction(nameof(Users), new { search, role = roleFilter });
            }

            user.Role = newRole;
            _db.SaveChanges();
            TempData["UserManagementSuccess"] = "อัปเดตสิทธิ์บัญชีเรียบร้อยแล้ว ผู้ใช้ต้องเข้าสู่ระบบใหม่เพื่อรับสิทธิ์ใหม่";
            if (isSelfBootstrap)
                return RedirectToAction("Logout", "Account");

            return RedirectToAction(nameof(Users), new { search, role = roleFilter });
        }

        private static bool IsClothing(string? category) =>
            string.Equals(category, "เสื้อผ้า", StringComparison.OrdinalIgnoreCase);

        private static bool TryNormalizeSizeStocks(
            Dictionary<string, int>? submitted,
            out Dictionary<string, int> normalized)
        {
            normalized = new Dictionary<string, int>(StringComparer.Ordinal);
            if (submitted == null)
                return false;

            foreach (var stock in submitted)
            {
                var size = stock.Key.Trim().ToUpperInvariant();
                if (!ClothingSizes.Contains(size) || stock.Value < 0 || normalized.ContainsKey(size))
                    return false;

                normalized.Add(size, stock.Value);
            }

            return ClothingSizes.All(normalized.ContainsKey) &&
                normalized.Values.Sum(value => (long)value) <= int.MaxValue;
        }

        private void AddUserBroadcastNotification(string title, string message)
        {
            var userIds = _db.Users
                .AsNoTracking()
                .Where(user => user.Role == "User")
                .Select(user => user.UserId)
                .ToList();
            var createdAt = DateTime.Now;

            _db.UserNotifications.AddRange(userIds.Select(userId => new UserNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = createdAt
            }));
        }
    }
}