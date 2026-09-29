using System.Net;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Menu;

namespace RestaurantPos.Tests;

/// <summary>Helpers to build a menu through the API.</summary>
public static class MenuApi
{
    public static async Task<CategoryDto> AddCategory(this HttpClient c, string name) =>
        await (await c.Post("/api/menu/categories", new CategoryInput(name, null))).Read<CategoryDto>();

    public static MenuItemInput Dish(int categoryId, string name, long rupees, string? code = null, FoodType type = FoodType.Veg) =>
        new(categoryId, name, code, null, type, null, true, null, [new VariantInput(null, null, rupees * 100)]);

    public static MenuItemInput HalfFull(int categoryId, string name, long half, long full, string? code = null) =>
        new(categoryId, name, code, null, FoodType.Veg, null, true, null,
            [new VariantInput(null, "Half", half * 100), new VariantInput(null, "Full", full * 100)]);

    public static async Task<MenuItemDto> AddItem(this HttpClient c, MenuItemInput input) =>
        await (await c.Post("/api/menu/items", input)).Read<MenuItemDto>();
}

public class MenuTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    [Fact]
    public async Task Categories_are_listed_in_display_order()
    {
        var a = await _c.AddCategory("Order Z");
        var b = await _c.AddCategory("Order A");
        await _c.Put($"/api/menu/categories/{a.Id}", new CategoryInput("Order Z", 1000));
        await _c.Put($"/api/menu/categories/{b.Id}", new CategoryInput("Order A", 999));

        var menu = await _c.Get<MenuDto>("/api/menu");
        var names = menu.Categories.Select(x => x.Name).ToList();
        Assert.True(names.IndexOf("Order A") < names.IndexOf("Order Z"));
    }

    [Fact]
    public async Task Category_name_must_be_unique_and_present()
    {
        await _c.AddCategory("Soups");
        Assert.Contains("name", await (await _c.Post("/api/menu/categories", new CategoryInput("soups", null))).ErrorFields());
        Assert.Contains("name", await (await _c.Post("/api/menu/categories", new CategoryInput(" ", null))).ErrorFields());
    }

    [Fact]
    public async Task Dish_with_half_and_full_prices()
    {
        var cat = await _c.AddCategory("Curries HF");
        var item = await _c.AddItem(MenuApi.HalfFull(cat.Id, "Dal Makhani", 150, 240, "dm1"));

        Assert.Equal("DM1", item.ShortCode);
        Assert.Equal(["Half", "Full"], item.Variants.Select(v => v.Name));
        Assert.Equal([15000L, 24000L], item.Variants.Select(v => v.PricePaise));
    }

    [Fact]
    public async Task Single_price_dish_gets_a_Regular_variant()
    {
        var cat = await _c.AddCategory("Breads R");
        var item = await _c.AddItem(MenuApi.Dish(cat.Id, "Butter Naan", 60));
        Assert.Equal("Regular", Assert.Single(item.Variants).Name);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("shortCode")]
    [InlineData("variants")]
    [InlineData("categoryId")]
    [InlineData("gstRateBp")]
    public async Task Invalid_dish_is_rejected(string field)
    {
        var cat = await _c.AddCategory($"Invalid {field}");
        var ok = MenuApi.Dish(cat.Id, "Something", 100);
        var bad = field switch
        {
            "name" => ok with { Name = "" },
            "shortCode" => ok with { ShortCode = "P B M" },
            "variants" => ok with { Variants = [] },
            "categoryId" => ok with { CategoryId = 99999 },
            _ => ok with { GstRateBp = 3000 },
        };
        Assert.Contains(field, await (await _c.Post("/api/menu/items", bad)).ErrorFields());
    }

    [Fact]
    public async Task Negative_price_and_duplicate_size_names_are_rejected()
    {
        var cat = await _c.AddCategory("Bad prices");
        var neg = MenuApi.Dish(cat.Id, "Neg", 0) with { Variants = [new VariantInput(null, null, -100)] };
        Assert.Contains("variants", await (await _c.Post("/api/menu/items", neg)).ErrorFields());
        var dup = MenuApi.HalfFull(cat.Id, "Dup", 1, 2) with { Variants = [new VariantInput(null, "Half", 100), new VariantInput(null, "half", 200)] };
        Assert.Contains("variants", await (await _c.Post("/api/menu/items", dup)).ErrorFields());
    }

    [Fact]
    public async Task Short_code_must_be_unique_among_active_dishes()
    {
        var cat = await _c.AddCategory("Codes");
        var first = await _c.AddItem(MenuApi.Dish(cat.Id, "Paneer Tikka", 280, "UNQ"));
        Assert.Contains("shortCode", await (await _c.Post("/api/menu/items", MenuApi.Dish(cat.Id, "Other", 100, "unq"))).ErrorFields());

        // Once the first dish is removed from the menu, the code can be used again.
        await _c.Put($"/api/menu/items/{first.Id}/active", new ActiveInput(false));
        await _c.AddItem(MenuApi.Dish(cat.Id, "Other", 100, "UNQ"));

        // ...and the old dish cannot come back with the same code.
        Assert.Equal(HttpStatusCode.Conflict, (await _c.Put($"/api/menu/items/{first.Id}/active", new ActiveInput(true))).StatusCode);
    }

    [Fact]
    public async Task Not_available_switch()
    {
        var cat = await _c.AddCategory("Avail");
        var item = await _c.AddItem(MenuApi.Dish(cat.Id, "Rasmalai", 110));
        var off = await (await _c.Put($"/api/menu/items/{item.Id}/availability", new AvailabilityInput(false))).Read<MenuItemDto>();
        Assert.False(off.IsAvailable);

        // Unavailable dishes are still on the menu (shown greyed out on the billing screen).
        var menu = await _c.Get<MenuDto>("/api/menu");
        Assert.False(menu.Items.Single(i => i.Id == item.Id).IsAvailable);
    }

    [Fact]
    public async Task Deactivated_dish_is_hidden_but_kept()
    {
        var cat = await _c.AddCategory("Deact");
        var item = await _c.AddItem(MenuApi.Dish(cat.Id, "Old Dish", 100));
        await _c.Put($"/api/menu/items/{item.Id}/active", new ActiveInput(false));

        Assert.DoesNotContain((await _c.Get<MenuDto>("/api/menu")).Items, i => i.Id == item.Id);
        var all = await _c.Get<MenuDto>("/api/menu?includeInactive=true");
        Assert.False(all.Items.Single(i => i.Id == item.Id).IsActive);
    }

    [Fact]
    public async Task Deactivated_category_hides_its_dishes_from_billing()
    {
        var cat = await _c.AddCategory("Seasonal");
        var item = await _c.AddItem(MenuApi.Dish(cat.Id, "Mango Lassi", 120));
        await _c.Put($"/api/menu/categories/{cat.Id}/active", new ActiveInput(false));

        var menu = await _c.Get<MenuDto>("/api/menu");
        Assert.DoesNotContain(menu.Categories, c => c.Id == cat.Id);
        Assert.DoesNotContain(menu.Items, i => i.Id == item.Id);
        Assert.Contains((await _c.Get<MenuDto>("/api/menu?includeInactive=true")).Categories, c => c.Id == cat.Id && !c.IsActive);
    }

    [Fact]
    public async Task Editing_variants_deactivates_removed_ones_and_keeps_ids()
    {
        var cat = await _c.AddCategory("Edit V");
        var item = await _c.AddItem(MenuApi.HalfFull(cat.Id, "Chilli Paneer", 160, 260));
        var half = item.Variants[0];
        var full = item.Variants[1];

        // Change Full's price, drop Half, add Family.
        var edit = MenuApi.Dish(cat.Id, "Chilli Paneer", 0) with
        {
            Variants = [new VariantInput(full.Id, "Full", 27000), new VariantInput(null, "Family", 45000)],
        };
        var saved = await (await _c.Put($"/api/menu/items/{item.Id}", edit)).Read<MenuItemDto>();

        Assert.Equal(["Full", "Family"], saved.Variants.Select(v => v.Name));
        Assert.Equal(full.Id, saved.Variants[0].Id);
        Assert.Equal(27000, saved.Variants[0].PricePaise);
        Assert.DoesNotContain(saved.Variants, v => v.Id == half.Id);
    }

    [Fact]
    public async Task Variant_of_another_dish_cannot_be_used()
    {
        var cat = await _c.AddCategory("Steal");
        var a = await _c.AddItem(MenuApi.Dish(cat.Id, "A", 10));
        var b = await _c.AddItem(MenuApi.Dish(cat.Id, "B", 20));
        var edit = MenuApi.Dish(cat.Id, "B", 0) with { Variants = [new VariantInput(a.Variants[0].Id, null, 100)] };
        Assert.Contains("variants", await (await _c.Put($"/api/menu/items/{b.Id}", edit)).ErrorFields());
    }

    [Fact]
    public async Task Per_dish_gst_rate()
    {
        var cat = await _c.AddCategory("Tax");
        var item = await _c.AddItem(MenuApi.Dish(cat.Id, "Packaged Water", 20) with { GstRateBp = 1800 });
        Assert.Equal(1800, item.GstRateBp);
    }

    [Fact]
    public async Task Search_by_name_and_short_code()
    {
        var cat = await _c.AddCategory("Search");
        await _c.AddItem(MenuApi.HalfFull(cat.Id, "Paneer Butter Masala", 180, 290, "PBMX"));
        await _c.AddItem(MenuApi.Dish(cat.Id, "Kadai Paneer", 280, "KPX"));
        await _c.AddItem(MenuApi.Dish(cat.Id, "Butter Naan", 60, "BNX"));

        var byCode = await _c.Get<List<MenuItemDto>>($"/api/menu/items?q=pbmx&categoryId={cat.Id}");
        Assert.Equal("Paneer Butter Masala", byCode[0].Name);

        var byName = await _c.Get<List<MenuItemDto>>($"/api/menu/items?q=paneer&categoryId={cat.Id}");
        Assert.Equal(["Paneer Butter Masala", "Kadai Paneer"], byName.Select(i => i.Name));

        var butter = await _c.Get<List<MenuItemDto>>($"/api/menu/items?q=butter&categoryId={cat.Id}");
        Assert.Equal(["Butter Naan", "Paneer Butter Masala"], butter.Select(i => i.Name));
    }

    [Fact]
    public async Task There_is_no_delete()
    {
        var cat = await _c.AddCategory("NoDelete");
        var item = await _c.AddItem(MenuApi.Dish(cat.Id, "Keep", 10));
        var response = await _c.DeleteAsync($"/api/menu/items/{item.Id}");
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(item.Id, (await _c.Get<MenuItemDto>($"/api/menu/items/{item.Id}")).Id);
    }
}

public class MenuSearchTests
{
    [Theory]
    [InlineData("pbm", 0)]      // exact short code
    [InlineData("PB", 1)]       // short code prefix
    [InlineData("paneer b", 2)] // name starts with
    [InlineData("butt", 3)]     // a word starts with
    [InlineData("pb", 1)]
    [InlineData("ter mas", 5)]  // anywhere in the name
    public void Ranks(string q, int expected) =>
        Assert.Equal(expected, MenuSearch.Rank("Paneer Butter Masala", "PBM", q));

    [Fact]
    public void Initials_match_without_a_short_code() =>
        Assert.Equal(4, MenuSearch.Rank("Paneer Butter Masala", null, "pbm"));

    [Fact]
    public void No_match() => Assert.Null(MenuSearch.Rank("Dal Makhani", "DM", "paneer"));

    [Fact]
    public void Empty_query_matches_everything() => Assert.Equal(0, MenuSearch.Rank("Dal Makhani", null, "  "));
}

public class SampleMenuTests(SampleMenuFactory factory) : IClassFixture<SampleMenuFactory>
{
    [Fact]
    public async Task Development_sample_menu_is_loaded_when_empty()
    {
        var menu = await factory.CreateClient().Get<MenuDto>("/api/menu");
        Assert.Equal(6, menu.Categories.Count);
        var pbm = menu.Items.Single(i => i.ShortCode == "PBM");
        Assert.Equal(["Half", "Full"], pbm.Variants.Select(v => v.Name));
        Assert.False(menu.Items.Single(i => i.Name == "Rasmalai").IsAvailable);
    }
}
