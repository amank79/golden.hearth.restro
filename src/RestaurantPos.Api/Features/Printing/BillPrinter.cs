using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RestaurantPos.Api.Features.Printing;

/// <summary>A printer could not take the bill. The message can be shown to staff as-is.</summary>
public class PrinterException(string message) : Exception(message);

/// <summary>Sends ESC/POS bytes straight to a printer installed in Windows.</summary>
public interface IBillPrinter
{
    /// <summary>Names of the printers installed on this laptop.</summary>
    IReadOnlyList<string> InstalledPrinters();

    /// <summary>Sends the bytes as one print job. Throws <see cref="PrinterException"/> if Windows refuses it.</summary>
    void Send(string printerName, byte[] data, string jobName);
}

/// <summary>
/// Raw printing through the Windows print spooler (winspool): the job goes to the printer's driver with the RAW data
/// type, so the printer gets the ESC/POS commands unchanged and prints in its own font, then cuts the paper.
/// The app runs as the LocalSystem service, which sees printers installed for the whole laptop (USB printers are).
/// </summary>
public sealed class WindowsBillPrinter : IBillPrinter
{
    public IReadOnlyList<string> InstalledPrinters()
    {
        if (!OperatingSystem.IsWindows()) return [];
        const int Flags = PrinterEnumLocal | PrinterEnumConnections;
        EnumPrinters(Flags, null, 4, IntPtr.Zero, 0, out var needed, out _);
        if (needed == 0) return [];
        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!EnumPrinters(Flags, null, 4, buffer, needed, out _, out var count)) return [];
            var names = new List<string>(count);
            var size = Marshal.SizeOf<PrinterInfo4>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo4>(buffer + i * size);
                if (!string.IsNullOrEmpty(info.PrinterName)) names.Add(info.PrinterName);
            }
            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Send(string printerName, byte[] data, string jobName)
    {
        if (!OperatingSystem.IsWindows()) throw new PrinterException("Direct printing works only on Windows.");
        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            throw new PrinterException($"Printer \"{printerName}\" was not found. Check it in Settings > Bill printer. ({Reason()})");
        }
        try
        {
            var doc = new DocInfo1 { DocName = jobName, OutputFile = null, DataType = "RAW" };
            if (StartDocPrinter(handle, 1, ref doc) == 0) throw new PrinterException($"The printer did not accept the bill. ({Reason()})");
            try
            {
                if (!StartPagePrinter(handle)) throw new PrinterException($"The printer did not accept the bill. ({Reason()})");
                var unmanaged = Marshal.AllocHGlobal(data.Length);
                try
                {
                    Marshal.Copy(data, 0, unmanaged, data.Length);
                    if (!WritePrinter(handle, unmanaged, data.Length, out var written) || written != data.Length)
                    {
                        throw new PrinterException($"The bill was not sent to the printer completely. ({Reason()})");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(unmanaged);
                    EndPagePrinter(handle);
                }
            }
            finally
            {
                EndDocPrinter(handle);
            }
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    private static string Reason() => new Win32Exception(Marshal.GetLastWin32Error()).Message;

    private const int PrinterEnumLocal = 0x2, PrinterEnumConnections = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        public string DocName;
        public string? OutputFile;
        public string DataType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        public string PrinterName;
        public string ServerName;
        public int Attributes;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr buffer, int bufferSize, out int needed, out int returned);

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string printerName, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int StartDocPrinter(IntPtr handle, int level, ref DocInfo1 docInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, IntPtr data, int count, out int written);
}
