using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Gamesgiftshop.Models;

namespace Gamesgiftshop.Controllers
{
    public class HomeController : Controller
    {
        private readonly Csi402dbContext _db;

        public HomeController(Csi402dbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            // ดึงสินค้าล่าสุด 8 รายการมาแสดงที่หน้าแรก
            var products = _db.Products.OrderByDescending(p => p.ProductId).Take(8).ToList();
            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}