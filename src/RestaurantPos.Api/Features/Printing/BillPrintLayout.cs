using System.Globalization;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;

namespace RestaurantPos.Api.Features.Printing;

/// <summary>
/// The printed bill for an 80 mm printer: 48 characters per line in the printer's own font (direct printing), or 42
/// for browser printing (Consolas 3 mm bold, chosen by the family on the real paper). Shows the restaurant name, address, phone, GSTIN,
/// FSSAI no., document title (Tax Invoice / Bill of Supply), bill no., date and time, table or "Takeaway", items with
/// qty, rate and amount, subtotal, discount, taxable value, CGST/SGST with rate and amount, round off, grand total,
/// payment method(s) and the footer. Reprints are marked "DUPLICATE".
/// </summary>
public static class BillPrintLayout
{
    /// <summary>Characters per line in the printer's own font on 80 mm paper.</summary>
    public const int Width = 48;

    /// <summary>Characters per line when printing through the browser: 42 at Consolas 3 mm bold is 69.3 mm of 72 mm.</summary>
    public const int BrowserWidth = 42;

    public const string CompositionDeclaration = "Composition taxable person, not eligible to collect tax on supplies";

    public static PrintDocument Build(Bill bill, RestaurantSettings shop, bool duplicate, DateTimeOffset? reprintedAt = null, int width = Width)
    {
        var doc = new PrintDocument(width);
        var totals = BillService.Totals(bill);

        // Item | Qty | Rate | Amount: 21 + 5 + 10 + 12 = 48, or 20 + 4 + 8 + 10 = 42.
        var (itemCol, qtyCol, rateCol, amountCol) = width >= Width ? (21, 5, 10, 12) : (width - 22, 4, 8, 10);
        string Row(string item, string qty, string rate, string amount) =>
            item.PadRight(itemCol) + qty.PadLeft(qtyCol) + rate.PadLeft(rateCol) + amount.PadLeft(amountCol);

        // Header: the name in large letters when it fits on two large lines ("THE GOLDEN HEARTH" / "RESTAURANT").
        var name = shop.Name.Trim().ToUpperInvariant();
        var large = PrintDocument.Wrap(name, doc.WidthFor(PrintSize.Double)).Count() <= 2;
        doc.Center(name, bold: true, size: large ? PrintSize.Double : PrintSize.Normal);
        if (!string.IsNullOrWhiteSpace(shop.Address)) doc.Center(shop.Address.Trim());
        if (!string.IsNullOrWhiteSpace(shop.Phone)) doc.Center($"Ph: {shop.Phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(shop.Gstin)) doc.Center($"GSTIN: {shop.Gstin}");
        if (!string.IsNullOrWhiteSpace(shop.FssaiNo)) doc.Center($"FSSAI Lic. No: {shop.FssaiNo}");
        doc.Rule('=');
        doc.Center(totals.DocumentTitle.ToUpperInvariant(), bold: true);
        if (duplicate) doc.Center("DUPLICATE", bold: true, size: PrintSize.Double);
        if (bill.BillNo is null) doc.Center("*** DRAFT - NOT A BILL ***", bold: true);
        doc.Rule();

        // Bill number, date, table
        var when = IndiaTime.ToIndia(bill.FinalisedAt ?? bill.OpenedAt);
        doc.LeftRight($"Bill No: {bill.BillNo ?? "-"}", $"Date: {when.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}");
        doc.LeftRight(bill.OrderType == OrderType.DineIn ? $"Table: {bill.TableLabel}" : "Takeaway",
            $"Time: {when.ToString("hh:mm tt", CultureInfo.InvariantCulture)}");
        if (duplicate && reprintedAt is { } r)
        {
            doc.Add($"Reprinted: {IndiaTime.ToIndia(r).ToString("dd-MM-yyyy hh:mm tt", CultureInfo.InvariantCulture)}");
        }
        doc.Rule();

        // Items
        doc.Lines.Add(new PrintLine(Row("Item", "Qty", "Rate", "Amount"), Bold: true));
        doc.Rule();
        var lines = BillService.Active(bill).ToList();
        foreach (var l in lines)
        {
            var label = l.VariantName is "" or "Regular" ? l.ItemName : $"{l.ItemName} ({l.VariantName})";
            var parts = PrintDocument.Wrap(label, itemCol).ToList();
            var qty = l.Qty.ToString(CultureInfo.InvariantCulture);
            var rate = Money.Format(l.UnitPricePaise);
            var amount = Money.Format(l.UnitPricePaise * l.Qty);
            if (qty.Length < qtyCol && rate.Length < rateCol && amount.Length < amountCol)
            {
                doc.Lines.Add(new PrintLine(Row(parts[0], qty, rate, amount)));
                foreach (var more in parts.Skip(1)) doc.Lines.Add(new PrintLine("  " + more));
            }
            else
            {
                // Numbers too wide for the columns (very large amounts): name first, then the numbers on their own line.
                doc.Lines.Add(new PrintLine(parts[0]));
                foreach (var more in parts.Skip(1)) doc.Lines.Add(new PrintLine("  " + more));
                doc.Add($"{qty} x {rate} = {amount}", PrintAlign.Right);
            }
        }
        doc.Rule();

        // Totals
        doc.LeftRight($"Subtotal ({lines.Sum(l => l.Qty)} items)", Money.Format(totals.SubtotalPaise));
        if (totals.DiscountPaise > 0)
        {
            var pct = bill.DiscountKind == DiscountKind.Percent ? $" ({Money.Percent((int)bill.DiscountValue)}%)" : "";
            doc.LeftRight($"Discount{pct}", "-" + Money.Format(totals.DiscountPaise));
        }
        if (totals.TaxMode == TaxMode.Regular)
        {
            doc.LeftRight("Taxable value", Money.Format(totals.TaxablePaise));
            var taxed = totals.TaxGroups.Where(g => g.GstRateBp > 0).ToList();
            foreach (var g in taxed)
            {
                var on = taxed.Count > 1 || totals.TaxGroups.Count > 1 ? $" on {Money.Format(g.TaxablePaise)}" : "";
                doc.LeftRight($"CGST @ {Money.Percent(g.HalfRateBp)}%{on}", Money.Format(g.CgstPaise));
                doc.LeftRight($"SGST @ {Money.Percent(g.HalfRateBp)}%{on}", Money.Format(g.SgstPaise));
            }
        }
        doc.LeftRight("Round off", (totals.RoundOffPaise > 0 ? "+" : "") + Money.Format(totals.RoundOffPaise));
        doc.Rule('=');
        // Large and bold; "TOTAL" when "GRAND TOTAL" and the amount do not fit on one large line.
        var total = $"Rs. {Money.Format(totals.TotalPaise)}";
        var room = doc.WidthFor(PrintSize.Double) - total.Length - 1;
        if (room >= "GRAND TOTAL".Length) doc.LeftRight("GRAND TOTAL", total, bold: true, size: PrintSize.Double);
        else if (room >= "TOTAL".Length) doc.LeftRight("TOTAL", total, bold: true, size: PrintSize.Double);
        else doc.LeftRight("GRAND TOTAL", total, bold: true);
        doc.Rule('=');

        // Payment
        if (bill.Payments.Count > 0)
        {
            var paid = string.Join(", ", bill.Payments.Select(p => $"{MethodName(p.Method)} {Money.Format(p.AmountPaise)}"));
            doc.Add($"Paid by: {paid}");
        }
        else if (bill.Status == BillStatus.Cancelled)
        {
            doc.Add("Payment: - (cancelled)");
        }
        else
        {
            doc.Add("Payment: Pending");
        }
        if (bill.Status == BillStatus.Cancelled) doc.Center("*** CANCELLED ***", bold: true);

        // Footer
        if (totals.TaxMode == TaxMode.Composition)
        {
            doc.Rule();
            doc.Center(CompositionDeclaration);
        }
        if (!string.IsNullOrWhiteSpace(shop.BillFooter))
        {
            doc.Rule();
            doc.Center(shop.BillFooter.Trim());
        }
        return doc;
    }

    public static string MethodName(PaymentMethod m) => m switch
    {
        PaymentMethod.Upi => "UPI",
        _ => m.ToString(),
    };
}
