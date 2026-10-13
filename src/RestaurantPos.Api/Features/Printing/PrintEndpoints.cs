using System.Globalization;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Api.Features.Printing;

/// <param name="Text">The bill as 42-column plain text (the browser-print layout).</param>
/// <param name="EscPosBase64">The bill as ESC/POS bytes for the bill printer (48 columns), base64-encoded.</param>
/// <param name="IsDraft">True for a preview of a bill that has no number yet.</param>
/// <param name="PrintedTo">The printer the bill was sent to, or null if it was not sent to one.</param>
/// <param name="PrinterError">Why the bill printer did not take it (the bill is still numbered and counted as printed).</param>
public record PrintOutput(BillDto Bill, bool Duplicate, bool IsDraft, string Text, string EscPosBase64,
    string? PrintedTo = null, string? PrinterError = null)
{
    /// <summary>The browser-print layout line by line, with bold and large lines marked (what the screen shows).</summary>
    public IReadOnlyList<PrintTextLine> Lines { get; init; } = [];
}

/// <summary>The printers Windows has, and the one bills go to ("" = print through the browser).</summary>
public record PrintersDto(IReadOnlyList<string> Printers, string Current);

public record PrinterInput(string? PrinterName);

/// <summary>
/// Bill printing. With a bill printer chosen in Settings, bills go straight to it as ESC/POS (its own font, auto cut);
/// without one, the screen shows the text and prints it through the browser.
/// </summary>
public static class PrintEndpoints
{
    public static IServiceCollection AddBillPrinter(this IServiceCollection services) =>
        services.AddSingleton<IBillPrinter, WindowsBillPrinter>();

    public static RouteGroupBuilder MapPrintEndpoints(this RouteGroupBuilder api)
    {
        // Print: the first print finalises the bill (gives the number); later prints are marked DUPLICATE.
        api.MapPost("/bills/{id:int}/print", async (int id, BillService bills, PosDbContext db, TimeProvider clock, IBillPrinter printer, ILoggerFactory logs) =>
        {
            var (bill, duplicate) = await bills.RecordPrintAsync(id);
            return Send(bill, await db.GetSettingsAsync(), duplicate, clock.GetUtcNow(), printer, logs);
        });

        // Send the last print to the bill printer again (e.g. after fixing the paper), without counting another print.
        api.MapPost("/bills/{id:int}/print/resend", async (int id, BillService bills, PosDbContext db, TimeProvider clock, IBillPrinter printer, ILoggerFactory logs) =>
        {
            var bill = await bills.LoadAsync(id);
            if (bill.PrintCount == 0) throw new RuleException("This bill has not been printed yet. Use Print.");
            var shop = await db.GetSettingsAsync();
            if (shop.PrinterName.Length == 0) throw new RuleException("No bill printer is chosen. Choose it in Settings > Bill printer.");
            return Send(bill, shop, bill.PrintCount > 1, clock.GetUtcNow(), printer, logs);
        });

        // What the next print would look like, without printing or finalising anything.
        api.MapGet("/bills/{id:int}/print-preview", async (int id, BillService bills, PosDbContext db, TimeProvider clock) =>
        {
            var bill = await bills.LoadAsync(id);
            return Output(bill, await db.GetSettingsAsync(), bill.PrintCount > 0, clock.GetUtcNow());
        });

        // The preview as raw ESC/POS bytes, e.g. to test a printer with a raw-print tool.
        api.MapGet("/bills/{id:int}/print-preview/escpos", async (int id, BillService bills, PosDbContext db, TimeProvider clock) =>
        {
            var bill = await bills.LoadAsync(id);
            var doc = BillPrintLayout.Build(bill, await db.GetSettingsAsync(), bill.PrintCount > 0, clock.GetUtcNow());
            return Results.File(EscPos.Render(doc), "application/octet-stream", $"bill-{bill.Id}.bin");
        });

        var printers = api.MapGroup("/printers");

        printers.MapGet("", async (PosDbContext db, IBillPrinter printer) =>
            new PrintersDto(printer.InstalledPrinters(), (await db.GetSettingsAsync()).PrinterName));

        // Choose the bill printer ("" = print through the browser). Saved straight away.
        printers.MapPut("/current", async (PrinterInput input, PosDbContext db, IBillPrinter printer) =>
        {
            var name = (input.PrinterName ?? "").Trim();
            var installed = printer.InstalledPrinters();
            var v = new Validation()
                .Check(name.Length == 0 || installed.Contains(name, StringComparer.OrdinalIgnoreCase), "printerName",
                    $"Windows has no printer called \"{name}\". Install its driver first.");
            if (!v.IsValid) return v.Problem();

            var s = await db.GetSettingsAsync();
            s.PrinterName = installed.FirstOrDefault(p => p.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? "";
            await db.SaveChangesAsync();
            return Results.Ok(new PrintersDto(installed, s.PrinterName));
        });

        // A short test slip, to check the printer, the paper width and the cutter.
        printers.MapPost("/test", async (PrinterInput input, PosDbContext db, TimeProvider clock, IBillPrinter printer) =>
        {
            var shop = await db.GetSettingsAsync();
            var name = string.IsNullOrWhiteSpace(input.PrinterName) ? shop.PrinterName : input.PrinterName.Trim();
            if (name.Length == 0) throw new RuleException("Choose a bill printer first.");
            try
            {
                printer.Send(name, EscPos.Render(TestSlip(shop, clock.GetUtcNow())), "Restaurant POS printer test");
            }
            catch (PrinterException e)
            {
                throw new RuleException(e.Message);
            }
            return Results.Ok(new { printedTo = name });
        });

        return api;
    }

    public static PrintDocument TestSlip(RestaurantSettings shop, DateTimeOffset now)
    {
        var doc = new PrintDocument(BillPrintLayout.Width);
        var name = shop.Name.Trim().ToUpperInvariant();
        doc.Center(name, bold: true, size: name.Length <= doc.Width / 2 ? PrintSize.Double : PrintSize.Normal);
        doc.Rule('=');
        doc.Center("PRINTER TEST", bold: true);
        doc.Center(IndiaTime.ToIndia(now).ToString("dd-MM-yyyy hh:mm tt", CultureInfo.InvariantCulture));
        doc.Rule();
        doc.Add("The line of numbers below must fit on one line:");
        doc.Add(string.Concat(Enumerable.Range(0, doc.Width).Select(i => (char)('0' + (i + 1) % 10))));
        doc.LeftRight("Normal text", "Rs. 1,234.50");
        doc.LeftRight("Bold text", "Rs. 1,234.50", bold: true);
        doc.Add("GRAND TOTAL", PrintAlign.Center, bold: true, size: PrintSize.Double);
        doc.Rule();
        doc.Center("If you can read this, bills will print here.");
        return doc;
    }

    private static PrintOutput Send(Bill bill, RestaurantSettings shop, bool duplicate, DateTimeOffset now, IBillPrinter printer, ILoggerFactory logs)
    {
        var output = Output(bill, shop, duplicate, now);
        if (shop.PrinterName.Length == 0) return output;
        try
        {
            printer.Send(shop.PrinterName, Convert.FromBase64String(output.EscPosBase64), $"Bill {bill.BillNo}");
            return output with { PrintedTo = shop.PrinterName };
        }
        catch (PrinterException e)
        {
            logs.CreateLogger("Printing").LogWarning("Bill {BillNo} was not printed on {Printer}: {Reason}", bill.BillNo, shop.PrinterName, e.Message);
            return output with { PrinterError = e.Message };
        }
    }

    private static PrintOutput Output(Bill bill, RestaurantSettings shop, bool duplicate, DateTimeOffset now)
    {
        var printer = BillPrintLayout.Build(bill, shop, duplicate, now);
        var browser = BillPrintLayout.Build(bill, shop, duplicate, now, BillPrintLayout.BrowserWidth);
        return new PrintOutput(BillEndpoints.ToDto(bill), duplicate, bill.BillNo is null, browser.ToText(), Convert.ToBase64String(EscPos.Render(printer)))
        {
            Lines = browser.ToLines(),
        };
    }
}
