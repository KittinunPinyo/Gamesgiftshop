using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Gamesgiftshop.Models;

namespace Gamesgiftshop.Controllers
{
    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
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
        public decimal Shipping { get; set; } = 50; // ตั้งค่าส่ง 50 บาท
        public decimal GrandTotal => (Subtotal - Discount) + Shipping;
    }

    public class CartController : Controller
    {
        private readonly Csi402dbContext _db;

        public CartController(Csi402dbContext db)
        {
            _db = db;
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

        // ==========================================
        // 1. หน้าตะกร้าสินค้า (แสดงข้อมูลจริงจาก Session)
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            var cartItems = GetCartItems();
            var cartVM = new CartViewModel { Items = cartItems };
            return View(cartVM);
        }

        // ==========================================
        // 2. เพิ่มสินค้าลงตะกร้า (จากหน้า Home หรือ Product)
        // ==========================================
        [HttpPost]
        public IActionResult Add(int productId, int quantity = 1)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null) return RedirectToAction("Index", "Product");

            var cart = GetCartItems();
            var existingItem = cart.FirstOrDefault(c => c.ProductId == productId);

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
        public IActionResult UpdateQty(int productId, int quantity)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(c => c.ProductId == productId);
            if (item != null)
            {
                item.Quantity = quantity;
                if (item.Quantity <= 0) cart.Remove(item); // ถ้าจำนวนเหลือ 0 ให้ลบทิ้ง
            }
            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        // ==========================================
        // 4. ลบสินค้าออกจากตะกร้า
        // ==========================================
        [HttpGet]
        public IActionResult Remove(int productId)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(c => c.ProductId == productId);
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

            var cartItems = GetCartItems();
            if (!cartItems.Any()) return RedirectToAction("Index");

            var cartVM = new CartViewModel { Items = cartItems };
            ViewBag.GrandTotal = cartVM.GrandTotal;
            return View();
        }

        // ==========================================
        // 6. ยืนยันการสั่งซื้อและรับไฟล์สลิป
        // ==========================================
        [HttpPost]
        public IActionResult ConfirmOrder(string address, IFormFile paymentSlip)
        {
            // เมื่อสั่งซื้อสำเร็จ ให้ล้างตะกร้าของ User คนนั้นออก
            var sessionKey = GetCartSessionKey();
            HttpContext.Session.Remove(sessionKey);
            
            return RedirectToAction("Profile", "Account");
        }
    }
}