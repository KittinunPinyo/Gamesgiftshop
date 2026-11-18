using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            ViewBag.FavoriteProductIds = GetFavoriteProductIds();
            var now = DateTime.Now;
            ViewBag.ActivePromotions = _db.Promotions
                .AsNoTracking()
                .Where(promotion => promotion.IsActive &&
                    (!promotion.StartsAt.HasValue || promotion.StartsAt <= now) &&
                    (!promotion.EndsAt.HasValue || promotion.EndsAt >= now))
                .OrderByDescending(promotion => promotion.PromotionId)
                .ToList();

            return View(products);
        }

        private HashSet<int> GetFavoriteProductIds()
        {
            var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
            if (string.IsNullOrWhiteSpace(username)) return new HashSet<int>();

            var userId = _db.Users.AsNoTracking()
                .Where(user => user.Username == username)
                .Select(user => (int?)user.UserId)
                .FirstOrDefault();
            return userId.HasValue
                ? _db.UserFavorites.AsNoTracking()
                    .Where(favorite => favorite.UserId == userId.Value)
                    .Select(favorite => favorite.ProductId)
                    .ToHashSet()
                : new HashSet<int>();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}