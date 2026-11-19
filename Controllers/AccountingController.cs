using System.Globalization;
using Gamesgiftshop.Models;
using Gamesgiftshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamesgiftshop.Controllers;

[Authorize(Roles = "Accountant,Admin")]
public class AccountingController : Controller
{
    private readonly Csi402dbContext _db;

    public AccountingController(Csi402dbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Dashboard(string? period, string? date, string? month)
    {
        if (!TryResolvePeriod(period, date, month, out var selectedPeriod))
            return BadRequest("ช่วงเวลาหรือวันที่ไม่ถูกต้อง");

        SetLayoutCounts();
        return View(BuildDashboardViewModel(selectedPeriod));
    }

    [HttpGet]
    public IActionResult Payments()
    {
        var orders = _db.Orders
            .AsNoTracking()
            .Include(order => order.Orderdetails)
                .ThenInclude(detail => detail.Product)
            .Where(order => order.Status == "รอตรวจสอบสลิป" || order.Status == "รอดำเนินการ")
            .OrderBy(order => order.OrderDate)
            .ToList();
        ViewBag.PendingPaymentCount = orders.Count;
        ViewBag.LatestPendingPaymentOrderId = orders.Select(order => order.OrderId).DefaultIfEmpty().Max();
        ViewBag.PendingRefundRequestCount = GetPendingRefundRequestCount();
        var userIds = orders.Select(order => order.UserId).Distinct().ToList();
        var usernames = _db.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.UserId))
            .ToDictionary(user => user.UserId, user => user.Username);

        return View(new AccountingPaymentViewModel
        {
            Orders = orders.Select(order => new AccountingPaymentOrder
            {
                OrderId = order.OrderId,
                OrderDate = order.OrderDate,
                CustomerName = usernames.GetValueOrDefault(order.UserId, "ไม่ทราบ"),
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                PaymentSlipUrl = order.PaymentSlipUrl,
                ProductSummary = order.Orderdetails
                    .Select(detail => $"{detail.Product?.Name ?? $"สินค้า #{detail.ProductId}"}{(string.IsNullOrWhiteSpace(detail.Size) ? "" : $" (ไซซ์ {detail.Size})")} × {detail.Quantity}")
                    .ToList()
            }).ToList()
        });
    }

    [HttpGet]
    public IActionResult PaymentStatus()
    {
        var pendingOrders = _db.Orders
            .AsNoTracking()
            .Where(order => order.Status == "รอตรวจสอบสลิป" || order.Status == "รอดำเนินการ");

        return Json(new
        {
            pendingCount = pendingOrders.Count(),
            latestOrderId = pendingOrders.Max(order => (int?)order.OrderId) ?? 0
        });
    }

    [HttpGet]
    public IActionResult Refunds()
    {
        SetLayoutCounts();
        var requests = _db.RefundRequests
            .AsNoTracking()
            .Include(request => request.User)
            .Include(request => request.Order)
                .ThenInclude(order => order.Orderdetails)
                    .ThenInclude(detail => detail.Product)
            .OrderBy(request => request.Status == RefundRequest.PendingStatus ||
                request.Status == RefundRequest.ReturnShippedStatus ||
                request.Status == RefundRequest.ReturnReceivedStatus ? 0 : 1)
            .ThenByDescending(request => request.RequestedAt)
            .ToList();

        ViewBag.RefundEvidenceIds = _db.RefundEvidence.AsNoTracking()
            .Select(evidence => evidence.RefundRequestId).ToHashSet();
        return View(new AccountingRefundViewModel { Requests = requests });
    }

    [HttpGet]
    public IActionResult RefundEvidence(int id)
    {
        var evidence = _db.RefundEvidence.AsNoTracking()
            .Where(item => item.RefundRequestId == id)
            .FirstOrDefault();
        if (evidence == null)
            return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(evidence.Data, evidence.ContentType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RequireReturn(int id, string? returnAddress, string? feedback)
    {
        returnAddress = returnAddress?.Trim() ?? "";
        feedback = feedback?.Trim() ?? "";
        if (returnAddress.Length is < 5 or > 1000 || feedback.Length is < 5 or > 1000)
        {
            TempData["AccountingRefundError"] = "กรุณาระบุที่อยู่ส่งคืนและ Feedback อย่างละ 5–1000 ตัวอักษร";
            return RedirectToAction(nameof(Refunds));
        }
        var updated = _db.RefundRequests
            .Where(item => item.RefundRequestId == id && item.Status == RefundRequest.PendingStatus)
            .ExecuteUpdate(updates => updates
                .SetProperty(item => item.ReturnAddress, returnAddress)
                .SetProperty(item => item.Feedback, feedback)
                .SetProperty(item => item.Status, RefundRequest.AwaitingReturnStatus));
        TempData[updated == 0 ? "AccountingRefundError" : "AccountingRefundSuccess"] = updated == 0
            ? "ไม่พบคำขอที่รอตรวจสอบหรือสถานะมีการเปลี่ยนแปลงแล้ว"
            : "แจ้งลูกค้าให้ส่งคืนสินค้าแล้ว";
        return RedirectToAction(nameof(Refunds));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmReturnReceived(int id)
    {
        var updated = _db.RefundRequests
            .Where(item => item.RefundRequestId == id && item.Status == RefundRequest.ReturnShippedStatus)
            .ExecuteUpdate(updates => updates.SetProperty(item => item.Status, RefundRequest.ReturnReceivedStatus));
        TempData[updated == 0 ? "AccountingRefundError" : "AccountingRefundSuccess"] = updated == 0
            ? "ไม่พบคำขอที่รอตรวจรับสินค้าหรือสถานะมีการเปลี่ยนแปลงแล้ว"
            : "บันทึกการตรวจรับสินค้าแล้ว กรุณาดำเนินการคืนเงิน";
        return RedirectToAction(nameof(Refunds));
    }

    [HttpGet]
    public IActionResult RefundReceipt(int id)
    {
        var request = _db.RefundRequests
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Order)
                .ThenInclude(order => order.Orderdetails)
                    .ThenInclude(detail => detail.Product)
            .FirstOrDefault(item =>
                item.RefundRequestId == id &&
                item.Status == RefundRequest.RefundedStatus);
        if (request == null)
            return NotFound();

        return File(
            AccountingReportExporter.CreateRefundReceipt(request),
            "application/pdf",
            $"refund-receipt-{request.RefundRequestId:D6}.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewRefundRequest(int id, bool approve, string? feedback)
    {
        feedback = feedback?.Trim() ?? "";
        if (feedback.Length is < 5 or > 1000)
        {
            TempData["AccountingRefundError"] = "กรุณาระบุ Feedback อย่างน้อย 5 ตัวอักษร";
            return RedirectToAction(nameof(Refunds));
        }

        using var transaction = _db.Database.BeginTransaction();
        var request = _db.RefundRequests
            .AsNoTracking()
            .Include(item => item.Order)
            .FirstOrDefault(item => item.RefundRequestId == id);
        if (request == null)
            return NotFound();
        if (request.Status != RefundRequest.PendingStatus && request.Status != RefundRequest.ReturnReceivedStatus)
        {
            TempData["AccountingRefundError"] = "คำขอนี้ถูกตรวจสอบไปแล้ว";
            return RedirectToAction(nameof(Refunds));
        }

        var currentStatus = request.Status;
        var reviewedAt = DateTime.Now;
        var reviewedBy = User.Identity?.Name;
        var updated = _db.RefundRequests
            .Where(item => item.RefundRequestId == id && item.Status == currentStatus)
            .ExecuteUpdate(updates => updates
                .SetProperty(item => item.Status, approve ? RefundRequest.RefundedStatus : RefundRequest.RejectedStatus)
                .SetProperty(item => item.Feedback, feedback)
                .SetProperty(item => item.ReviewedAt, reviewedAt)
                .SetProperty(item => item.ReviewedBy, reviewedBy));
        if (updated == 0)
        {
            TempData["AccountingRefundError"] = "คำขอนี้ถูกตรวจสอบไปแล้ว";
            return RedirectToAction(nameof(Refunds));
        }
        if (approve)
            _db.Orders.Where(order => order.OrderId == request.OrderId)
                .ExecuteUpdate(updates => updates.SetProperty(order => order.Status, RefundRequest.RefundedStatus));

        transaction.Commit();
        TempData["AccountingRefundSuccess"] = approve
            ? $"บันทึกการคืนเงินของออเดอร์ #{request.OrderId} แล้ว"
            : $"ปฏิเสธคำขอคืนเงินของออเดอร์ #{request.OrderId} แล้ว";
        return RedirectToAction(nameof(Refunds));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewPayment(int orderId, bool approved)
    {
        var newStatus = approved ? "ชำระเงินสำเร็จแล้ว" : "ชำระเงินไม่ผ่าน";
        var updated = _db.Orders
            .Where(order => order.OrderId == orderId &&
                (order.Status == "รอตรวจสอบสลิป" || order.Status == "รอดำเนินการ") &&
                order.PaymentSlipUrl != null &&
                order.PaymentSlipUrl != "")
            .ExecuteUpdate(updates => updates.SetProperty(order => order.Status, newStatus));

        if (updated == 0)
        {
            TempData["PaymentReviewError"] = "ไม่พบออเดอร์ที่รอตรวจสอบพร้อมสลิป หรือออเดอร์นี้ถูกตรวจสอบไปแล้ว";
            return RedirectToAction(nameof(Payments));
        }

        TempData["PaymentReviewSuccess"] = approved
            ? $"ยืนยันการชำระเงินของออเดอร์ #{orderId} แล้ว"
            : $"ปฏิเสธสลิปของออเดอร์ #{orderId} แล้ว ลูกค้าจะเห็นสถานะและติดต่อร้านค้าได้";
        return RedirectToAction(nameof(Payments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddExpense(
        string description,
        string category,
        decimal amount,
        DateTime expenseDate,
        string? period,
        string? date,
        string? month)
    {
        description = description?.Trim() ?? "";
        category = category?.Trim() ?? "";
        if (description.Length is < 1 or > 200 ||
            category.Length is < 1 or > 80 ||
            amount <= 0 || amount > 99999999.99m ||
            expenseDate == default)
        {
            TempData["AccountingError"] = "กรุณากรอกรายละเอียด หมวดหมู่ จำนวนเงิน และวันที่ให้ถูกต้อง";
            return RedirectToAction(nameof(Dashboard), new { period, date, month });
        }

        _db.AccountingExpenses.Add(new AccountingExpense
        {
            Description = description,
            Category = category,
            Amount = amount,
            ExpenseDate = expenseDate.Date,
            CreatedBy = User.Identity?.Name,
            CreatedAt = DateTime.Now
        });
        _db.SaveChanges();

        TempData["AccountingSuccess"] = "บันทึกรายจ่ายเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Dashboard), new { period, date, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteExpense(int id, string? period, string? date, string? month)
    {
        var expense = _db.AccountingExpenses.FirstOrDefault(item => item.AccountingExpenseId == id);
        if (expense == null) return NotFound();

        _db.AccountingExpenses.Remove(expense);
        _db.SaveChanges();
        TempData["AccountingSuccess"] = "ลบรายการรายจ่ายเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Dashboard), new { period, date, month });
    }

    [HttpGet]
    public IActionResult ExportExcel(string? period, string? date, string? month)
    {
        if (!TryResolvePeriod(period, date, month, out var selectedPeriod))
            return BadRequest("ช่วงเวลาหรือวันที่ไม่ถูกต้อง");

        var report = BuildDashboardViewModel(selectedPeriod);
        var fileName = BuildExportFileName(selectedPeriod, "xlsx");
        return File(
            AccountingReportExporter.CreateExcel(report),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet]
    public IActionResult ExportPdf(string? period, string? date, string? month)
    {
        if (!TryResolvePeriod(period, date, month, out var selectedPeriod))
            return BadRequest("ช่วงเวลาหรือวันที่ไม่ถูกต้อง");

        var report = BuildDashboardViewModel(selectedPeriod);
        var fileName = BuildExportFileName(selectedPeriod, "pdf");
        return File(AccountingReportExporter.CreatePdf(report), "application/pdf", fileName);
    }

    private AccountingDashboardViewModel BuildDashboardViewModel(AccountingPeriod period)
    {
        var sales = _db.Orders
            .AsNoTracking()
            .Include(order => order.Orderdetails)
                .ThenInclude(detail => detail.Product)
            .Where(order => order.OrderDate.HasValue &&
                order.OrderDate.Value >= period.Start &&
                order.OrderDate.Value < period.EndExclusive &&
                (order.Status == "ชำระเงินสำเร็จแล้ว" ||
                 order.Status == "เตรียมจัดส่ง" ||
                 order.Status == "จัดส่งแล้ว" ||
                 order.Status == "สำเร็จ" ||
                 order.Status == RefundRequest.RefundedStatus))
            .OrderByDescending(order => order.OrderDate)
            .ToList();
        var expenses = _db.AccountingExpenses
            .AsNoTracking()
            .Where(expense => expense.ExpenseDate >= period.Start && expense.ExpenseDate < period.EndExclusive)
            .OrderByDescending(expense => expense.ExpenseDate)
            .ThenByDescending(expense => expense.AccountingExpenseId)
            .ToList();
        var refunds = _db.RefundRequests
            .AsNoTracking()
            .Include(request => request.Order)
            .Where(request => request.Status == RefundRequest.RefundedStatus &&
                request.ReviewedAt.HasValue &&
                request.ReviewedAt.Value >= period.Start &&
                request.ReviewedAt.Value < period.EndExclusive)
            .OrderByDescending(request => request.ReviewedAt)
            .ToList();

        var revenue = sales.Sum(order => order.TotalAmount);
        var expenseTotal = expenses.Sum(expense => expense.Amount);
        var refundTotal = refunds.Sum(request => request.Order.TotalAmount);
        var model = new AccountingDashboardViewModel
        {
            Period = period.Key,
            PeriodLabel = period.Label,
            DateInputValue = period.InputValue,
            SelectedDate = period.SelectedDate,
            PeriodStart = period.Start,
            PeriodEndExclusive = period.EndExclusive,
            PaidOrderCount = sales.Count(order => order.Status != RefundRequest.RefundedStatus),
            Revenue = revenue,
            ExpenseTotal = expenseTotal,
            RefundTotal = refundTotal,
            NetIncome = revenue - expenseTotal - refundTotal,
            SalesTransactions = sales,
            Expenses = expenses,
            RefundTransactions = refunds
        };

        model.LedgerEntries.AddRange(sales.Select(order => new AccountingLedgerEntry
        {
            Date = order.OrderDate!.Value,
            Type = "รายรับ",
            Reference = $"#{order.OrderId}",
            Description = string.Join(", ", order.Orderdetails.Select(detail =>
                $"{detail.Product?.Name ?? $"สินค้า #{detail.ProductId}"} × {detail.Quantity}")) +
                $" ({order.Status})",
            Amount = order.TotalAmount
        }));
        model.LedgerEntries.AddRange(expenses.Select(expense => new AccountingLedgerEntry
        {
            Date = expense.ExpenseDate,
            Type = "รายจ่าย",
            Reference = expense.Category,
            Description = expense.Description,
            Amount = -expense.Amount
        }));
        model.LedgerEntries.AddRange(refunds.Select(request => new AccountingLedgerEntry
        {
            Date = request.ReviewedAt!.Value,
            Type = "คืนเงิน",
            Reference = $"#{request.OrderId}",
            Description = request.Feedback ?? "ยืนยันคืนเงิน",
            Amount = -request.Order.TotalAmount
        }));

        return model;
    }

    private void SetLayoutCounts()
    {
        var pendingOrders = _db.Orders
            .AsNoTracking()
            .Where(order => order.Status == "รอตรวจสอบสลิป" || order.Status == "รอดำเนินการ");
        ViewBag.PendingPaymentCount = pendingOrders.Count();
        ViewBag.LatestPendingPaymentOrderId = pendingOrders.Max(order => (int?)order.OrderId) ?? 0;
        ViewBag.PendingRefundRequestCount = GetPendingRefundRequestCount();
    }

    private int GetPendingRefundRequestCount()
    {
        return _db.RefundRequests
            .AsNoTracking()
            .Count(request => request.Status == RefundRequest.PendingStatus ||
                request.Status == RefundRequest.ReturnShippedStatus ||
                request.Status == RefundRequest.ReturnReceivedStatus);
    }

    private static bool TryResolvePeriod(
        string? period,
        string? date,
        string? legacyMonth,
        out AccountingPeriod resolved)
    {
        var key = string.IsNullOrWhiteSpace(period) ? "month" : period.Trim().ToLowerInvariant();
        if (key is not ("day" or "week" or "month"))
        {
            resolved = default!;
            return false;
        }

        var inputValue = string.IsNullOrWhiteSpace(date)
            ? key == "month" ? legacyMonth : null
            : date;
        var dateFormat = key == "month" ? "yyyy-MM" : "yyyy-MM-dd";
        DateTime selectedDate;
        if (string.IsNullOrWhiteSpace(inputValue))
        {
            selectedDate = DateTime.Today;
        }
        else if (!DateTime.TryParseExact(
                     inputValue,
                     dateFormat,
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.None,
                     out selectedDate))
        {
            resolved = default!;
            return false;
        }

        DateTime start;
        DateTime endExclusive;
        string label;
        if (key == "day")
        {
            start = selectedDate.Date;
            if (start == DateTime.MaxValue.Date)
            {
                resolved = default!;
                return false;
            }
            endExclusive = start.AddDays(1);
            label = start.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("th-TH"));
        }
        else if (key == "week")
        {
            var daysFromMonday = ((int)selectedDate.DayOfWeek + 6) % 7;
            if (selectedDate.Date < DateTime.MinValue.Date.AddDays(daysFromMonday))
            {
                resolved = default!;
                return false;
            }
            start = selectedDate.Date.AddDays(-daysFromMonday);
            if (start > DateTime.MaxValue.Date.AddDays(-7))
            {
                resolved = default!;
                return false;
            }
            endExclusive = start.AddDays(7);
            label = $"{start:dd/MM/yyyy} - {endExclusive.AddDays(-1):dd/MM/yyyy}";
        }
        else
        {
            start = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            if (start.Year == DateTime.MaxValue.Year && start.Month == DateTime.MaxValue.Month)
            {
                resolved = default!;
                return false;
            }
            endExclusive = start.AddMonths(1);
            label = start.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("th-TH"));
        }

        resolved = new AccountingPeriod(
            key,
            selectedDate.Date,
            start,
            endExclusive,
            key == "month" ? selectedDate.ToString("yyyy-MM", CultureInfo.InvariantCulture) :
                selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            label);
        return true;
    }

    private static string BuildExportFileName(AccountingPeriod period, string extension)
    {
        var lastDay = period.EndExclusive.AddDays(-1);
        return $"accounting-{period.Key}-{period.Start:yyyyMMdd}-{lastDay:yyyyMMdd}.{extension}";
    }

    private sealed record AccountingPeriod(
        string Key,
        DateTime SelectedDate,
        DateTime Start,
        DateTime EndExclusive,
        string InputValue,
        string Label);
}
