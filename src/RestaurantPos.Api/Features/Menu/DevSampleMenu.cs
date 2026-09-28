using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Menu;

/// <summary>
/// Sample dishes from demo/restaurant-demo.html, loaded only in Development when the menu is empty, so the screens
/// have something to show. Never used on the restaurant laptop (Production) and never put in a migration:
/// the family's real menu is entered through the Menu screen.
/// </summary>
public static class DevSampleMenu
{
    private record Dish(string Name, string Code, FoodType Type, long Rupees, (string Name, long Rupees)[]? Sizes = null, bool Available = true);

    private static readonly (string Category, Dish[] Dishes)[] Menu =
    [
        ("Starters",
        [
            new("Paneer Tikka", "PT", FoodType.Veg, 280),
            new("Chicken Tikka", "CT", FoodType.NonVeg, 320),
            new("Veg Spring Roll", "VSR", FoodType.Veg, 180),
            new("Chilli Paneer", "CP", FoodType.Veg, 260, [("Half", 160), ("Full", 260)]),
            new("Crispy Corn", "CC", FoodType.Veg, 200),
            new("Chicken 65", "C65", FoodType.NonVeg, 300, [("Half", 180), ("Full", 300)]),
        ]),
        ("Main Course",
        [
            new("Butter Chicken", "BC", FoodType.NonVeg, 360, [("Half", 220), ("Full", 360)]),
            new("Paneer Butter Masala", "PBM", FoodType.Veg, 290, [("Half", 180), ("Full", 290)]),
            new("Dal Makhani", "DM", FoodType.Veg, 240, [("Half", 150), ("Full", 240)]),
            new("Kadai Paneer", "KP", FoodType.Veg, 280),
            new("Chicken Curry", "CHC", FoodType.NonVeg, 330, [("Half", 200), ("Full", 330)]),
            new("Mix Veg", "MV", FoodType.Veg, 220),
        ]),
        ("Breads",
        [
            new("Tandoori Roti", "TR", FoodType.Veg, 30),
            new("Butter Naan", "BN", FoodType.Veg, 60),
            new("Garlic Naan", "GN", FoodType.Veg, 80),
            new("Lachha Paratha", "LP", FoodType.Veg, 60),
        ]),
        ("Rice & Biryani",
        [
            new("Steamed Rice", "SR", FoodType.Veg, 120),
            new("Jeera Rice", "JR", FoodType.Veg, 160),
            new("Veg Biryani", "VB", FoodType.Veg, 240),
            new("Chicken Biryani", "CB", FoodType.NonVeg, 320, [("Half", 200), ("Full", 320)]),
        ]),
        ("Drinks",
        [
            new("Masala Chaas", "MC", FoodType.Veg, 60),
            new("Sweet Lassi", "SL", FoodType.Veg, 90),
            new("Fresh Lime Soda", "FLS", FoodType.Veg, 80),
            new("Cold Coffee", "CCF", FoodType.Veg, 120),
            new("Soft Drink", "SD", FoodType.Veg, 40),
        ]),
        ("Desserts",
        [
            new("Gulab Jamun", "GJ", FoodType.Veg, 80),
            new("Rasmalai", "RM", FoodType.Veg, 110, Available: false),
            new("Ice Cream", "IC", FoodType.Veg, 90),
        ]),
    ];

    public static async Task SeedIfEmptyAsync(PosDbContext db)
    {
        if (await db.Categories.AnyAsync()) return;

        var catSort = 0;
        foreach (var (categoryName, dishes) in Menu)
        {
            var category = new Category { Name = categoryName, SortOrder = ++catSort };
            var itemSort = 0;
            foreach (var d in dishes)
            {
                var item = new MenuItem
                {
                    Name = d.Name,
                    ShortCode = d.Code,
                    FoodType = d.Type,
                    IsAvailable = d.Available,
                    SortOrder = ++itemSort,
                };
                var sizes = d.Sizes ?? [("Regular", d.Rupees)];
                for (var i = 0; i < sizes.Length; i++)
                {
                    item.Variants.Add(new ItemVariant { Name = sizes[i].Name, PricePaise = sizes[i].Rupees * 100, SortOrder = i });
                }
                category.Items.Add(item);
            }
            db.Categories.Add(category);
        }
        await db.SaveChangesAsync();
    }
}
