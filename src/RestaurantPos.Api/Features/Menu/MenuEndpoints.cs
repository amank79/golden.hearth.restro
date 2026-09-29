using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Api.Features.Menu;

/// <summary>
/// Menu management (MENU-1, 2, 3, 5, 6, 7, 8). Nothing is ever deleted: categories, dishes and variants are
/// deactivated instead, because old bills point to them. Price changes only affect new bill lines, since bill
/// lines copy the name and price when they are added (MENU-7).
/// </summary>
public static partial class MenuEndpoints
{
    public const long MaxPricePaise = 10_000_000; // ₹1,00,000

    [GeneratedRegex("^[A-Z0-9]{1,10}$")]
    private static partial Regex ShortCodePattern();

    public static RouteGroupBuilder MapMenuEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/menu");

        // Whole menu in display order. Billing uses the default (active only); the menu screen asks for everything.
        g.MapGet("", async (PosDbContext db, bool includeInactive = false) =>
        {
            var categories = await db.Categories
                .Where(c => includeInactive || c.IsActive)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .ToListAsync();
            var items = await LoadItems(db, includeInactive);
            return new MenuDto(categories.Select(CategoryDto.From).ToList(), items.Select(MenuItemDto.From).ToList());
        });

        // Search by name or short code, best match first (MENU-6).
        g.MapGet("/items", async (PosDbContext db, string? q, int? categoryId, bool includeInactive = false) =>
        {
            var items = await LoadItems(db, includeInactive);
            return items
                .Where(i => categoryId is null || i.CategoryId == categoryId)
                .Select(i => (Item: i, Rank: MenuSearch.Rank(i, q ?? "")))
                .Where(x => x.Rank is not null)
                .OrderBy(x => x.Rank)
                .Select(x => MenuItemDto.From(x.Item))
                .ToList();
        });

        g.MapPost("/categories", async (CategoryInput input, PosDbContext db) =>
        {
            var v = await ValidateCategory(input, db, null);
            if (!v.IsValid) return v.Problem();

            var sort = input.SortOrder ?? (await db.Categories.MaxAsync(c => (int?)c.SortOrder) ?? 0) + 1;
            var category = new Category { Name = input.Name.Trim(), SortOrder = sort };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/menu/categories/{category.Id}", CategoryDto.From(category));
        });

        g.MapPut("/categories/{id:int}", async (int id, CategoryInput input, PosDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return ApiErrors.NotFound("Category");
            var v = await ValidateCategory(input, db, id);
            if (!v.IsValid) return v.Problem();

            category.Name = input.Name.Trim();
            if (input.SortOrder is { } sort) category.SortOrder = sort;
            await db.SaveChangesAsync();
            return Results.Ok(CategoryDto.From(category));
        });

        // Deactivate or bring back a category. Its dishes are hidden from billing while it is inactive.
        g.MapPut("/categories/{id:int}/active", async (int id, ActiveInput input, PosDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return ApiErrors.NotFound("Category");
            if (input.IsActive && await db.Categories.AnyAsync(c => c.Id != id && c.IsActive && c.Name.ToLower() == category.Name.ToLower()))
            {
                return ApiErrors.Conflict($"Another active category is already called \"{category.Name}\".");
            }
            category.IsActive = input.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(CategoryDto.From(category));
        });

        g.MapPost("/items", async (MenuItemInput input, PosDbContext db) =>
        {
            var v = await ValidateItem(input, db, null);
            if (!v.IsValid) return v.Problem();

            var sort = input.SortOrder
                ?? (await db.MenuItems.Where(i => i.CategoryId == input.CategoryId).MaxAsync(i => (int?)i.SortOrder) ?? 0) + 1;
            var item = new MenuItem { SortOrder = sort };
            Apply(item, input);
            var n = 0;
            foreach (var vi in input.Variants!)
            {
                item.Variants.Add(new ItemVariant { Name = VariantName(vi, input.Variants!.Count), PricePaise = vi.PricePaise, SortOrder = n++ });
            }
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/menu/items/{item.Id}", MenuItemDto.From(item));
        });

        g.MapGet("/items/{id:int}", async (int id, PosDbContext db) =>
        {
            var item = await db.MenuItems.Include(i => i.Variants).SingleOrDefaultAsync(i => i.Id == id);
            return item is null ? ApiErrors.NotFound("Dish") : Results.Ok(MenuItemDto.From(item));
        });

        // Full edit of a dish, including its variants. Variants left out of the list are deactivated, never deleted.
        g.MapPut("/items/{id:int}", async (int id, MenuItemInput input, PosDbContext db) =>
        {
            var item = await db.MenuItems.Include(i => i.Variants).SingleOrDefaultAsync(i => i.Id == id);
            if (item is null) return ApiErrors.NotFound("Dish");
            var v = await ValidateItem(input, db, item);
            if (!v.IsValid) return v.Problem();

            Apply(item, input);
            if (input.SortOrder is { } sort) item.SortOrder = sort;

            var keep = input.Variants!.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToHashSet();
            foreach (var old in item.Variants.Where(x => x.IsActive && !keep.Contains(x.Id))) old.IsActive = false;

            var n = 0;
            foreach (var vi in input.Variants!)
            {
                var name = VariantName(vi, input.Variants!.Count);
                if (vi.Id is { } variantId)
                {
                    var variant = item.Variants.Single(x => x.Id == variantId);
                    variant.Name = name;
                    variant.PricePaise = vi.PricePaise;
                    variant.SortOrder = n++;
                    variant.IsActive = true;
                }
                else
                {
                    item.Variants.Add(new ItemVariant { Name = name, PricePaise = vi.PricePaise, SortOrder = n++ });
                }
            }
            await db.SaveChangesAsync();
            return Results.Ok(MenuItemDto.From(item));
        });

        // "Not available today" switch (MENU-5).
        g.MapPut("/items/{id:int}/availability", async (int id, AvailabilityInput input, PosDbContext db) =>
        {
            var item = await db.MenuItems.Include(i => i.Variants).SingleOrDefaultAsync(i => i.Id == id);
            if (item is null) return ApiErrors.NotFound("Dish");
            item.IsAvailable = input.IsAvailable;
            await db.SaveChangesAsync();
            return Results.Ok(MenuItemDto.From(item));
        });

        // Remove a dish from the menu (or bring it back). The row stays for old bills.
        g.MapPut("/items/{id:int}/active", async (int id, ActiveInput input, PosDbContext db) =>
        {
            var item = await db.MenuItems.Include(i => i.Variants).SingleOrDefaultAsync(i => i.Id == id);
            if (item is null) return ApiErrors.NotFound("Dish");
            if (input.IsActive && item.ShortCode is { } code
                && await db.MenuItems.AnyAsync(i => i.Id != id && i.IsActive && i.ShortCode == code))
            {
                return ApiErrors.Conflict($"Short code {code} is now used by another dish. Change it before bringing this dish back.");
            }
            item.IsActive = input.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(MenuItemDto.From(item));
        });

        return api;
    }

    private static Task<List<MenuItem>> LoadItems(PosDbContext db, bool includeInactive) =>
        db.MenuItems
            .Include(i => i.Variants)
            .Include(i => i.Category)
            .Where(i => includeInactive || (i.IsActive && i.Category!.IsActive))
            .OrderBy(i => i.Category!.SortOrder).ThenBy(i => i.Category!.Name).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .AsSplitQuery()
            .ToListAsync();

    private static void Apply(MenuItem item, MenuItemInput input)
    {
        item.CategoryId = input.CategoryId;
        item.Name = input.Name.Trim();
        item.ShortCode = NormaliseCode(input.ShortCode);
        item.Description = ApiErrors.Clean(input.Description);
        item.FoodType = input.FoodType;
        item.GstRateBp = input.GstRateBp;
        item.IsAvailable = input.IsAvailable;
    }

    private static string? NormaliseCode(string? code) => ApiErrors.Clean(code)?.ToUpperInvariant();

    /// <summary>A dish with a single price gets one variant called "Regular" unless a name is given.</summary>
    private static string VariantName(VariantInput v, int count) =>
        ApiErrors.Clean(v.Name) ?? (count == 1 ? "Regular" : "");

    private static async Task<Validation> ValidateCategory(CategoryInput input, PosDbContext db, int? id)
    {
        var v = new Validation().Required(input.Name, "name", "Category name", 60);
        if (v.IsValid)
        {
            var name = input.Name.Trim().ToLower();
            var taken = await db.Categories.AnyAsync(c => c.Id != id && c.IsActive && c.Name.ToLower() == name);
            v.Check(!taken, "name", "There is already a category with this name.");
        }
        return v;
    }

    private static async Task<Validation> ValidateItem(MenuItemInput input, PosDbContext db, MenuItem? existing)
    {
        var v = new Validation()
            .Required(input.Name, "name", "Dish name", 100)
            .MaxLength(input.Description, "description", "Description", 300)
            .Check(Enum.IsDefined(input.FoodType), "foodType", "Choose veg, non-veg or egg.")
            .Check(input.GstRateBp is null or (>= 0 and <= SettingsEndpoints.MaxGstRateBp) && (input.GstRateBp ?? 0) % 2 == 0,
                "gstRateBp", "GST rate must be between 0% and 28% and split equally into CGST and SGST.");

        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId))
        {
            v.Add("categoryId", "Choose a category.");
        }

        var code = NormaliseCode(input.ShortCode);
        if (code is not null)
        {
            if (!ShortCodePattern().IsMatch(code))
            {
                v.Add("shortCode", "Short code can only have letters and digits (up to 10), like PBM.");
            }
            else if (await db.MenuItems.AnyAsync(i => i.Id != (existing == null ? 0 : existing.Id) && i.IsActive && i.ShortCode == code))
            {
                v.Add("shortCode", $"Short code {code} is already used by another dish.");
            }
        }

        var variants = input.Variants ?? [];
        if (variants.Count == 0)
        {
            v.Add("variants", "Give the dish at least one price.");
        }
        else if (variants.Count > 10)
        {
            v.Add("variants", "A dish can have at most 10 sizes.");
        }
        else
        {
            var names = variants.Select(x => VariantName(x, variants.Count)).ToList();
            v.Check(names.All(n => n.Length > 0), "variants", "Give every size a name, like Half and Full.")
             .Check(names.All(n => n.Length <= 30), "variants", "Size names can have at most 30 characters.")
             .Check(names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count, "variants", "Two sizes have the same name.")
             .Check(variants.All(x => x.PricePaise is >= 0 and <= MaxPricePaise), "variants", "Price must be between ₹0 and ₹1,00,000.");

            var ids = variants.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToList();
            var ownIds = existing?.Variants.Select(x => x.Id).ToHashSet() ?? [];
            v.Check(ids.All(ownIds.Contains) && ids.Distinct().Count() == ids.Count, "variants", "A size does not belong to this dish.");
        }
        return v;
    }
}
