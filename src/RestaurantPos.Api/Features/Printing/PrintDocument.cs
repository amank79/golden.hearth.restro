namespace RestaurantPos.Api.Features.Printing;

public enum PrintAlign { Left, Center, Right }

/// <summary>Normal text, or double width and height (half as many characters per line).</summary>
public enum PrintSize { Normal, Double }

public record PrintLine(string Text, PrintAlign Align = PrintAlign.Left, bool Bold = false, PrintSize Size = PrintSize.Normal);

/// <summary>A receipt as lines with simple styling, rendered as plain text or as ESC/POS printer commands.</summary>
public class PrintDocument(int width)
{
    public int Width { get; } = width;
    public List<PrintLine> Lines { get; } = [];

    public int WidthFor(PrintSize size) => size == PrintSize.Double ? Width / 2 : Width;

    /// <summary>Adds text, wrapping it at word boundaries to the line width.</summary>
    public void Add(string text, PrintAlign align = PrintAlign.Left, bool bold = false, PrintSize size = PrintSize.Normal)
    {
        foreach (var part in Wrap(text, WidthFor(size))) Lines.Add(new PrintLine(part, align, bold, size));
    }

    public void Center(string text, bool bold = false, PrintSize size = PrintSize.Normal) => Add(text, PrintAlign.Center, bold, size);

    /// <summary>Left text and right text on one line (right text wins if both do not fit).</summary>
    public void LeftRight(string left, string right, bool bold = false)
    {
        var room = Width - right.Length - 1;
        if (left.Length > room)
        {
            // Put the left text on its own line(s), the right text on the last one.
            var parts = Wrap(left, room).ToList();
            foreach (var p in parts.Take(parts.Count - 1)) Lines.Add(new PrintLine(p, Bold: bold));
            left = parts[^1];
        }
        Lines.Add(new PrintLine(left.PadRight(Width - right.Length) + right, Bold: bold));
    }

    public void Rule(char c = '-') => Lines.Add(new PrintLine(new string(c, Width)));

    public void Blank() => Lines.Add(new PrintLine(""));

    /// <summary>Word wrap; words longer than the width are cut.</summary>
    public static IEnumerable<string> Wrap(string text, int width)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var w = word;
                while (w.Length > width)
                {
                    if (line.Length > 0) { yield return line; line = ""; }
                    yield return w[..width];
                    w = w[width..];
                }
                if (line.Length == 0) line = w;
                else if (line.Length + 1 + w.Length <= width) line += " " + w;
                else { yield return line; line = w; }
            }
            yield return line;
        }
    }

    /// <summary>The receipt as plain text, each line at most <see cref="Width"/> characters.</summary>
    public string ToText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var l in Lines)
        {
            var text = l.Text;
            var pad = Width - text.Length;
            text = l.Align switch
            {
                PrintAlign.Center when pad > 0 => new string(' ', pad / 2) + text,
                PrintAlign.Right when pad > 0 => new string(' ', pad) + text,
                _ => text,
            };
            sb.Append(text.TrimEnd()).Append('\n');
        }
        return sb.ToString();
    }
}
