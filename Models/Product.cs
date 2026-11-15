using System;
using System.Collections.Generic;

namespace Gamesgiftshop.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public string? Category { get; set; }

    public decimal Price { get; set; }

    public decimal? OriginalPrice { get; set; }

    public string? ImageUrl { get; set; }

    public bool? IsTrending { get; set; }

    public string? BadgeType { get; set; }

    public decimal? Rating { get; set; }

    public int? ReviewCount { get; set; }

    public int? StockQuantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Description { get; set; }
    
    public string? WarrantyInfo { get; set; }

    public virtual ICollection<Orderdetail> Orderdetails { get; set; } = new List<Orderdetail>();
}
