using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Gamesgiftshop.Models;
using Gamesgiftshop.Services;
using Microsoft.Extensions.Configuration;

namespace Gamesgiftshop.Controllers
{
    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string? Size { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public decimal Total => Price * Quantity;
    }

    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();
        public decimal Subtotal => Items.Sum(i => i.Total);
        public decimal Discount { get; set; } = 0; // ตั้งค่าเริ่มต้นส่วนลดเป็น 0
        public decimal FlashSaleDiscount { get; set; }
        public decimal BuyXGetYDiscount { get; set; }
        public decimal TierDiscount { get; set; }
        public decimal CouponDiscount { get; set; }
        public decimal LoyaltyDiscount { get; set; }
        public int LoyaltyPointsAvailable { get; set; }
        public int LoyaltyPointsRedeemed { get; set; }
        public string? PromoCode { get; set; }
        public decimal Shipping { get; set; } = 50; // ตั้งค่าส่ง 50 บาท
        public decimal GrandTotal => Math.Max(0, Subtotal - Discount) + Shipping;
    }

    public class CartController : Controller
    {
        private readonly Csi402dbContext _db;
        private readonly IConfiguration _configuration;
        private const string PromoSessionSuffix = "_PromoCode";
        private const string LoyaltySessionSuffix = "_LoyaltyPoints";
        private const string PromoMessageSessionKey = "PromoMessage";
        private static readonly string[] ClothingSizes = ProductSizeStock.AvailableSizes;

        public CartController(Csi402dbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        // ==========================================
        // Helper: ดึงข้อมูลตะกร้า / บันทึกตะกร้า ลง Session
        // ==========================================

        // 1. สร้างชื่อคีย์ Session ให้ผูกกับชื่อ User
        private string GetCartSessionKey()
        {
            // ถ้าล็อกอินแล้วให้ใช้ชื่อ User ถ้ายังไม่ล็อกอินให้เป็น Guest
            var username = User.Identity != null && User.Identity.IsAuthenticated ? User.Identity.Name : "Guest";
            return $"CartSession_{username}";
        }

        // 2. ดึงข้อมูลตะกร้าของ User คนนั้น
        private List<CartItemViewModel> GetCartItems()
        {
            var sessionKey = GetCartSessionKey();
            var sessionCart = HttpContext.Session.GetString(sessionKey);
            
            if (string.IsNullOrEmpty(sessionCart)) return new List<CartItemViewModel>();
            
            return JsonSerializer.Deserialize<List<CartItemViewModel>>(sessionCart);
        }

        // 3. บันทึกตะกร้าของ User คนนั้น
        private void SaveCartItems(List<CartItemViewModel> cart)
        {
            var sessionKey = GetCartSessionKey();
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(cart));
        }

        private CartViewModel BuildCartViewModel()
        {
            var cart = new CartViewModel { Items = GetCartItems() };
            if (!cart.Items.Any()) return cart;

            var now = DateTime.Now;
            var activePromotions = _db.Promotions.Where(promotion => promotion.IsActive).ToList()
                .Where(promotion => (!promotion.StartsAt.HasValue || promotion.StartsAt <= now) &&
                                    (!promotion.EndsAt.HasValue || promotion.EndsAt >= now))
                .ToList();
            var baseSubtotal = cart.Subtotal;
            var itemPrices = cart.Items.ToDictionary(item => (item.ProductId, item.Size), item => item.Price);

            foreach (var item in cart.Items)
            {
                var matchingFlash = activePromotions
                    .Where(promotion => promotion.Type == "FlashSale" &&
                        (string.IsNullOrEmpty(promotion.Category) ||
                         string.Equals(promotion.Category, item.Category, StringComparison.OrdinalIgnoreCase)))
                    .Select(promotion => promotion.DiscountPercent ?? 0)
                    .DefaultIfEmpty(0)
                    .Max();
                if (matchingFlash > 0)
                {
                    var salePrice = item.Price * (1 - matchingFlash / 100m);
                    cart.FlashSaleDiscount += (item.Price - salePrice) * item.Quantity;
                    itemPrices[(item.ProductId, item.Size)] = salePrice;
                }
            }

            var bestBuyXGetYDiscount = 0m;
            foreach (var promotion in activePromotions.Where(promotion => promotion.Type == "BuyXGetY" &&
                         (promotion.BuyQuantity ?? 0) > 0 && (promotion.FreeQuantity ?? 0) > 0))
            {
                var eligibleItems = cart.Items
                    .Where(item => string.Equals(item.Category, promotion.Category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => itemPrices[(item.ProductId, item.Size)])
                    .ToList();
                var totalQuantity = eligibleItems.Sum(item => item.Quantity);
                var freeQuantity = totalQuantity / ((promotion.BuyQuantity ?? 0) + (promotion.FreeQuantity ?? 0)) *
                                   (promotion.FreeQuantity ?? 0);
                var remainingFree = freeQuantity;
                var savings = 0m;
                foreach (var item in eligibleItems)
                {
                    var freeHere = Math.Min(remainingFree, item.Quantity);
                    savings += itemPrices[(item.ProductId, item.Size)] * freeHere;
                    remainingFree -= freeHere;
                    if (remainingFree == 0) break;
                }
                bestBuyXGetYDiscount = Math.Max(bestBuyXGetYDiscount, savings);
            }
            cart.BuyXGetYDiscount = bestBuyXGetYDiscount;

            var afterProductDeals = Math.Max(0, baseSubtotal - cart.FlashSaleDiscount - cart.BuyXGetYDiscount);
            var tierPercent = activePromotions
                .Where(promotion => promotion.Type == "Tier" &&
                    baseSubtotal >= (promotion.MinimumOrderAmount ?? decimal.MaxValue))
                .Select(promotion => promotion.DiscountPercent ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            cart.TierDiscount = Math.Min(afterProductDeals, afterProductDeals * tierPercent / 100m);
            var remainingTotal = afterProductDeals - cart.TierDiscount;

            var codeSessionKey = GetCartSessionKey() + PromoSessionSuffix;
            cart.PromoCode = HttpContext.Session.GetString(codeSessionKey);
            if (!string.IsNullOrEmpty(cart.PromoCode))
            {
                var coupon = activePromotions.FirstOrDefault(promotion => promotion.Type == "Coupon" &&
                    string.Equals(promotion.Code, cart.PromoCode, StringComparison.OrdinalIgnoreCase));
                var alreadyUsed = coupon?.Code != null && HasUsedPromoCode(coupon.Code);
                if (coupon != null && baseSubtotal >= (coupon.MinimumOrderAmount ?? 0) &&
                    (!coupon.FirstOrderOnly || !HasPreviousOrder()) && !alreadyUsed)
                {
                    cart.CouponDiscount = coupon.DiscountPercent.HasValue
                        ? remainingTotal * coupon.DiscountPercent.Value / 100m
                        : coupon.DiscountAmount ?? 0;
                    cart.CouponDiscount = Math.Min(remainingTotal, cart.CouponDiscount);
                    remainingTotal -= cart.CouponDiscount;
                }
                else
                {
                    HttpContext.Session.Remove(codeSessionKey);
                    if (alreadyUsed)
                        HttpContext.Session.SetString(PromoMessageSessionKey, "บัญชีนี้ใช้โค้ดส่วนลดนี้ไปแล้ว");
                    cart.PromoCode = null;
                }
            }

            cart.LoyaltyPointsAvailable = GetAvailableLoyaltyPoints();
            var selectedPoints = HttpContext.Session.GetInt32(GetCartSessionKey() + LoyaltySessionSuffix) ?? 0;
            cart.LoyaltyPointsRedeemed = Math.Min(selectedPoints, cart.LoyaltyPointsAvailable / 500 * 500);
            cart.LoyaltyDiscount = Math.Min(remainingTotal, remainingTotal * (cart.LoyaltyPointsRedeemed / 500 * 5) / 100m);

            cart.Discount = cart.FlashSaleDiscount + cart.BuyXGetYDiscount + cart.TierDiscount +
                            cart.CouponDiscount + cart.LoyaltyDiscount;
            return cart;
        }

        private bool HasPreviousOrder()
        {
            var user = GetCurrentUser();
            return user != null && _db.Orders
                .Where(order => order.UserId == user.UserId)
                .Select(order => order.Status)
                .AsEnumerable()
                .Any(status => !IsCancelledOrder(status));
        }

        private bool HasUsedPromoCode(string code)
        {
            var user = GetCurrentUser();
            return user != null && _db.Orders
                .Where(order => order.UserId == user.UserId && order.AppliedPromoCode == code)
                .Select(order => order.Status)
                .AsEnumerable()
                .Any(status => !IsCancelledOrder(status));
        }

        private static bool IsCancelledOrder(string? status)
        {
            return status == "ยกเลิก" || status == "ยกเลิกแล้ว" ||
                   status == "ชำระเงินไม่ผ่าน" ||
                   string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
        }

        private User? GetCurrentUser()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return null;
            return _db.Users.FirstOrDefault(user => user.Username == User.Identity.Name);
        }

        private int GetAvailableLoyaltyPoints()
        {
            var user = GetCurrentUser();
            if (user == null) return 0;

            var orders = _db.Orders.Where(order => order.UserId == user.UserId).ToList()
                .Where(order => !IsCancelledOrder(order.Status))
                .ToList();
            var earnedPoints = (int)Math.Floor(orders.Sum(order => order.TotalAmount) / 100m);
            var redeemedPoints = orders.Sum(order => order.LoyaltyPointsRedeemed);
            return Math.Max(0, earnedPoints - redeemedPoints);
        }

        // ==========================================
        // 1. หน้าตะกร้าสินค้า (แสดงข้อมูลจริงจาก Session)
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            var cartVM = BuildCartViewModel();
            ViewBag.PromoMessage = HttpContext.Session.GetString(PromoMessageSessionKey);
            HttpContext.Session.Remove(PromoMessageSessionKey);
            return View(cartVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApplyPromo(string promoCode)
        {
            var code = promoCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                HttpContext.Session.Remove(GetCartSessionKey() + PromoSessionSuffix);
                HttpContext.Session.SetString(PromoMessageSessionKey, "นำโค้ดส่วนลดออกแล้ว");
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var coupon = _db.Promotions.FirstOrDefault(promotion => promotion.Type == "Coupon" &&
                promotion.Code == code && promotion.IsActive &&
                (!promotion.StartsAt.HasValue || promotion.StartsAt <= now) &&
                (!promotion.EndsAt.HasValue || promotion.EndsAt >= now));
            if (coupon == null)
            {
                HttpContext.Session.Remove(GetCartSessionKey() + PromoSessionSuffix);
                HttpContext.Session.SetString(PromoMessageSessionKey, "ไม่พบโค้ดนี้ หรือโปรโมชั่นหมดอายุแล้ว");
                return RedirectToAction(nameof(Index));
            }

            if (HasUsedPromoCode(code))
            {
                HttpContext.Session.Remove(GetCartSessionKey() + PromoSessionSuffix);
                HttpContext.Session.SetString(PromoMessageSessionKey, "บัญชีนี้ใช้โค้ดส่วนลดนี้ไปแล้ว");
                return RedirectToAction(nameof(Index));
            }

            if (coupon.FirstOrderOnly && HasPreviousOrder())
            {
                HttpContext.Session.SetString(PromoMessageSessionKey, "โค้ดนี้ใช้ได้เฉพาะการสั่งซื้อครั้งแรก");
                return RedirectToAction(nameof(Index));
            }

            if (BuildCartViewModel().Subtotal < (coupon.MinimumOrderAmount ?? 0))
            {
                HttpContext.Session.SetString(PromoMessageSessionKey,
                    $"ยอดซื้อยังไม่ถึงขั้นต่ำ ฿{coupon.MinimumOrderAmount:N0}");
                return RedirectToAction(nameof(Index));
            }

            HttpContext.Session.SetString(GetCartSessionKey() + PromoSessionSuffix, code);
            HttpContext.Session.SetString(PromoMessageSessionKey, "ใช้โค้ดส่วนลดเรียบร้อยแล้ว");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApplyLoyalty(bool redeem)
        {
            var sessionKey = GetCartSessionKey() + LoyaltySessionSuffix;
            if (redeem)
            {
                var points = GetAvailableLoyaltyPoints() / 500 * 500;
                if (points > 0)
                    HttpContext.Session.SetInt32(sessionKey, points);
                else
                    HttpContext.Session.Remove(sessionKey);
            }
            else
            {
                HttpContext.Session.Remove(sessionKey);
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 2. เพิ่มสินค้าลงตะกร้า (จากหน้า Home หรือ Product)
        // ==========================================
        [HttpPost]
        public IActionResult Add(int productId, int quantity = 1, string? size = null)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null) return RedirectToAction("Index", "Product");

            var requiresSize = string.Equals(product.Category, "เสื้อผ้า", StringComparison.OrdinalIgnoreCase);
            if (requiresSize)
            {
                size = size?.Trim().ToUpperInvariant();
                if (!ClothingSizes.Contains(size))
                {
                    TempData["CartError"] = "กรุณาเลือกไซซ์เสื้อก่อนหยิบใส่ตะกร้า";
                    return RedirectToAction("Details", "Product", new { id = productId });
                }
            }
            else
            {
                size = null;
            }

            if (quantity < 1)
                return BadRequest();

            var cart = GetCartItems();
            var existingItem = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size);

            if (requiresSize)
            {
                var currentSizeQuantity = cart
                    .Where(item => item.ProductId == productId && item.Size == size)
                    .Sum(item => (long)item.Quantity);
                var availableSizeStock = GetClothingSizeStock(product, size!);
                if (currentSizeQuantity + quantity > availableSizeStock)
                {
                    TempData["CartError"] = $"ไซซ์ {size} มีสินค้าไม่เพียงพอ";
                    return RedirectToAction("Details", "Product", new { id = productId });
                }
            }
            else
            {
                var currentProductQuantity = cart
                    .Where(item => item.ProductId == productId)
                    .Sum(item => (long)item.Quantity);
                var availableStock = Math.Max(0, product.StockQuantity ?? 0);
                if (currentProductQuantity + quantity > availableStock)
                {
                    TempData["CartError"] = availableStock == 0
                        ? "สินค้าหมดชั่วคราว"
                        : $"สินค้า {product.Name} มีสินค้าไม่เพียงพอ";
                    return RedirectToAction("Details", "Product", new { id = productId });
                }
            }

            if (existingItem != null)
            {
                existingItem.Quantity += quantity; // ถ้ามีอยู่แล้วให้บวกจำนวนเพิ่ม
            }
            else
            {
                cart.Add(new CartItemViewModel
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Category = product.Category ?? "ทั่วไป",
                    Size = size,
                    Price = (decimal)product.Price,
                    Quantity = quantity,
                    ImageUrl = string.IsNullOrEmpty(product.ImageUrl) ? "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=400&q=80" : product.ImageUrl
                });
            }

            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        // ==========================================
        // 3. ปรับเพิ่ม/ลด จำนวนสินค้าในตะกร้า
        // ==========================================
        [HttpPost]
        public IActionResult UpdateQty(int productId, int quantity, string? size = null)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size);
            if (item != null)
            {
                var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
                if (quantity > item.Quantity)
                {
                    var isClothing = product != null &&
                        string.Equals(product.Category, "เสื้อผ้า", StringComparison.OrdinalIgnoreCase);
                    var matchingCartItems = cart.Where(cartItem =>
                        cartItem.ProductId == productId && (!isClothing || cartItem.Size == item.Size));
                    var otherQuantity = matchingCartItems.Sum(cartItem => (long)cartItem.Quantity) - item.Quantity;
                    var availableStock = product == null
                        ? 0
                        : isClothing
                            ? string.IsNullOrWhiteSpace(item.Size)
                                ? 0
                                : GetClothingSizeStock(product, item.Size)
                            : Math.Max(0, product.StockQuantity ?? 0);

                    if (otherQuantity + quantity > availableStock)
                    {
                        TempData["CartError"] = product == null
                            ? "ไม่พบสินค้า กรุณาตรวจสอบตะกร้าอีกครั้ง"
                            : isClothing
                                ? $"ไซซ์ {item.Size} มีสินค้าไม่เพียงพอ"
                                : $"สินค้า {product.Name} มีสินค้าไม่เพียงพอ";
                        return RedirectToAction(nameof(Index));
                    }
                }

                item.Quantity = quantity;
                if (item.Quantity <= 0) cart.Remove(item); // ถ้าจำนวนเหลือ 0 ให้ลบทิ้ง
            }
            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        private int GetClothingSizeStock(Product product, string size)
        {
            var sizeStock = _db.ProductSizeStocks
                .AsNoTracking()
                .Where(stock => stock.ProductId == product.ProductId && stock.Size == size)
                .Select(stock => (int?)stock.StockQuantity)
                .FirstOrDefault();
            if (sizeStock.HasValue)
                return Math.Max(0, sizeStock.Value);

            var hasSizeStocks = _db.ProductSizeStocks
                .AsNoTracking()
                .Any(stock => stock.ProductId == product.ProductId);
            return !hasSizeStocks && size == "L"
                ? Math.Max(0, product.StockQuantity ?? 0)
                : 0;
        }

        // ==========================================
        // 4. ลบสินค้าออกจากตะกร้า
        // ==========================================
        [HttpGet]
        public IActionResult Remove(int productId, string? size = null)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size);
            if (item != null) cart.Remove(item);
            
            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        // ==========================================
        // 5. หน้าชำระเงิน (Checkout)
        // ==========================================
        [HttpGet]
        public IActionResult Checkout()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            var cartVM = BuildCartViewModel();
            if (!cartVM.Items.Any()) return RedirectToAction("Index");
            ViewBag.GrandTotal = cartVM.GrandTotal;
            ViewBag.Discount = cartVM.Discount;
            var promptPayPayload = _configuration["PaymentSettings:PromptPayStaticPayload"]
                ?? throw new InvalidOperationException("The PromptPay payment payload is not configured.");
            ViewBag.PaymentQrCodeDataUrl = PromptPayQrCodeGenerator.GenerateDataUrl(promptPayPayload, cartVM.GrandTotal);
            return View();
        }

        // ==========================================
        // 6. ยืนยันการสั่งซื้อและรับไฟล์สลิป
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> ConfirmOrder(string address, IFormFile paymentSlip)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            if (paymentSlip == null || paymentSlip.Length == 0)
            {
                TempData["CheckoutError"] = "กรุณาแนบสลิปก่อนยืนยันคำสั่งซื้อ";
                return RedirectToAction(nameof(Checkout));
            }

            var cartVM = BuildCartViewModel();
            var cartItems = cartVM.Items;
            if (!cartItems.Any()) return RedirectToAction("Index");

            var requestedStock = cartItems
                .GroupBy(item => new { item.ProductId, item.Size })
                .Select(group => new
                {
                    group.Key.ProductId,
                    group.Key.Size,
                    Quantity = group.Sum(item => (long)item.Quantity)
                });
            foreach (var requested in requestedStock)
            {
                var product = _db.Products
                    .AsNoTracking()
                    .FirstOrDefault(item => item.ProductId == requested.ProductId);
                if (product == null || requested.Quantity <= 0)
                {
                    TempData["CheckoutError"] = "พบสินค้าในตะกร้าที่ไม่ถูกต้อง กรุณาตรวจสอบตะกร้าอีกครั้ง";
                    return RedirectToAction(nameof(Checkout));
                }

                var isClothing = string.Equals(product.Category, "เสื้อผ้า", StringComparison.OrdinalIgnoreCase);
                var availableStock = isClothing
                    ? requested.Size != null && ClothingSizes.Contains(requested.Size)
                        ? GetClothingSizeStock(product, requested.Size)
                        : 0
                    : Math.Max(0, product.StockQuantity ?? 0);
                if (requested.Quantity > availableStock)
                {
                    TempData["CheckoutError"] = isClothing
                        ? $"ไซซ์ {requested.Size ?? "ที่เลือก"} ของ {product.Name} มีสินค้าไม่เพียงพอ"
                        : $"สินค้า {product.Name} มีสินค้าไม่เพียงพอ";
                    return RedirectToAction(nameof(Checkout));
                }
            }

            // ดึงข้อมูล User ปัจจุบัน
            var currentUsername = User.Identity.Name;
            var currentUser = _db.Users.FirstOrDefault(u => u.Username == currentUsername);
            
            if (currentUser == null) return RedirectToAction("Login", "Account");

            // คำนวณยอดรวม (ต้องสร้าง CartViewModel มาคำนวณเหมือนหน้า Checkout)
            decimal totalAmount = cartVM.GrandTotal;

            string slipPath = "";

            // ⭐️ จัดการอัปโหลดรูปภาพสลิป ⭐️
            if (paymentSlip != null && paymentSlip.Length > 0)
            {
                // สร้างโฟลเดอร์ uploads/slips ถ้ายังไม่มี
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "slips");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                // สุ่มชื่อไฟล์ใหม่ป้องกันชื่อซ้ำ
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(paymentSlip.FileName);
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await paymentSlip.CopyToAsync(stream);
                }

                slipPath = "/uploads/slips/" + fileName; // Path ที่จะเก็บลง Database
            }

            // ⭐️ สร้างคำสั่งซื้อใหม่ (Order) ⭐️
            var newOrder = new Order
            {
                UserId = currentUser.UserId,
                OrderDate = DateTime.Now,
                TotalAmount = totalAmount,
                LoyaltyPointsRedeemed = cartVM.LoyaltyPointsRedeemed,
                AppliedPromoCode = cartVM.PromoCode,
                Status = "รอตรวจสอบสลิป",
                PaymentSlipUrl = slipPath,
                ShippingAddress = string.IsNullOrEmpty(address) ? "ไม่ระบุที่อยู่" : address
            };

            _db.Orders.Add(newOrder);
            _db.SaveChanges(); // บันทึก Order เพื่อให้ได้ OrderId มาใช้ต่อ

            foreach (var item in cartItems)
            {
                _db.Orderdetails.Add(new Orderdetail 
                { 
                    OrderId = newOrder.OrderId, 
                    ProductId = item.ProductId, 
                    Quantity = item.Quantity, 
                    Size = item.Size,
                    Price = item.Price 
                });
            }
            _db.SaveChanges();

            // เมื่อสั่งซื้อสำเร็จ ให้ล้างตะกร้าของ User คนนั้นออก
            var sessionKey = GetCartSessionKey();
            HttpContext.Session.Remove(sessionKey);
            HttpContext.Session.Remove(sessionKey + PromoSessionSuffix);
            HttpContext.Session.Remove(sessionKey + LoyaltySessionSuffix);
            
            // ส่งกลับไปหน้า Profile พร้อมข้อความสำเร็จ
            TempData["Success"] = "ยืนยันคำสั่งซื้อเรียบร้อยแล้ว!";
            return RedirectToAction("Profile", "Account");
        }
    }
}