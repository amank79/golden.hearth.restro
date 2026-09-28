namespace RestaurantPos.Api.Data;

// Money is stored as whole paise (long) everywhere: SQLite has no exact decimal type.
// Rates are stored in basis points (500 = 5.00%).

public enum TaxMode { Regular, Composition }
public enum FoodType { Veg, NonVeg, Egg }
public enum OrderType { DineIn, Takeaway }
public enum BillStatus { Open, Paid, Cancelled }
public enum PaymentMethod { Cash, Upi, Card }

/// <summary>Single row (Id = 1) holding the restaurant details printed on every bill.</summary>
public class RestaurantSettings
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "Your Restaurant";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string FssaiNo { get; set; } = "";
    public TaxMode TaxMode { get; set; } = TaxMode.Regular;
    public int GstRateBp { get; set; } = 500;
    public string BillFooter { get; set; } = "Thank you! Visit again.";
    public string? OwnerPinHash { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<MenuItem> Items { get; set; } = [];
}

public class MenuItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Name { get; set; } = "";
    public string? ShortCode { get; set; }
    public string? Description { get; set; }
    public FoodType FoodType { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public List<ItemVariant> Variants { get; set; } = [];
}

/// <summary>A priced option of an item. Items without Half/Full have one variant named "Regular".</summary>
public class ItemVariant
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string Name { get; set; } = "Regular";
    public long PricePaise { get; set; }
    public int SortOrder { get; set; }
}

public class Bill
{
    public int Id { get; set; }

    // Assigned only when the bill is finalised (first print), so open orders never use up a number.
    // FinancialYear is like "2026-27"; SeqNo restarts at 1 every 1 April; BillNo = "2026-27/000123".
    public string? FinancialYear { get; set; }
    public int? SeqNo { get; set; }
    public string? BillNo { get; set; }

    public OrderType OrderType { get; set; }
    public string? TableLabel { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Open;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? FinalisedAt { get; set; }
    public DateTimeOffset? SettledAt { get; set; }

    public long SubtotalPaise { get; set; }
    public long DiscountPaise { get; set; }
    public string? DiscountReason { get; set; }
    public long TaxablePaise { get; set; }
    public long CgstPaise { get; set; }
    public long SgstPaise { get; set; }
    public long RoundOffPaise { get; set; }
    public long TotalPaise { get; set; }

    public string? CancelReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public List<BillLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

/// <summary>Names and price are copied from the menu so later menu changes never alter old bills.</summary>
public class BillLine
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int MenuItemId { get; set; }
    public int ItemVariantId { get; set; }
    public string ItemName { get; set; } = "";
    public string VariantName { get; set; } = "";
    public long UnitPricePaise { get; set; }
    public int Qty { get; set; }
    public string? Note { get; set; }
    public long LineTotalPaise { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public PaymentMethod Method { get; set; }
    public long AmountPaise { get; set; }
    public DateTimeOffset At { get; set; }
}
