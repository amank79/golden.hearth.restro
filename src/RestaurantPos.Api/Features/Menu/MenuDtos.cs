using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Menu;

public record CategoryDto(int Id, string Name, int SortOrder, bool IsActive)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, c.SortOrder, c.IsActive);
}

public record VariantDto(int Id, string Name, long PricePaise, int SortOrder);

public record MenuItemDto(
    int Id,
    int CategoryId,
    string Name,
    string? ShortCode,
    string? Description,
    FoodType FoodType,
    int? GstRateBp,
    bool IsAvailable,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<VariantDto> Variants)
{
    /// <summary>Only active variants are shown; removed ones are kept in the database for old bills.</summary>
    public static MenuItemDto From(MenuItem i) => new(
        i.Id, i.CategoryId, i.Name, i.ShortCode, i.Description, i.FoodType, i.GstRateBp, i.IsAvailable, i.IsActive, i.SortOrder,
        i.Variants.Where(v => v.IsActive).OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select(v => new VariantDto(v.Id, v.Name, v.PricePaise, v.SortOrder)).ToList());
}

public record MenuDto(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<MenuItemDto> Items);

public record CategoryInput(string Name, int? SortOrder);

public record VariantInput(int? Id, string? Name, long PricePaise);

public record MenuItemInput(
    int CategoryId,
    string Name,
    string? ShortCode,
    string? Description,
    FoodType FoodType,
    int? GstRateBp,
    bool IsAvailable,
    int? SortOrder,
    IReadOnlyList<VariantInput>? Variants);

public record AvailabilityInput(bool IsAvailable);

public record ActiveInput(bool IsActive);
