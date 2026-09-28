using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Api.Features.Billing;

/// <summary>
/// Everything that changes a bill. A bill goes Open → (finalised: numbered and frozen) → Paid, or to Cancelled from
/// any state. Bills and their lines are never deleted. Once finalised (first print or payment) the lines, discount
/// and table cannot change; to fix a printed bill, cancel it with a reason and copy it to a new bill.
/// </summary>
public class BillService(PosDbContext db, TimeProvider clock)
{
    public const int MaxQty = 999;
    public const int MaxTableLabel = 20;

    public async Task<Bill> LoadAsync(int id) =>
        await db.Bills
            .Include(b => b.Lines.Where(l => l.RemovedAt == null).OrderBy(l => l.Id))
            .Include(b => b.Payments.OrderBy(p => p.Id))
            .AsSplitQuery()
            .SingleOrDefaultAsync(b => b.Id == id)
        ?? throw RuleException.NotFound("Bill");

    public async Task<Bill> OpenAsync(OpenBillInput input)
    {
        var table = CleanTable(input);
        if (table is not null && await db.Bills.AnyAsync(b => b.Status == BillStatus.Open && b.TableLabel == table))
        {
            throw new RuleException($"Table {table} already has an open bill.");
        }

        var settings = await db.GetSettingsAsync();
        var bill = new Bill
        {
            OrderType = input.OrderType,
            TableLabel = table,
            OpenedAt = clock.GetUtcNow(),
            TaxMode = settings.TaxMode,
        };
        db.Bills.Add(bill);
        await db.SaveChangesAsync();
        return bill;
    }

    /// <summary>Fix the order type or table number of a bill that is not printed yet.</summary>
    public async Task<Bill> ChangeTableAsync(int id, OpenBillInput input)
    {
        var bill = await EditableAsync(id);
        var table = CleanTable(input);
        if (table is not null && await db.Bills.AnyAsync(b => b.Id != id && b.Status == BillStatus.Open && b.TableLabel == table))
        {
            throw new RuleException($"Table {table} already has an open bill.");
        }
        bill.OrderType = input.OrderType;
        bill.TableLabel = table;
        await db.SaveChangesAsync();
        return bill;
    }

    /// <summary>Adds a dish. The same size with the same note goes onto the existing line (quantity + n).</summary>
    public async Task<Bill> AddLineAsync(int id, AddLineInput input)
    {
        CheckQty(input.Qty);
        var note = CleanNote(input.Note);
        var bill = await EditableAsync(id);

        var variant = await db.ItemVariants.SingleOrDefaultAsync(v => v.Id == input.ItemVariantId)
            ?? throw RuleException.Invalid("This dish is not on the menu.");
        var item = await db.MenuItems.Include(i => i.Category).SingleAsync(i => i.Id == variant.MenuItemId);
        if (!variant.IsActive || !item.IsActive || item.Category is { IsActive: false })
        {
            throw RuleException.Invalid($"{item.Name} is no longer on the menu.");
        }
        if (!item.IsAvailable)
        {
            throw RuleException.Invalid($"{item.Name} is marked not available today.");
        }

        var settings = await db.GetSettingsAsync();
        var rate = item.GstRateBp ?? settings.GstRateBp;
        var existing = Active(bill).FirstOrDefault(l =>
            l.ItemVariantId == variant.Id && l.Note == note && l.UnitPricePaise == variant.PricePaise && l.GstRateBp == rate);
        if (existing is not null)
        {
            CheckQty(existing.Qty + input.Qty);
            existing.Qty += input.Qty;
        }
        else
        {
            // Copy name, size, price and tax rate so later menu changes never alter this bill (MENU-7).
            bill.Lines.Add(new BillLine
            {
                MenuItemId = item.Id,
                ItemVariantId = variant.Id,
                ItemName = item.Name,
                VariantName = variant.Name,
                UnitPricePaise = variant.PricePaise,
                GstRateBp = rate,
                Qty = input.Qty,
                Note = note,
            });
        }
        return await RecalculateAndSaveAsync(bill, settings);
    }

    public async Task<Bill> ChangeLineAsync(int id, int lineId, ChangeLineInput input)
    {
        CheckQty(input.Qty);
        var bill = await EditableAsync(id);
        var line = Active(bill).SingleOrDefault(l => l.Id == lineId) ?? throw RuleException.NotFound("Line");
        line.Qty = input.Qty;
        line.Note = CleanNote(input.Note);
        return await RecalculateAndSaveAsync(bill);
    }

    /// <summary>Takes a line off the bill. The row is kept with RemovedAt set.</summary>
    public async Task<Bill> RemoveLineAsync(int id, int lineId)
    {
        var bill = await EditableAsync(id);
        var line = Active(bill).SingleOrDefault(l => l.Id == lineId) ?? throw RuleException.NotFound("Line");
        // Never removed from bill.Lines: EF would delete the row. It stays, marked as removed.
        line.RemovedAt = clock.GetUtcNow();
        return await RecalculateAndSaveAsync(bill);
    }

    public async Task<Bill> SetDiscountAsync(int id, DiscountInput input)
    {
        var bill = await EditableAsync(id);
        var reason = ApiErrors.Clean(input.Reason);
        switch (input.Kind)
        {
            case DiscountKind.None:
                bill.DiscountKind = DiscountKind.None;
                bill.DiscountValue = 0;
                bill.DiscountReason = null;
                break;
            case DiscountKind.Percent or DiscountKind.Amount:
                if (reason is null) throw RuleException.Invalid("Please give a reason for the discount.");
                if (reason.Length > 100) throw RuleException.Invalid("The discount reason can have at most 100 characters.");
                if (input.Kind == DiscountKind.Percent && input.Value is <= 0 or > Money.BasisPoints)
                {
                    throw RuleException.Invalid("Discount percent must be more than 0 and at most 100.");
                }
                var subtotal = Active(bill).Sum(l => l.UnitPricePaise * l.Qty);
                if (input.Kind == DiscountKind.Amount && (input.Value <= 0 || input.Value > subtotal))
                {
                    throw RuleException.Invalid($"Discount must be more than ₹0 and at most the items total (₹{Money.Format(subtotal)}).");
                }
                bill.DiscountKind = input.Kind;
                bill.DiscountValue = input.Value;
                bill.DiscountReason = reason;
                break;
            default:
                throw RuleException.Invalid("Unknown discount type.");
        }
        return await RecalculateAndSaveAsync(bill);
    }

    /// <summary>
    /// Freezes the bill and gives it the next bill number (first print). Does nothing if it already has a number.
    /// </summary>
    public async Task<Bill> FinaliseAsync(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var bill = await LoadAsync(id);
        await FinaliseInTransactionAsync(bill);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return bill;
    }

    private async Task FinaliseInTransactionAsync(Bill bill)
    {
        if (bill.BillNo is not null) return;
        if (bill.Status != BillStatus.Open) throw new RuleException("This bill is cancelled.");
        if (!Active(bill).Any()) throw new RuleException("Add at least one dish before printing the bill.");

        Recalculate(bill, await db.GetSettingsAsync()); // freeze with the tax mode in force now
        await BillNumbering.AssignAsync(db, bill, clock.GetUtcNow());
    }

    /// <summary>
    /// Records how the customer paid (cash / UPI / card, split allowed) and closes the bill. The payments must add
    /// up exactly to the grand total. Finalises the bill first if it was not printed yet.
    /// </summary>
    public async Task<Bill> SettleAsync(int id, SettleInput input)
    {
        var payments = input.Payments ?? [];
        if (payments.Any(p => p.AmountPaise <= 0)) throw RuleException.Invalid("Every payment amount must be more than ₹0.");
        if (payments.Any(p => !Enum.IsDefined(p.Method))) throw RuleException.Invalid("Unknown payment method.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var bill = await LoadAsync(id);
        if (bill.Status == BillStatus.Paid) throw new RuleException("This bill is already paid.");
        if (bill.Status == BillStatus.Cancelled) throw new RuleException("This bill is cancelled.");
        await FinaliseInTransactionAsync(bill);

        var sum = payments.Sum(p => p.AmountPaise);
        if (sum != bill.TotalPaise)
        {
            throw RuleException.Invalid(
                $"Payments add up to ₹{Money.Format(sum)} but the bill total is ₹{Money.Format(bill.TotalPaise)}.");
        }

        var now = clock.GetUtcNow();
        // One row per method, in the order cash, UPI, card.
        foreach (var group in payments.GroupBy(p => p.Method).OrderBy(g => g.Key))
        {
            bill.Payments.Add(new Payment { Method = group.Key, AmountPaise = group.Sum(p => p.AmountPaise), At = now });
        }
        bill.Status = BillStatus.Paid;
        bill.SettledAt = now;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return bill;
    }

    /// <summary>
    /// Cancels a bill with a reason (BILL-3). The bill stays, with its number if it had one. Optionally copies its
    /// dishes to a new open bill for the same table, to re-bill after a mistake on a printed bill.
    /// </summary>
    public async Task<(Bill Cancelled, Bill? NewBill)> CancelAsync(int id, CancelInput input)
    {
        var reason = ApiErrors.Clean(input.Reason) ?? throw RuleException.Invalid("Please give a reason for cancelling.");
        if (reason.Length > 200) throw RuleException.Invalid("The reason can have at most 200 characters.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var bill = await LoadAsync(id);
        if (bill.Status == BillStatus.Cancelled) throw new RuleException("This bill is already cancelled.");

        var now = clock.GetUtcNow();
        bill.Status = BillStatus.Cancelled;
        bill.CancelReason = reason;
        bill.CancelledAt = now;

        Bill? copy = null;
        if (input.CopyToNewBill)
        {
            var settings = await db.GetSettingsAsync();
            copy = new Bill { OrderType = bill.OrderType, TableLabel = bill.TableLabel, OpenedAt = now, TaxMode = settings.TaxMode };
            foreach (var l in Active(bill))
            {
                copy.Lines.Add(new BillLine
                {
                    MenuItemId = l.MenuItemId,
                    ItemVariantId = l.ItemVariantId,
                    ItemName = l.ItemName,
                    VariantName = l.VariantName,
                    UnitPricePaise = l.UnitPricePaise,
                    GstRateBp = l.GstRateBp,
                    Qty = l.Qty,
                    Note = l.Note,
                });
            }
            Recalculate(copy, settings);
            db.Bills.Add(copy);
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (bill, copy);
    }

    /// <summary>Records a print (the first print finalises the bill). Returns true if this print is a duplicate.</summary>
    public async Task<(Bill Bill, bool Duplicate)> RecordPrintAsync(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var bill = await LoadAsync(id);
        if (bill.Status == BillStatus.Cancelled) throw new RuleException("A cancelled bill cannot be printed.");
        await FinaliseInTransactionAsync(bill);
        var duplicate = bill.PrintCount > 0;
        bill.PrintCount++;
        bill.LastPrintedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (bill, duplicate);
    }

    /// <summary>A bill whose lines, discount or table can still change: open and not printed.</summary>
    private async Task<Bill> EditableAsync(int id)
    {
        var bill = await LoadAsync(id);
        if (bill.Status == BillStatus.Cancelled) throw new RuleException("This bill is cancelled.");
        if (bill.Status == BillStatus.Paid) throw new RuleException("This bill is already paid.");
        if (bill.BillNo is not null)
        {
            throw new RuleException("This bill is already printed and cannot be changed. Cancel it and make a new bill.");
        }
        return bill;
    }

    private async Task<Bill> RecalculateAndSaveAsync(Bill bill, RestaurantSettings? settings = null)
    {
        Recalculate(bill, settings ?? await db.GetSettingsAsync());
        await db.SaveChangesAsync();
        return bill;
    }

    /// <summary>Updates the stored totals from the lines, discount and tax mode.</summary>
    private static void Recalculate(Bill bill, RestaurantSettings settings)
    {
        if (bill.BillNo is null) bill.TaxMode = settings.TaxMode;
        foreach (var l in bill.Lines) l.LineTotalPaise = l.UnitPricePaise * l.Qty;
        var t = Totals(bill);
        bill.SubtotalPaise = t.SubtotalPaise;
        bill.DiscountPaise = t.DiscountPaise;
        bill.TaxablePaise = t.TaxablePaise;
        bill.CgstPaise = t.CgstPaise;
        bill.SgstPaise = t.SgstPaise;
        bill.RoundOffPaise = t.RoundOffPaise;
        bill.TotalPaise = t.TotalPaise;
    }

    /// <summary>The full calculation (with the per-rate tax breakdown) from what is stored on the bill.</summary>
    public static BillTotals Totals(Bill bill) =>
        BillCalculator.Calculate(
            Active(bill).Select(l => new CalcLine(l.UnitPricePaise, l.Qty, l.GstRateBp)),
            new Discount(bill.DiscountKind, bill.DiscountValue),
            bill.TaxMode);

    /// <summary>Lines still on the bill (removed ones stay in the database with RemovedAt set).</summary>
    public static IEnumerable<BillLine> Active(Bill bill) => bill.Lines.Where(l => l.RemovedAt == null).OrderBy(l => l.Id);

    /// <summary>After the tax mode changes in settings, open bills that are not printed yet follow it.</summary>
    public static async Task RecalculateOpenBillsAsync(PosDbContext db, RestaurantSettings settings)
    {
        var open = await db.Bills.Include(b => b.Lines.Where(l => l.RemovedAt == null))
            .Where(b => b.Status == BillStatus.Open && b.BillNo == null).ToListAsync();
        foreach (var bill in open) Recalculate(bill, settings);
        await db.SaveChangesAsync();
    }

    private static string? CleanTable(OpenBillInput input)
    {
        if (!Enum.IsDefined(input.OrderType)) throw RuleException.Invalid("Choose dine-in or takeaway.");
        if (input.OrderType == OrderType.Takeaway) return null;
        var table = ApiErrors.Clean(input.TableLabel) ?? throw RuleException.Invalid("Enter the table number.");
        if (table.Length > MaxTableLabel) throw RuleException.Invalid($"Table number can have at most {MaxTableLabel} characters.");
        return table;
    }

    private static string? CleanNote(string? note)
    {
        var clean = ApiErrors.Clean(note);
        if (clean is { Length: > 100 }) throw RuleException.Invalid("A note can have at most 100 characters.");
        return clean;
    }

    private static void CheckQty(int qty)
    {
        if (qty is < 1 or > MaxQty) throw RuleException.Invalid($"Quantity must be between 1 and {MaxQty}.");
    }
}
