namespace RestaurantPos.Api.Features.Printing;

/// <summary>
/// Renders a <see cref="PrintDocument"/> as ESC/POS commands for an 80 mm thermal printer (48 characters per line
/// in the standard font). The bytes are sent raw through the Windows printer driver (done later, on Windows).
/// </summary>
public static class EscPos
{
    public const byte Esc = 0x1B;
    public const byte Gs = 0x1D;
    public const byte Lf = 0x0A;

    public static readonly byte[] Initialize = [Esc, (byte)'@'];

    /// <summary>Feed 4 lines so the text clears the cutter, then a partial cut.</summary>
    public static readonly byte[] FeedAndCut = [Esc, (byte)'d', 4, Gs, (byte)'V', 66, 0];

    public static byte[] Align(PrintAlign a) => [Esc, (byte)'a', (byte)(a == PrintAlign.Center ? 1 : a == PrintAlign.Right ? 2 : 0)];

    public static byte[] Bold(bool on) => [Esc, (byte)'E', (byte)(on ? 1 : 0)];

    /// <summary>GS ! n: 0x11 = double width and height, 0x00 = normal.</summary>
    public static byte[] Size(PrintSize s) => [Gs, (byte)'!', (byte)(s == PrintSize.Double ? 0x11 : 0x00)];

    public static byte[] Render(PrintDocument doc)
    {
        var bytes = new List<byte>(4096);
        bytes.AddRange(Initialize);
        foreach (var line in doc.Lines)
        {
            bytes.AddRange(Align(line.Align));
            bytes.AddRange(Bold(line.Bold));
            bytes.AddRange(Size(line.Size));
            bytes.AddRange(ToPrinterText(line.Text));
            bytes.Add(Lf);
        }
        bytes.AddRange(Align(PrintAlign.Left));
        bytes.AddRange(Bold(false));
        bytes.AddRange(Size(PrintSize.Normal));
        bytes.AddRange(FeedAndCut);
        return [.. bytes];
    }

    /// <summary>
    /// Printers use a single-byte code page, so only plain ASCII is sent; anything else (₹, Hindi letters) becomes '?'.
    /// The bill layout already writes "Rs." instead of ₹.
    /// </summary>
    public static byte[] ToPrinterText(string text)
    {
        var b = new byte[text.Length];
        for (var i = 0; i < text.Length; i++) b[i] = text[i] is >= ' ' and <= '~' ? (byte)text[i] : (byte)'?';
        return b;
    }

    /// <summary>Whether the text prints as-is (only plain ASCII).</summary>
    public static bool IsPrintable(string text) => text.All(c => c is >= ' ' and <= '~');
}
