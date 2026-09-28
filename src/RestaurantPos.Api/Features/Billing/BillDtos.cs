using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Billing;

public record BillLineDto(
    int Id, int MenuItemId, int ItemVariantId, string ItemName, string VariantName,
    long UnitPricePaise, int Qty, string? Note, int GstRateBp, long AmountPaise);

public record TaxGroupDto(int GstRateBp, int HalfRateBp, long TaxablePaise, long CgstPaise, long SgstPaise);

public record PaymentDto(PaymentMethod Method, long AmountPaise, DateTimeOffset At);

public record BillDto(
    int Id,
    Guid PublicId,
    string? BillNo,
    string? FinancialYear,
    OrderType OrderType,
    string? TableLabel,
    BillStatus Status,
    bool IsFinalised,
    DateTimeOffset OpenedAt,
    DateTimeOffset? FinalisedAt,
    DateTimeOffset? SettledAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    TaxMode TaxMode,
    string DocumentTitle,
    IReadOnlyList<BillLineDto> Lines,
    DiscountKind DiscountKind,
    long DiscountValue,
    string? DiscountReason,
    long SubtotalPaise,
    long DiscountPaise,
    long TaxablePaise,
    IReadOnlyList<TaxGroupDto> TaxGroups,
    long CgstPaise,
    long SgstPaise,
    long RoundOffPaise,
    long TotalPaise,
    IReadOnlyList<PaymentDto> Payments,
    long PaidPaise,
    long BalancePaise,
    int PrintCount);

public record BillSummaryDto(
    int Id,
    string? BillNo,
    OrderType OrderType,
    string? TableLabel,
    BillStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? FinalisedAt,
    DateTimeOffset? SettledAt,
    int ItemCount,
    long TotalPaise,
    IReadOnlyList<PaymentMethod> PaymentMethods,
    string? CancelReason);

public record BillPage(IReadOnlyList<BillSummaryDto> Items, int Total);

public record MethodTotalDto(PaymentMethod Method, long AmountPaise);

/// <summary>Today's total (India date): bills finalised today that are not cancelled.</summary>
public record TodaySummaryDto(
    DateOnly Date,
    int BillCount,
    long TotalPaise,
    long PaidPaise,
    long UnpaidPaise,
    IReadOnlyList<MethodTotalDto> ByMethod,
    int CancelledCount,
    long CancelledPaise,
    int OpenCount);

public record OpenBillInput(OrderType OrderType, string? TableLabel);

public record AddLineInput(int ItemVariantId, int Qty, string? Note);

public record ChangeLineInput(int Qty, string? Note);

public record DiscountInput(DiscountKind Kind, long Value, string? Reason);

public record PaymentInput(PaymentMethod Method, long AmountPaise);

public record SettleInput(IReadOnlyList<PaymentInput> Payments);

public record CancelInput(string Reason, bool CopyToNewBill = false);

public record CancelResult(BillDto Cancelled, BillDto? NewBill);
