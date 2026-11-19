using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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
            ViewBag.FavoriteProductIds = GetFavoriteProductIds();

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
            var requiresClothingSize = string.Equals(product.Category, "เสื้อผ้า", StringComparison.OrdinalIgnoreCase);
            ViewBag.RequiresClothingSize = requiresClothingSize;
            if (requiresClothingSize)
            {
                var sizeStocks = ProductSizeStock.AvailableSizes.ToDictionary(size => size, _ => 0);
                var savedSizeStocks = _db.ProductSizeStocks
                    .AsNoTracking()
                    .Where(stock => stock.ProductId == product.ProductId)
                    .ToList();
                foreach (var stock in savedSizeStocks)
                {
                    if (sizeStocks.ContainsKey(stock.Size))
                        sizeStocks[stock.Size] = stock.StockQuantity;
                }

                if (savedSizeStocks.Count == 0 && (product.StockQuantity ?? 0) > 0)
                    sizeStocks["L"] = product.StockQuantity ?? 0;

                ViewBag.SizeStocks = sizeStocks;
            }
            var userId = GetCurrentUserId();
            ViewBag.IsFavorite = userId.HasValue && _db.UserFavorites
                .AsNoTracking()
                .Any(favorite => favorite.UserId == userId.Value && favorite.ProductId == product.ProductId);

            var reviews = (from review in _db.ProductReviews.AsNoTracking()
                           join user in _db.Users.AsNoTracking() on review.UserId equals user.UserId
                           where review.ProductId == product.ProductId
                           orderby review.CreatedAt descending, review.ReviewId descending
                           select new ProductReviewDisplayViewModel
                           {
                               Username = user.Username,
                               Rating = review.Rating,
                               Comment = review.Comment,
                               CreatedAt = review.CreatedAt
                           })
                .ToList();
            var legacyReviewCount = Math.Max(0, product.ReviewCount ?? 0);
            var totalReviewCount = legacyReviewCount + reviews.Count;
            var totalRating = (product.Rating ?? 0m) * legacyReviewCount +
                reviews.Sum(review => (decimal)review.Rating);
            ViewBag.ProductReviews = reviews;
            ViewBag.DisplayReviewCount = totalReviewCount;
            ViewBag.DisplayRating = totalReviewCount == 0 ? 0m : totalRating / totalReviewCount;

            var hasReviewed = userId.HasValue && _db.ProductReviews
                .AsNoTracking()
                .Any(review => review.ProductId == product.ProductId && review.UserId == userId.Value);
            var hasPurchased = userId.HasValue && _db.Orders
                .AsNoTracking()
                .Any(order => order.UserId == userId.Value &&
                    (order.Status == "ชำระเงินสำเร็จแล้ว" ||
                     order.Status == "เตรียมจัดส่ง" ||
                     order.Status == "จัดส่งแล้ว" ||
                     order.Status == "สำเร็จ") &&
                    order.Orderdetails.Any(detail => detail.ProductId == product.ProductId));
            ViewBag.HasReviewed = hasReviewed;
            ViewBag.HasPurchased = hasPurchased;

            return View(product);
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Review(int productId, int rating, string? comment)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Forbid();

            var product = _db.Products.FirstOrDefault(item => item.ProductId == productId);
            if (product == null) return NotFound();

            var normalizedComment = comment?.Trim();
            if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(normalizedComment) || normalizedComment.Length > 1000)
            {
                TempData["ReviewError"] = "กรุณาให้คะแนน 1–5 ดาวและเขียนรีวิวไม่เกิน 1,000 ตัวอักษร";
                return RedirectToReviewSection(productId);
            }

            var hasPaidOrder = _db.Orders
                .AsNoTracking()
                .Any(order => order.UserId == userId.Value &&
                    (order.Status == "ชำระเงินสำเร็จแล้ว" ||
                     order.Status == "เตรียมจัดส่ง" ||
                     order.Status == "จัดส่งแล้ว" ||
                     order.Status == "สำเร็จ") &&
                    order.Orderdetails.Any(detail => detail.ProductId == productId));
            if (!hasPaidOrder)
            {
                TempData["ReviewError"] = "รีวิวได้เฉพาะสินค้าที่เคยสั่งซื้อและชำระเงินแล้ว";
                return RedirectToReviewSection(productId);
            }

            if (_db.ProductReviews.Any(review => review.ProductId == productId && review.UserId == userId.Value))
            {
                TempData["ReviewError"] = "คุณรีวิวสินค้านี้ไปแล้ว";
                return RedirectToReviewSection(productId);
            }

            _db.ProductReviews.Add(new ProductReview
            {
                ProductId = productId,
                UserId = userId.Value,
                Rating = rating,
                Comment = normalizedComment,
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();

            TempData["ReviewSuccess"] = "บันทึกรีวิวสินค้าเรียบร้อยแล้ว";
            return RedirectToReviewSection(productId);
        }

        private IActionResult RedirectToReviewSection(int productId)
        {
            var productUrl = Url.Action(nameof(Details), new { id = productId }) ?? $"/Product/Details/{productId}";
            return Redirect($"{productUrl}#reviews");
        }

        private int? GetCurrentUserId()
        {
            var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
            if (string.IsNullOrWhiteSpace(username)) return null;

            return _db.Users.AsNoTracking()
                .Where(user => user.Username == username)
                .Select(user => (int?)user.UserId)
                .FirstOrDefault();
        }

        private HashSet<int> GetFavoriteProductIds()
        {
            var userId = GetCurrentUserId();
            return userId.HasValue
                ? _db.UserFavorites.AsNoTracking()
                    .Where(favorite => favorite.UserId == userId.Value)
                    .Select(favorite => favorite.ProductId)
                    .ToHashSet()
                : new HashSet<int>();
        }
    }
}