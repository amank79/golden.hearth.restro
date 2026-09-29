using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Billing;

namespace RestaurantPos.Api.Features.Settings;

/// <summary>Restaurant details and tax settings printed on every bill (SET-1, SET-2).</summary>
public record SettingsDto(
    string Name,
    string Address,
    string Phone,
    string Gstin,
    string FssaiNo,
    TaxMode TaxMode,
    int GstRateBp,
    string BillFooter)
{
    public static SettingsDto From(RestaurantSettings s) =>
        new(s.Name, s.Address, s.Phone, s.Gstin, s.FssaiNo, s.TaxMode, s.GstRateBp, s.BillFooter);
}

public static partial class SettingsEndpoints
{
    /// <summary>Highest GST rate allowed (28%).</summary>
    public const int MaxGstRateBp = 2800;

    // 2-digit state code, 10-character PAN, entity number, 'Z', check character.
    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex GstinPattern();

    [GeneratedRegex("^[0-9]{14}$")]
    private static partial Regex FssaiPattern();

    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/settings");

        g.MapGet("", async (PosDbContext db) => SettingsDto.From(await db.GetSettingsAsync()));

        g.MapPut("", async (SettingsDto dto, PosDbContext db) =>
        {
            var gstin = (dto.Gstin ?? "").Trim().ToUpperInvariant();
            var fssai = (dto.FssaiNo ?? "").Trim();

            var v = new Validation()
                .Required(dto.Name, "name", "Restaurant name", 60)
                .MaxLength(dto.Address, "address", "Address", 200)
                .MaxLength(dto.Phone, "phone", "Phone", 40)
                .Check(gstin.Length == 0 || GstinPattern().IsMatch(gstin), "gstin", "GSTIN must be 15 characters, like 24ABCDE1234F1Z5.")
                .Check(fssai.Length == 0 || FssaiPattern().IsMatch(fssai), "fssaiNo", "FSSAI licence number must be 14 digits.")
                .Check(Enum.IsDefined(dto.TaxMode), "taxMode", "Tax mode must be Regular or Composition.")
                .Check(dto.GstRateBp is >= 0 and <= MaxGstRateBp, "gstRateBp", "GST rate must be between 0% and 28%.")
                .Check(dto.GstRateBp % 2 == 0, "gstRateBp", "GST rate must split equally into CGST and SGST (for example 5, 12 or 18).")
                .MaxLength(dto.BillFooter, "billFooter", "Bill footer", 300);
            if (!v.IsValid) return v.Problem();

            var s = await db.GetSettingsAsync();
            s.Name = dto.Name.Trim();
            s.Address = (dto.Address ?? "").Trim();
            s.Phone = (dto.Phone ?? "").Trim();
            s.Gstin = gstin;
            s.FssaiNo = fssai;
            s.TaxMode = dto.TaxMode;
            s.GstRateBp = dto.GstRateBp;
            s.BillFooter = (dto.BillFooter ?? "").Trim();
            await db.SaveChangesAsync();
            await BillService.RecalculateOpenBillsAsync(db, s);
            return Results.Ok(SettingsDto.From(s));
        });

        return api;
    }

    /// <summary>The single settings row (created by the first migration).</summary>
    public static Task<RestaurantSettings> GetSettingsAsync(this PosDbContext db) =>
        db.Settings.SingleAsync(s => s.Id == 1);
}
