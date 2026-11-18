namespace Gamesgiftshop.Models;

public class UserFavorite
{
    public int UserId { get; set; }

    public int ProductId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
