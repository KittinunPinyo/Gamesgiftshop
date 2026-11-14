using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Collections.Generic;
using Gamesgiftshop.Models;

namespace Gamesgiftshop.Controllers
{
    public class ManagerController : Controller
    {
        private readonly Csi402dbContext _db;

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
    }
}