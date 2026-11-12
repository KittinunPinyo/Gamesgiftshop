using System;
using System.Collections.Generic;

namespace Gamesgiftshop.Models;

public partial class Useraddress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string AddressDetail { get; set; } = null!;

    public bool IsDefault { get; set; }

    public virtual User User { get; set; } = null!;
}
