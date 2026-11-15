using System.Globalization;
using Gamesgiftshop.Models;
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
    public IActionResult Dashboard(string? month)
    {
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out monthStart))
                return BadRequest("รูปแบบเดือนต้องเป็น yyyy-MM");
        }
        if (monthStart.Year == DateTime.MaxValue.Year && monthStart.Month == DateTime.MaxValue.Month)
            return BadRequest("เดือนไม่ถูกต้อง");

        var monthEnd = monthStart.AddMonths(1);
        var sales = _db.Orders
            .AsNoTracking()
            .Include(order => order.Orderdetails)
                .ThenInclude(detail => detail.Product)
            .Where(order => order.OrderDate.HasValue &&
                order.OrderDate.Value >= monthStart &&
                order.OrderDate.Value < monthEnd &&
                (order.Status == "ชำระเงินสำเร็จแล้ว" ||
                 order.Status == "เตรียมจัดส่ง" ||
                 order.Status == "จัดส่งแล้ว" ||
                 order.Status == "สำเร็จ"))
            .OrderByDescending(order => order.OrderDate)
            .ToList();
        var expenses = _db.AccountingExpenses
            .AsNoTracking()
            .Where(expense => expense.ExpenseDate >= monthStart && expense.ExpenseDate < monthEnd)
            .OrderByDescending(expense => expense.ExpenseDate)
            .ThenByDescending(expense => expense.AccountingExpenseId)
            .ToList();
        var revenue = sales.Sum(order => order.TotalAmount);
        var expenseTotal = expenses.Sum(expense => expense.Amount);

        return View(new AccountingDashboardViewModel
        {
            Month = monthStart,
            PaidOrderCount = sales.Count,
            Revenue = revenue,
            ExpenseTotal = expenseTotal,
            NetIncome = revenue - expenseTotal,
            SalesTransactions = sales,
            Expenses = expenses
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddExpense(string description, string category, decimal amount, DateTime expenseDate, string? month)
    {
        description = description?.Trim() ?? "";
        category = category?.Trim() ?? "";
        if (description.Length is < 1 or > 200 ||
            category.Length is < 1 or > 80 ||
            amount <= 0 || amount > 99999999.99m ||
            expenseDate == default)
        {
            TempData["AccountingError"] = "กรุณากรอกรายละเอียด หมวดหมู่ จำนวนเงิน และวันที่ให้ถูกต้อง";
            return RedirectToAction(nameof(Dashboard), new { month });
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
        return RedirectToAction(nameof(Dashboard), new { month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteExpense(int id, string? month)
    {
        var expense = _db.AccountingExpenses.FirstOrDefault(item => item.AccountingExpenseId == id);
        if (expense == null) return NotFound();

        _db.AccountingExpenses.Remove(expense);
        _db.SaveChanges();
        TempData["AccountingSuccess"] = "ลบรายการรายจ่ายเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Dashboard), new { month });
    }
}
