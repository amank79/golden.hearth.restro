using System.Net;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Printing;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Tests;

public class BillPrintLayoutTests
{
    private static readonly RestaurantSettings Shop = new()
    {
        Name = "Golden Hearth",
        Address = "12, Station Road, Near Bus Stand, Anand, Gujarat 388001",
        Phone = "98250 12345",
        Gstin = "24ABCDE1234F1Z5",
        FssaiNo = "10726001000123",
        TaxMode = TaxMode.Regular,
        GstRateBp = 500,
        BillFooter = "Thank you! Visit again.",
    };

    /// <summary>A paid dine-in bill: 1160 - 10% = 1044, CGST/SGST 26.10 each, round off -0.20, total 1096.</summary>
    private static Bill SampleBill(TaxMode mode = TaxMode.Regular)
    {
        var at = TestClock.India(2026, 10, 11, 20, 15);
        var bill = new Bill
        {
            Id = 7,
            BillNo = "2026-27/000123",
            FinancialYear = "2026-27",
            SeqNo = 123,
            OrderType = OrderType.DineIn,
            TableLabel = "4",
            Status = BillStatus.Paid,
            TaxMode = mode,
            OpenedAt = at.AddMinutes(-40),
            FinalisedAt = at,
            DiscountKind = DiscountKind.Percent,
            DiscountValue = 1000,
            DiscountReason = "Family",
            Lines =
            [
                new BillLine { Id = 1, ItemName = "Chicken Tikka", VariantName = "Regular", UnitPricePaise = 32000, Qty = 1, GstRateBp = 500 },
                new BillLine { Id = 2, ItemName = "Butter Chicken", VariantName = "Full", UnitPricePaise = 36000, Qty = 1, GstRateBp = 500 },
                new BillLine { Id = 3, ItemName = "Garlic Naan", VariantName = "Regular", UnitPricePaise = 8000, Qty = 3, GstRateBp = 500 },
                new BillLine { Id = 4, ItemName = "Jeera Rice", VariantName = "Regular", UnitPricePaise = 16000, Qty = 1, GstRateBp = 500 },
                new BillLine { Id = 5, ItemName = "Soft Drink", VariantName = "Regular", UnitPricePaise = 4000, Qty = 2, GstRateBp = 500 },
                new BillLine { Id = 6, ItemName = "Removed Dish", VariantName = "Regular", UnitPricePaise = 99900, Qty = 1, GstRateBp = 500, RemovedAt = at },
            ],
        };
        var t = BillService.Totals(bill);
        bill.TotalPaise = t.TotalPaise;
        bill.Payments = [new Payment { Method = PaymentMethod.Cash, AmountPaise = 50000 }, new Payment { Method = PaymentMethod.Upi, AmountPaise = t.TotalPaise - 50000 }];
        return bill;
    }

    [Fact]
    public void Regular_bill_matches_the_expected_layout()
    {
        var text = BillPrintLayout.Build(SampleBill(), Shop, duplicate: false).ToText();
        const string expected = """
                             GOLDEN HEARTH
            12, Station Road, Near Bus Stand, Anand, Gujarat
                                 388001
                            Ph: 98250 12345
                         GSTIN: 24ABCDE1234F1Z5
                     FSSAI Lic. No: 10726001000123
            ================================================
                              TAX INVOICE
            ------------------------------------------------
            Bill No: 2026-27/000123         Date: 11-10-2026
            Table: 4                          Time: 08:15 PM
            ------------------------------------------------
            Item                   Qty      Rate      Amount
            ------------------------------------------------
            Chicken Tikka            1    320.00      320.00
            Butter Chicken (Full)    1    360.00      360.00
            Garlic Naan              3     80.00      240.00
            Jeera Rice               1    160.00      160.00
            Soft Drink               2     40.00       80.00
            ------------------------------------------------
            Subtotal (8 items)                      1,160.00
            Discount (10%)                           -116.00
            Taxable value                           1,044.00
            CGST @ 2.5%                                26.10
            SGST @ 2.5%                                26.10
            Round off                                  -0.20
            ================================================
            GRAND TOTAL Rs. 1,096.00
            ================================================
            Paid by: Cash 500.00, UPI 596.00
            ------------------------------------------------
                        Thank you! Visit again.

            """;
        Assert.Equal(expected.Replace("\r", ""), text);
    }

    [Fact]
    public void Browser_layout_is_42_characters_with_a_large_total()
    {
        var shop = new RestaurantSettings
        {
            Name = "The Golden Hearth Restaurant", Address = Shop.Address, Phone = Shop.Phone, Gstin = Shop.Gstin,
            FssaiNo = Shop.FssaiNo, GstRateBp = Shop.GstRateBp, BillFooter = Shop.BillFooter,
        };
        var doc = BillPrintLayout.Build(SampleBill(), shop, duplicate: false, width: BillPrintLayout.BrowserWidth);
        var actual = string.Join("\n", doc.ToLines().Select(l => (l.Big ? "[big] " : l.Bold ? "[b] " : "") + l.Text));
        const string expected = """
            [big]   THE GOLDEN HEARTH
            [big]      RESTAURANT
             12, Station Road, Near Bus Stand, Anand,
                          Gujarat 388001
                         Ph: 98250 12345
                      GSTIN: 24ABCDE1234F1Z5
                  FSSAI Lic. No: 10726001000123
            ==========================================
            [b]                TAX INVOICE
            ------------------------------------------
            Bill No: 2026-27/000123   Date: 11-10-2026
            Table: 4                    Time: 08:15 PM
            ------------------------------------------
            [b] Item                 Qty    Rate    Amount
            ------------------------------------------
            Chicken Tikka          1  320.00    320.00
            Butter Chicken         1  360.00    360.00
              (Full)
            Garlic Naan            3   80.00    240.00
            Jeera Rice             1  160.00    160.00
            Soft Drink             2   40.00     80.00
            ------------------------------------------
            Subtotal (8 items)                1,160.00
            Discount (10%)                     -116.00
            Taxable value                     1,044.00
            CGST @ 2.5%                          26.10
            SGST @ 2.5%                          26.10
            Round off                            -0.20
            ==========================================
            [big] TOTAL    Rs. 1,096.00
            ==========================================
            Paid by: Cash 500.00, UPI 596.00
            ------------------------------------------
                     Thank you! Visit again.
            """;
        Assert.Equal(expected.Replace("\r", ""), actual);
        Assert.All(doc.ToLines(), l => Assert.True(l.Text.Length <= (l.Big ? 21 : 42), l.Text));
    }

    [Fact]
    public void Every_line_fits_48_characters()
    {
        var bill = SampleBill();
        bill.Lines.Add(new BillLine { Id = 9, ItemName = "Special Paneer Tikka Masala With Extra Butter And Cheese", VariantName = "Family Pack", UnitPricePaise = 9_999_900, Qty = 999, GstRateBp = 500 });
        var shop = new RestaurantSettings { Name = "A Very Long Restaurant Name That Does Not Fit On One Line", Address = new string('x', 200), BillFooter = "Footer " + new string('y', 120) };
        var doc = BillPrintLayout.Build(bill, shop, duplicate: true, reprintedAt: TestClock.India(2026, 10, 12));

        foreach (var line in doc.ToText().Split('\n')) Assert.True(line.Length <= 48, $"Too long ({line.Length}): {line}");
        Assert.All(doc.Lines.Where(l => l.Size == PrintSize.Double), l => Assert.True(l.Text.Length <= 24));
        Assert.Contains("  Butter And Cheese", doc.ToText()); // long names wrap under the item column
        Assert.Contains("999 x 99,999.00 = 9,98,99,001.00", doc.ToText()); // too wide for the columns: own line
    }

    [Fact]
    public void Shows_every_required_field()
    {
        var text = BillPrintLayout.Build(SampleBill(), Shop, duplicate: false).ToText();
        foreach (var expected in new[]
                 {
                     "GOLDEN HEARTH", "Station Road", "Ph: 98250 12345", "GSTIN: 24ABCDE1234F1Z5", "FSSAI Lic. No: 10726001000123",
                     "TAX INVOICE", "Bill No: 2026-27/000123", "Date: 11-10-2026", "Time: 08:15 PM", "Table: 4",
                     "Qty", "Rate", "Amount", "Subtotal", "Discount (10%)", "Taxable value", "CGST @ 2.5%", "SGST @ 2.5%",
                     "Round off", "GRAND TOTAL", "Rs. 1,096.00", "Paid by: Cash 500.00, UPI 596.00", "Thank you! Visit again.",
                 })
        {
            Assert.Contains(expected, text);
        }
        Assert.DoesNotContain("Removed Dish", text);
        Assert.DoesNotContain("DUPLICATE", text);
        Assert.DoesNotContain("DRAFT", text);
    }

    [Fact]
    public void Reprint_is_marked_duplicate()
    {
        var doc = BillPrintLayout.Build(SampleBill(), Shop, duplicate: true, reprintedAt: TestClock.India(2026, 10, 12, 9, 5));
        var line = Assert.Single(doc.Lines, l => l.Text == "DUPLICATE");
        Assert.True(line.Bold);
        Assert.Equal(PrintSize.Double, line.Size);
        Assert.Contains("Reprinted: 12-10-2026 09:05 AM", doc.ToText());
        Assert.Contains("Date: 11-10-2026", doc.ToText()); // original bill date is kept
    }

    [Fact]
    public void Takeaway_and_pending_payment()
    {
        var bill = SampleBill();
        bill.OrderType = OrderType.Takeaway;
        bill.TableLabel = null;
        bill.Status = BillStatus.Open;
        bill.Payments = [];
        var text = BillPrintLayout.Build(bill, Shop, duplicate: false).ToText();
        Assert.Contains("\nTakeaway ", text);
        Assert.Contains("Payment: Pending", text);
    }

    [Fact]
    public void Composition_bill_is_a_bill_of_supply_without_tax()
    {
        var bill = SampleBill(TaxMode.Composition);
        var text = BillPrintLayout.Build(bill, Shop, duplicate: false).ToText();
        Assert.Contains("BILL OF SUPPLY", text);
        Assert.DoesNotContain("TAX INVOICE", text);
        Assert.DoesNotContain("CGST", text);
        Assert.DoesNotContain("SGST", text);
        Assert.DoesNotContain("Taxable value", text);
        Assert.Contains("Composition taxable person, not eligible to", text);
        Assert.Contains("Rs. 1,044.00", text); // 1160 - 116, no tax
    }

    [Fact]
    public void Mixed_rates_print_a_tax_pair_per_rate()
    {
        var bill = SampleBill();
        bill.Lines.Add(new BillLine { Id = 10, ItemName = "Packaged Water", VariantName = "Regular", UnitPricePaise = 2000, Qty = 1, GstRateBp = 1800 });
        var text = BillPrintLayout.Build(bill, Shop, duplicate: false).ToText();
        Assert.Contains("CGST @ 2.5% on", text);
        Assert.Contains("CGST @ 9% on 18.00", text); // 20 less its 10% share of the discount
        Assert.Contains("SGST @ 9% on 18.00", text);
    }

    [Fact]
    public void Draft_preview_says_it_is_not_a_bill()
    {
        var bill = SampleBill();
        bill.BillNo = null;
        bill.FinalisedAt = null;
        var text = BillPrintLayout.Build(bill, Shop, duplicate: false).ToText();
        Assert.Contains("DRAFT - NOT A BILL", text);
        Assert.Contains("Bill No: -", text);
    }

    [Fact]
    public void Wrap_breaks_at_spaces_and_cuts_long_words()
    {
        Assert.Equal(["Paneer Butter", "Masala"], PrintDocument.Wrap("Paneer Butter Masala", 14));
        Assert.Equal(["abcde", "fgh"], PrintDocument.Wrap("abcdefgh", 5));
        Assert.Equal([""], PrintDocument.Wrap("", 5));
    }
}

public class EscPosTests
{
    [Fact]
    public void Starts_with_initialise_and_ends_with_feed_and_cut()
    {
        var doc = new PrintDocument(48);
        doc.Center("HELLO", bold: true, size: PrintSize.Double);
        doc.LeftRight("Total", "10.00");
        var bytes = EscPos.Render(doc);

        Assert.Equal(EscPos.Initialize, bytes[..2]);
        Assert.Equal(EscPos.FeedAndCut, bytes[^EscPos.FeedAndCut.Length..]);
    }

    [Fact]
    public void Styles_each_line()
    {
        var doc = new PrintDocument(48);
        doc.Center("HELLO", bold: true, size: PrintSize.Double);
        var bytes = EscPos.Render(doc);

        byte[] expectedLine = [0x1B, 0x61, 1, 0x1B, 0x45, 1, 0x1D, 0x21, 0x11, (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O', 0x0A];
        Assert.Equal(expectedLine, bytes[2..(2 + expectedLine.Length)]);
    }

    [Fact]
    public void Non_ascii_becomes_question_mark()
    {
        Assert.Equal("Rs? 5 ????"u8.ToArray(), EscPos.ToPrinterText("Rs₹ 5 पनीर"));
        Assert.True(EscPos.IsPrintable("Paneer 65"));
        Assert.False(EscPos.IsPrintable("पनीर"));
    }

    [Fact]
    public void Bill_bytes_contain_the_text_and_duplicate_mark()
    {
        var shop = new RestaurantSettings { Name = "Golden Hearth", BillFooter = "Thanks" };
        var bill = new Bill
        {
            BillNo = "2026-27/000001", OrderType = OrderType.Takeaway, FinalisedAt = TestClock.India(2026, 10, 11),
            Lines = [new BillLine { ItemName = "Butter Naan", VariantName = "Regular", UnitPricePaise = 6000, Qty = 2, GstRateBp = 500 }],
        };
        var ascii = System.Text.Encoding.ASCII.GetString(EscPos.Render(BillPrintLayout.Build(bill, shop, duplicate: true)));
        Assert.Contains("GOLDEN HEARTH", ascii);
        Assert.Contains("DUPLICATE", ascii);
        Assert.Contains("Butter Naan", ascii);
        Assert.Contains("Rs. 126.00", ascii);
        Assert.DoesNotContain("₹", ascii);
    }
}

public class PrintApiTests(BillFixture f) : IClassFixture<BillFixture>, IAsyncLifetime
{
    private readonly HttpClient _c = f.CreateClient();

    public Task InitializeAsync() => f.EnsureMenu();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task First_print_finalises_later_prints_are_duplicates()
    {
        var bill = await _c.OpenDineIn("Print-1");
        await _c.AddLine(bill.Id, f.Tikka);

        var preview = await _c.Get<PrintOutput>($"/api/bills/{bill.Id}/print-preview");
        Assert.True(preview.IsDraft);
        Assert.False(preview.Duplicate);
        Assert.Contains("DRAFT", preview.Text);
        Assert.Null((await _c.Get<BillDto>($"/api/bills/{bill.Id}")).BillNo); // preview gives no number

        var first = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.False(first.Duplicate);
        Assert.False(first.IsDraft);
        Assert.NotNull(first.Bill.BillNo);
        Assert.Contains(first.Bill.BillNo!, first.Text);
        Assert.DoesNotContain("DUPLICATE", first.Text);
        Assert.Equal(1, first.Bill.PrintCount);

        var bytes = Convert.FromBase64String(first.EscPosBase64);
        Assert.Equal(EscPos.Initialize, bytes[..2]);

        var second = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.True(second.Duplicate);
        Assert.Contains("DUPLICATE", second.Text);
        Assert.Equal(first.Bill.BillNo, second.Bill.BillNo);
        Assert.Equal(2, second.Bill.PrintCount);

        // The preview of a printed bill shows what a reprint would look like.
        var again = await _c.Get<PrintOutput>($"/api/bills/{bill.Id}/print-preview");
        Assert.True(again.Duplicate);
        Assert.Equal(2, again.Bill.PrintCount); // preview does not count as a print
    }

    [Fact]
    public async Task Paid_bill_can_be_reprinted_with_payment_methods()
    {
        var bill = await _c.OpenTakeaway();
        bill = await _c.AddLine(bill.Id, f.Tikka);
        await _c.Pay(bill.Id, new PaymentInput(PaymentMethod.Upi, bill.TotalPaise));

        var reprint = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.Contains("Paid by: UPI 294.00", reprint.Text);
        Assert.Contains("Takeaway", reprint.Text);
        Assert.False(reprint.Duplicate); // paid without printing first: this is the first print
    }

    [Fact]
    public async Task Cancelled_or_empty_bill_cannot_be_printed()
    {
        var empty = await _c.OpenTakeaway();
        Assert.Equal(HttpStatusCode.Conflict, (await _c.PostAsync($"/api/bills/{empty.Id}/print", null)).StatusCode);

        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("test"));
        Assert.Equal(HttpStatusCode.Conflict, (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).StatusCode);

        // History can still show what it looked like.
        var preview = await _c.Get<PrintOutput>($"/api/bills/{bill.Id}/print-preview");
        Assert.Contains("CANCELLED", preview.Text);
    }

    [Fact]
    public async Task Escpos_download()
    {
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.DalFull);
        var response = await _c.GetAsync($"/api/bills/{bill.Id}/print-preview/escpos");
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(EscPos.FeedAndCut, bytes[^EscPos.FeedAndCut.Length..]);
    }
}

/// <summary>Bills sent straight to the bill printer chosen in Settings (a fake printer here).</summary>
public class DirectPrintTests(BillFixture f) : IClassFixture<BillFixture>, IAsyncLifetime
{
    private readonly HttpClient _c = f.CreateClient();

    public async Task InitializeAsync()
    {
        await f.EnsureMenu();
        f.Printer.Failure = null;
        f.Printer.Jobs.Clear();
        await _c.Put("/api/printers/current", new PrinterInput("POS-80C"));
    }

    public async Task DisposeAsync() => await _c.Put("/api/printers/current", new PrinterInput(""));

    [Fact]
    public async Task Lists_printers_and_saves_the_chosen_one()
    {
        var list = await _c.Get<PrintersDto>("/api/printers");
        Assert.Equal(["Microsoft Print to PDF", "POS-80C"], list.Printers);
        Assert.Equal("POS-80C", list.Current);
        Assert.Equal("POS-80C", (await _c.Get<SettingsDto>("/api/settings")).PrinterName);

        // The name is matched without caring about capitals, and saved as Windows spells it.
        var saved = await (await _c.Put("/api/printers/current", new PrinterInput(" pos-80c "))).Read<PrintersDto>();
        Assert.Equal("POS-80C", saved.Current);

        var unknown = await _c.Put("/api/printers/current", new PrinterInput("Old Printer"));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("printerName", await unknown.ErrorFields());
        Assert.Equal("POS-80C", (await _c.Get<PrintersDto>("/api/printers")).Current);

        var none = await (await _c.Put("/api/printers/current", new PrinterInput(""))).Read<PrintersDto>();
        Assert.Equal("", none.Current);
    }

    [Fact]
    public async Task Print_sends_the_bill_to_the_printer()
    {
        var bill = await _c.OpenDineIn("Direct-1");
        await _c.AddLine(bill.Id, f.Tikka);

        var first = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.Equal("POS-80C", first.PrintedTo);
        Assert.Null(first.PrinterError);
        var job = Assert.Single(f.Printer.Jobs);
        Assert.Equal("POS-80C", job.Printer);
        Assert.Equal(Convert.FromBase64String(first.EscPosBase64), job.Data);
        Assert.Equal($"Bill {first.Bill.BillNo}", job.JobName);
        Assert.Equal(EscPos.FeedAndCut, job.Data[^EscPos.FeedAndCut.Length..]);

        // The preview never prints.
        await _c.Get<PrintOutput>($"/api/bills/{bill.Id}/print-preview");
        Assert.Single(f.Printer.Jobs);
    }

    [Fact]
    public async Task Without_a_printer_nothing_is_sent()
    {
        await _c.Put("/api/printers/current", new PrinterInput(""));
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);

        var output = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.Null(output.PrintedTo);
        Assert.Null(output.PrinterError);
        Assert.NotNull(output.Bill.BillNo);
        Assert.Empty(f.Printer.Jobs);
    }

    [Fact]
    public async Task Printer_failure_still_numbers_the_bill_and_it_can_be_sent_again()
    {
        var bill = await _c.OpenDineIn("Direct-2");
        await _c.AddLine(bill.Id, f.Tikka);

        f.Printer.Failure = "The printer is out of paper.";
        var failed = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.Equal("The printer is out of paper.", failed.PrinterError);
        Assert.Null(failed.PrintedTo);
        Assert.NotNull(failed.Bill.BillNo);
        Assert.Equal(1, failed.Bill.PrintCount);

        // Sending again does not count as another print, so the customer's copy is not marked DUPLICATE.
        f.Printer.Failure = null;
        var resent = await (await _c.PostAsync($"/api/bills/{bill.Id}/print/resend", null)).Read<PrintOutput>();
        Assert.Equal("POS-80C", resent.PrintedTo);
        Assert.False(resent.Duplicate);
        Assert.DoesNotContain("DUPLICATE", resent.Text);
        Assert.Equal(1, resent.Bill.PrintCount);
        Assert.Single(f.Printer.Jobs);

        // After a real reprint, sending again repeats the DUPLICATE copy.
        await _c.PostAsync($"/api/bills/{bill.Id}/print", null);
        var resentCopy = await (await _c.PostAsync($"/api/bills/{bill.Id}/print/resend", null)).Read<PrintOutput>();
        Assert.True(resentCopy.Duplicate);
        Assert.Equal(2, resentCopy.Bill.PrintCount);
    }

    [Fact]
    public async Task Resend_needs_a_printed_bill_and_a_printer()
    {
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        Assert.Equal(HttpStatusCode.Conflict, (await _c.PostAsync($"/api/bills/{bill.Id}/print/resend", null)).StatusCode);

        await _c.PostAsync($"/api/bills/{bill.Id}/print", null);
        await _c.Put("/api/printers/current", new PrinterInput(""));
        Assert.Equal(HttpStatusCode.Conflict, (await _c.PostAsync($"/api/bills/{bill.Id}/print/resend", null)).StatusCode);
    }

    [Fact]
    public async Task Test_print_goes_to_the_given_or_chosen_printer()
    {
        Assert.Equal(HttpStatusCode.OK, (await _c.Post("/api/printers/test", new PrinterInput(null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _c.Post("/api/printers/test", new PrinterInput("Microsoft Print to PDF"))).StatusCode);
        Assert.Equal(["POS-80C", "Microsoft Print to PDF"], f.Printer.Jobs.Select(j => j.Printer));
        var text = System.Text.Encoding.ASCII.GetString(f.Printer.Jobs[0].Data);
        Assert.Contains("PRINTER TEST", text);
        Assert.Contains("123456789012345678901234567890123456789012345678", text);

        f.Printer.Failure = "Printer is offline.";
        var failed = await _c.Post("/api/printers/test", new PrinterInput(null));
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Contains("Printer is offline.", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Test_slip_fits_the_paper()
    {
        var doc = PrintEndpoints.TestSlip(new RestaurantSettings(), TestClock.India(2026, 10, 13, 20, 0));
        Assert.All(doc.Lines, l => Assert.True(l.Text.Length <= doc.WidthFor(l.Size), l.Text));
        Assert.All(doc.Lines, l => Assert.True(EscPos.IsPrintable(l.Text), l.Text));
    }
}
