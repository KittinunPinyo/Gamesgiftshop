using Gamesgiftshop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamesgiftshop.Controllers;

[Authorize(Roles = "Staff,Admin")]
public class StaffController : Controller
{
    private readonly Csi402dbContext _db;

    public StaffController(Csi402dbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        var orders = _db.Orders
            .AsNoTracking()
            .Include(order => order.Orderdetails)
                .ThenInclude(detail => detail.Product)
            .Where(order => order.Status == "ชำระเงินสำเร็จแล้ว" || order.Status == "เตรียมจัดส่ง")
            .OrderBy(order => order.OrderDate)
            .ToList();

        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StartPacking(int orderId)
    {
        using var transaction = _db.Database.BeginTransaction();

        var order = _db.Orders.AsNoTracking().FirstOrDefault(item => item.OrderId == orderId);
        if (order == null) return NotFound();
        if (order.Status != "ชำระเงินสำเร็จแล้ว")
        {
            TempData["StaffError"] = "ออเดอร์นี้ไม่ได้อยู่ในสถานะชำระเงินสำเร็จแล้ว";
            return RedirectToAction(nameof(Dashboard));
        }

        var requestedItems = _db.Orderdetails
            .AsNoTracking()
            .Where(detail => detail.OrderId == orderId)
            .GroupBy(detail => detail.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
            .ToList();
        if (requestedItems.Count == 0)
        {
            TempData["StaffError"] = "ออเดอร์นี้ไม่มีรายการสินค้า";
            return RedirectToAction(nameof(Dashboard));
        }
        if (requestedItems.Any(item => item.Quantity <= 0))
        {
            TempData["StaffError"] = "รายการสินค้าในออเดอร์มีจำนวนไม่ถูกต้อง";
            return RedirectToAction(nameof(Dashboard));
        }

        var claimed = _db.Orders
            .Where(item => item.OrderId == orderId && item.Status == "ชำระเงินสำเร็จแล้ว")
            .ExecuteUpdate(updates => updates.SetProperty(item => item.Status, "เตรียมจัดส่ง"));
        if (claimed == 0)
        {
            TempData["StaffError"] = "ออเดอร์นี้ถูกพนักงานคนอื่นรับไปดำเนินการแล้ว";
            return RedirectToAction(nameof(Dashboard));
        }

        foreach (var item in requestedItems)
        {
            var changed = _db.Products
                .Where(product => product.ProductId == item.ProductId && product.StockQuantity >= item.Quantity)
                .ExecuteUpdate(updates => updates.SetProperty(
                    product => product.StockQuantity,
                    product => (int?)((product.StockQuantity ?? 0) - item.Quantity)));

            if (changed == 0)
            {
                transaction.Rollback();
                TempData["StaffError"] = "สต็อกสินค้าไม่เพียงพอ ไม่ได้เริ่มจัดเตรียมออเดอร์";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        transaction.Commit();
        TempData["StaffSuccess"] = $"เริ่มจัดเตรียมออเดอร์ #{orderId} และตัดสต็อกแล้ว";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmShipment(int orderId, string trackingNumber)
    {
        var normalizedTrackingNumber = trackingNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTrackingNumber) || normalizedTrackingNumber.Length > 100)
        {
            TempData["StaffError"] = "กรุณากรอกเลขติดตามพัสดุ (ไม่เกิน 100 ตัวอักษร)";
            return RedirectToAction(nameof(Dashboard));
        }

        using var transaction = _db.Database.BeginTransaction();
        var userId = _db.Orders
            .AsNoTracking()
            .Where(order => order.OrderId == orderId && order.Status == "เตรียมจัดส่ง")
            .Select(order => (int?)order.UserId)
            .FirstOrDefault();
        if (!userId.HasValue)
        {
            TempData["StaffError"] = "ไม่พบออเดอร์ที่กำลังเตรียมจัดส่ง";
            return RedirectToAction(nameof(Dashboard));
        }

        var updated = _db.Orders
            .Where(order => order.OrderId == orderId && order.Status == "เตรียมจัดส่ง")
            .ExecuteUpdate(updates => updates
                .SetProperty(order => order.TrackingNumber, normalizedTrackingNumber)
                .SetProperty(order => order.Status, "จัดส่งแล้ว"));

        if (updated == 0)
        {
            TempData["StaffError"] = "ไม่พบออเดอร์ที่กำลังเตรียมจัดส่ง";
            return RedirectToAction(nameof(Dashboard));
        }

        _db.UserNotifications.Add(new UserNotification
        {
            UserId = userId.Value,
            OrderId = orderId,
            Title = $"จัดส่งออเดอร์ #{orderId} แล้ว",
            Message = $"เลขติดตามพัสดุ: {normalizedTrackingNumber}",
            IsRead = false,
            CreatedAt = DateTime.Now
        });
        _db.SaveChanges();
        transaction.Commit();

        TempData["StaffSuccess"] = $"ยืนยันจัดส่งออเดอร์ #{orderId} แล้ว";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public IActionResult Inventory(string? search)
    {
        var products = _db.Products.AsNoTracking();
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearch))
            products = products.Where(product => product.Name.Contains(normalizedSearch));

        ViewBag.Search = normalizedSearch;
        return View(products.OrderBy(product => product.Name).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddInventory(int productId, int quantity, string? search)
    {
        var product = _db.Products.FirstOrDefault(item => item.ProductId == productId);
        if (product == null) return NotFound();

        var currentStock = Math.Max(0, product.StockQuantity ?? 0);
        if (quantity <= 0 || quantity > int.MaxValue - currentStock)
        {
            TempData["InventoryError"] = "จำนวนสินค้าที่รับเข้าต้องมากกว่า 0 และไม่เกินขีดจำกัดสต็อก";
            return RedirectToAction(nameof(Inventory), new { search });
        }

        product.StockQuantity = currentStock + quantity;
        _db.SaveChanges();
        TempData["InventorySuccess"] = $"รับสินค้า {product.Name} เข้าคลังเพิ่ม {quantity} ชิ้นแล้ว";
        return RedirectToAction(nameof(Inventory), new { search });
    }
}
