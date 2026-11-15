namespace Gamesgiftshop.Models;

public partial class Promotion
{
    public int PromotionId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Code { get; set; }

    public string? Category { get; set; }

    public decimal? MinimumOrderAmount { get; set; }

    public decimal? DiscountPercent { get; set; }

    public decimal? DiscountAmount { get; set; }

    public int? BuyQuantity { get; set; }

    public int? FreeQuantity { get; set; }

    public bool FirstOrderOnly { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;
}
