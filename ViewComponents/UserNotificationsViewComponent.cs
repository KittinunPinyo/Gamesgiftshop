using Gamesgiftshop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamesgiftshop.ViewComponents;

public class UserNotificationsViewComponent : ViewComponent
{
    private readonly Csi402dbContext _db;

    public UserNotificationsViewComponent(Csi402dbContext db)
    {
        _db = db;
    }

    public IViewComponentResult Invoke()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return Content(string.Empty);

        var userId = _db.Users
            .AsNoTracking()
            .Where(user => user.Username == username)
            .Select(user => (int?)user.UserId)
            .FirstOrDefault();
        if (!userId.HasValue) return Content(string.Empty);

        var notifications = _db.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId.Value)
            .OrderBy(notification => notification.IsRead)
            .ThenByDescending(notification => notification.CreatedAt)
            .Take(5)
            .ToList();
        ViewBag.UnreadCount = _db.UserNotifications.Count(notification =>
            notification.UserId == userId.Value && !notification.IsRead);

        return View(notifications);
    }
}
