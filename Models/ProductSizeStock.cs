namespace Gamesgiftshop.Models;

public class ProductSizeStock
{
    public static readonly string[] AvailableSizes = ["S", "M", "L", "XL", "2XL"];

    public int ProductId { get; set; }

    public string Size { get; set; } = null!;

    public int StockQuantity { get; set; }

    public virtual Product Product { get; set; } = null!;
}
