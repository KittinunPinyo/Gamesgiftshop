using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Gamesgiftshop.Models;

namespace Gamesgiftshop.Controllers
{
    public class ProductController : Controller
    {
        private readonly Csi402dbContext _db;

        public ProductController(Csi402dbContext db)
        {
            _db = db;
        }

        public IActionResult Index(string search, string category, string sort)
        {
            // ดึงสินค้าทั้งหมดมารอกรอง
            var products = _db.Products.AsQueryable();

            // 1. กรองตามคำค้นหา (Search)
            if (!string.IsNullOrEmpty(search))
            {
                products = products.Where(p => p.Name.Contains(search));
            }

            // 2. กรองตามหมวดหมู่ (Category)
            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category == category);
            }

            // 3. เรียงลำดับ (Sort)
            switch (sort)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price);
                    break;
                case "newest":
                default:
                    products = products.OrderByDescending(p => p.ProductId);
                    break;
            }

            // 4. ดึงชื่อหมวดหมู่ทั้งหมดที่มีในฐานข้อมูลแบบไม่ซ้ำกัน ส่งไปให้ Dropdown
            ViewBag.Categories = _db.Products
                                    .Select(p => p.Category)
                                    .Where(c => !string.IsNullOrEmpty(c))
                                    .Distinct()
                                    .ToList();

            // ส่งข้อมูลสินค้าที่กรองเสร็จแล้วไปแสดงที่หน้าเว็บ
            return View(products.ToList());
        }

        // ==========================================
        // หน้าดูรายละเอียดสินค้า
        // ==========================================
        public IActionResult Details(int id)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }
            ViewBag.RequiresClothingSize = string.Equals(product.Category, "เสื้อผ้า", StringComparison.OrdinalIgnoreCase);
            return View(product);
        }
    }
}