using System.Net;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Customers;
using RestaurantPos.Api.Features.Printing;

namespace RestaurantPos.Tests;

public static class CustomerApi
{
    public static async Task<CustomerDto> AddCustomer(this HttpClient c, string name, string phone, int discountBp = 0) =>
        await (await c.Post("/api/customers", new CustomerInput(name, phone, discountBp))).Read<CustomerDto>();

    public static async Task<BillDto> SetCustomer(this HttpClient c, int billId, int? customerId) =>
        await (await c.Put($"/api/bills/{billId}/customer", new BillCustomerInput(customerId))).Read<BillDto>();
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("9825012345", "9825012345")]
    [InlineData("98250 12345", "9825012345")]
    [InlineData("+91 98250-12345", "9825012345")]
    [InlineData("919825012345", "9825012345")]
    [InlineData("09825012345", "9825012345")]
    [InlineData("6000000000", "6000000000")]
    [InlineData("5825012345", null)] // Indian mobiles start with 6-9
    [InlineData("982501234", null)]
    [InlineData("98250123456", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalises_indian_mobile_numbers(string? input, string? expected) =>
        Assert.Equal(expected, CustomerEndpoints.NormalisePhone(input));
}

public class CustomerTests(BillFixture f) : IClassFixture<BillFixture>, IAsyncLifetime
{
    private readonly HttpClient _c = f.CreateClient();

    public Task InitializeAsync() => f.EnsureMenu();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Customers_are_saved_with_a_clean_phone_number_and_found_by_phone_or_name()
    {
        var ramesh = await _c.AddCustomer(" Ramesh Patel ", "+91 98250 11111", 1000);
        Assert.Equal("Ramesh Patel", ramesh.Name);
        Assert.Equal("9825011111", ramesh.Phone);
        Assert.Equal(1000, ramesh.DiscountBp);
        Assert.Equal(0, ramesh.Visits);
        await _c.AddCustomer("Sita Shah", "9825022222");

        Assert.Equal([ramesh.Id], (await _c.Get<List<CustomerDto>>("/api/customers?q=11111")).Select(c => c.Id));
        Assert.Equal([ramesh.Id], (await _c.Get<List<CustomerDto>>("/api/customers?q=%2B91%2098250%2011111")).Select(c => c.Id));
        Assert.Equal([ramesh.Id], (await _c.Get<List<CustomerDto>>("/api/customers?q=rames")).Select(c => c.Id));
        Assert.Equal(2, (await _c.Get<List<CustomerDto>>("/api/customers?q=98250")).Count(c => c.Phone.EndsWith("11111") || c.Phone.EndsWith("22222")));
        Assert.Empty(await _c.Get<List<CustomerDto>>("/api/customers?q=nobody"));

        var edited = await (await _c.Put($"/api/customers/{ramesh.Id}", new CustomerInput("Ramesh Bhai", "9825011111", 1500))).Read<CustomerDto>();
        Assert.Equal("Ramesh Bhai", edited.Name);
        Assert.Equal(1500, edited.DiscountBp);
    }

    [Theory]
    [InlineData("", "9825033333", 0, "name")]
    [InlineData("Asha", "12345", 0, "phone")]
    [InlineData("Asha", "9825033333", 10001, "discountBp")]
    [InlineData("Asha", "9825033333", -1, "discountBp")]
    public async Task Rejects_bad_customer_details(string name, string phone, int discountBp, string field)
    {
        var r = await _c.Post("/api/customers", new CustomerInput(name, phone, discountBp));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains(field, await r.ErrorFields());
    }

    [Fact]
    public async Task A_phone_number_belongs_to_one_customer()
    {
        var first = await _c.AddCustomer("Kiran", "9825044444");
        var again = await _c.Post("/api/customers", new CustomerInput("Kiran Bhai", "98250 44444", 0));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already saved for Kiran", await again.ProblemTitle());

        var other = await _c.AddCustomer("Mehul", "9825055555");
        var clash = await _c.Put($"/api/customers/{other.Id}", new CustomerInput("Mehul", "9825044444", 0));
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _c.Put($"/api/customers/{first.Id}", new CustomerInput("Kiran", "9825044444", 500))).StatusCode);
    }

    [Fact]
    public async Task A_regulars_discount_is_put_on_the_bill_and_taken_off_with_them()
    {
        var regular = await _c.AddCustomer("Regular Ravi", "9825066666", 1000);
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka); // ₹280

        bill = await _c.SetCustomer(bill.Id, regular.Id);
        Assert.Equal(regular.Id, bill.CustomerId);
        Assert.Equal("Regular Ravi", bill.CustomerName);
        Assert.Equal(DiscountKind.Percent, bill.DiscountKind);
        Assert.Equal(1000, bill.DiscountValue);
        Assert.Equal(BillService.RegularDiscountReason, bill.DiscountReason);
        Assert.Equal(2800, bill.DiscountPaise);          // 10% of ₹280
        Assert.Equal(26500, bill.TotalPaise);            // 252 + 5% GST 12.60 = 264.60, rounded to 265

        // A customer without a standing discount replaces it: their standing discount goes.
        var plain = await _c.AddCustomer("Walk-in Wasim", "9825077777");
        bill = await _c.SetCustomer(bill.Id, plain.Id);
        Assert.Equal(DiscountKind.None, bill.DiscountKind);
        Assert.Equal(0, bill.DiscountPaise);

        bill = await _c.SetCustomer(bill.Id, regular.Id);
        Assert.Equal(2800, bill.DiscountPaise);
        bill = await _c.SetCustomer(bill.Id, null);
        Assert.Null(bill.CustomerId);
        Assert.Null(bill.CustomerName);
        Assert.Equal(DiscountKind.None, bill.DiscountKind);
        Assert.Equal(29400, bill.TotalPaise);
    }

    [Fact]
    public async Task A_discount_staff_gave_is_kept_when_a_customer_is_added_or_removed()
    {
        var regular = await _c.AddCustomer("Regular Rekha", "9825088888", 1000);
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        await _c.Put($"/api/bills/{bill.Id}/discount", new DiscountInput(DiscountKind.Amount, 5000, "Complaint"));

        bill = await _c.SetCustomer(bill.Id, regular.Id);
        Assert.Equal(DiscountKind.Amount, bill.DiscountKind);
        Assert.Equal("Complaint", bill.DiscountReason);

        bill = await _c.SetCustomer(bill.Id, null);
        Assert.Equal("Complaint", bill.DiscountReason);
        Assert.Equal(5000, bill.DiscountPaise);
    }

    [Fact]
    public async Task Printed_bill_shows_the_name_but_never_the_phone_and_cannot_change_customer()
    {
        var c = await _c.AddCustomer("Printed Pooja", "9825099999", 500);
        var bill = await _c.OpenDineIn("Cust-1");
        await _c.AddLine(bill.Id, f.Tikka);
        await _c.SetCustomer(bill.Id, c.Id);

        var printed = await (await _c.PostAsync($"/api/bills/{bill.Id}/print", null)).Read<PrintOutput>();
        Assert.Contains("Customer: Printed Pooja", printed.Text);
        Assert.Contains("Discount (5%)", printed.Text);
        var escPos = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(printed.EscPosBase64));
        Assert.Contains("Customer: Printed Pooja", escPos);
        Assert.DoesNotContain("9825099999", printed.Text + escPos);
        Assert.DoesNotContain("99999", printed.Text + escPos);

        var change = await _c.Put($"/api/bills/{bill.Id}/customer", new BillCustomerInput(null));
        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);

        // Renaming the customer later does not change the issued bill.
        await _c.Put($"/api/customers/{c.Id}", new CustomerInput("Pooja Ben", "9825099999", 500));
        Assert.Equal("Printed Pooja", (await _c.Get<BillDto>($"/api/bills/{bill.Id}")).CustomerName);
    }

    [Fact]
    public async Task Visits_and_spending_count_printed_or_paid_bills_that_are_not_cancelled()
    {
        var c = await _c.AddCustomer("Counted Chetan", "9876500001");

        var paid = await _c.OpenTakeaway();
        paid = await _c.AddLine(paid.Id, f.Tikka);
        await _c.SetCustomer(paid.Id, c.Id);
        await _c.Pay(paid.Id, new PaymentInput(PaymentMethod.Cash, 29400));

        var open = await _c.OpenTakeaway(); // not printed yet: not a visit
        await _c.AddLine(open.Id, f.Tikka);
        await _c.SetCustomer(open.Id, c.Id);

        var cancelled = await _c.OpenTakeaway();
        await _c.AddLine(cancelled.Id, f.Tikka);
        await _c.SetCustomer(cancelled.Id, c.Id);
        await _c.Finalise(cancelled.Id);
        await _c.Post($"/api/bills/{cancelled.Id}/cancel", new CancelInput("test"));

        var found = Assert.Single(await _c.Get<List<CustomerDto>>("/api/customers?q=9876500001"));
        Assert.Equal(1, found.Visits);
        Assert.Equal(29400, found.SpentPaise);
        Assert.NotNull(found.LastVisitAt);

        // Bill history finds the customer's bills by name or by phone.
        var byName = await _c.Get<BillPage>("/api/bills?q=Counted");
        Assert.Contains(byName.Items, b => b.Id == paid.Id && b.CustomerName == "Counted Chetan");
        var byPhone = await _c.Get<BillPage>("/api/bills?q=98765000");
        Assert.Equal(3, byPhone.Items.Count(b => b.Id == paid.Id || b.Id == open.Id || b.Id == cancelled.Id));
    }

    [Fact]
    public async Task Copying_a_cancelled_bill_keeps_the_customer_and_their_discount()
    {
        var c = await _c.AddCustomer("Copied Kavya", "9876500002", 1000);
        var bill = await _c.OpenTakeaway();
        await _c.AddLine(bill.Id, f.Tikka);
        await _c.SetCustomer(bill.Id, c.Id);
        await _c.Finalise(bill.Id);

        var result = await (await _c.Post($"/api/bills/{bill.Id}/cancel", new CancelInput("Wrong dish", CopyToNewBill: true))).Read<CancelResult>();
        Assert.Equal(c.Id, result.NewBill!.CustomerId);
        Assert.Equal("Copied Kavya", result.NewBill.CustomerName);
        Assert.Equal(2800, result.NewBill.DiscountPaise);
    }

    [Fact]
    public async Task Unknown_customer_or_bill_is_not_found()
    {
        var bill = await _c.OpenTakeaway();
        Assert.Equal(HttpStatusCode.NotFound, (await _c.Put($"/api/bills/{bill.Id}/customer", new BillCustomerInput(99999))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _c.Put("/api/customers/99999", new CustomerInput("X", "9825012345", 0))).StatusCode);
    }

    [Fact]
    public async Task Phone_numbers_are_not_written_to_the_log()
    {
        var c = await _c.AddCustomer("Logged Lata", "9876500003");
        await _c.GetAsync("/api/customers?q=9876500003");
        await _c.Put($"/api/customers/{c.Id}", new CustomerInput("Logged Lata", "9876500003", 0));
        await _c.GetAsync("/api/bills?q=9876500003");

        var text = "";
        foreach (var file in Directory.GetFiles(Path.Combine(f.DataRoot, "logs"), "app-*.log"))
        {
            using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            text += await reader.ReadToEndAsync();
        }
        Assert.Contains("GET /api/customers responded 200", text);
        Assert.DoesNotContain("9876500003", text);
    }
}
