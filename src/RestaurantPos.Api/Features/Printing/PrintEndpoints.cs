using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Api.Features.Printing;

/// <param name="Text">The bill as 48-column plain text (what the screen shows).</param>
/// <param name="EscPosBase64">The same bill as ESC/POS printer bytes, base64-encoded.</param>
/// <param name="IsDraft">True for a preview of a bill that has no number yet.</param>
public record PrintOutput(BillDto Bill, bool Duplicate, bool IsDraft, string Text, string EscPosBase64);

/// <summary>
/// Bill printing. Sending the bytes to the Windows printer is not built yet (it will be done on the Windows laptop);
/// for now the screen shows the text and can print it through the browser.
/// </summary>
public static class PrintEndpoints
{
    public static RouteGroupBuilder MapPrintEndpoints(this RouteGroupBuilder api)
    {
        // Print: the first print finalises the bill (gives the number); later prints are marked DUPLICATE.
        api.MapPost("/bills/{id:int}/print", async (int id, BillService bills, PosDbContext db, TimeProvider clock) =>
        {
            var (bill, duplicate) = await bills.RecordPrintAsync(id);
            return Output(bill, await db.GetSettingsAsync(), duplicate, clock.GetUtcNow());
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

        return api;
    }

    private static PrintOutput Output(Bill bill, RestaurantSettings shop, bool duplicate, DateTimeOffset now)
    {
        var doc = BillPrintLayout.Build(bill, shop, duplicate, now);
        return new PrintOutput(BillEndpoints.ToDto(bill), duplicate, bill.BillNo is null, doc.ToText(), Convert.ToBase64String(EscPos.Render(doc)));
    }
}
