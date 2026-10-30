using System.IO;
using LinenLuxe.POS.Models;
using PdfSharp.Pdf;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// The 80 mm till receipt — the slip handed across the counter, as opposed to the
/// A4 invoice that gets emailed or filed.
///
/// Deliberately not a squeezed A4. A receipt roll is 80 mm wide and unbounded in
/// length, so the layout is a single narrow column and the page grows to fit;
/// nothing is ever pushed onto a "second page" that a roll doesn't have.
///
/// Black only. Thermal printers have one colour, and the navy-and-gold that
/// carries the A4 invoice would come out as muddy dithering. The logo is the one
/// exception — it is the brand, and a shop's receipt is expected to carry it.
/// </summary>
public static class ReceiptService
{
    // ----- Paper (millimetres) -----
    private const double PaperW = 80;
    private const double Margin = 4;
    private const double ContentW = PaperW - 2 * Margin;

    private static readonly Rgb Black = new(0, 0, 0);
    private static readonly Rgb Grey = new(90, 90, 90);
    private static readonly Rgb Rule = new(170, 170, 170);

    private const string Body = "Arial";

    private const string BizName = "LinenLuxe Laundry";
    private const string BizTagline = "Luxury Care for Every Fabric";
    private const string BizPhone = "+971 56 690 0126";
    private const string BizWebsite = "linenluxelaundry.com";
    private const string BizArea = "Al Furjan, Dubai";

    /// <summary>Render the receipt to PDF bytes.</summary>
    public static byte[] Render(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        EnsureFonts();

        var height = Height(order, lines);

        using var doc = new PdfDocument();
        doc.Info.Title = $"LinenLuxe Receipt {order.OrderNumber}";
        doc.Info.Author = BizName;

        // No footer band on a roll, so nothing is reserved at the bottom.
        var canvas = new PdfInvoiceCanvas(doc, PaperW, height, 0);
        Draw(canvas, order, lines);
        canvas.Finish();

        using var memory = new MemoryStream();
        doc.Save(memory, false);
        return memory.ToArray();
    }

    /// <summary>
    /// Show the till receipt, then let staff print it — usually to the thermal
    /// roll — or save it as a PDF.
    ///
    /// The preview matters more here than for the A4: a wrong receipt is wasted
    /// roll and a customer waiting while it prints again.
    /// </summary>
    public static void Print(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        var canvas = RenderVisuals(order, lines);

        new Views.PrintPreviewWindow(
            $"Receipt {order.OrderNumber}",
            $"{order.CustomerName}  ·  AED {order.Total:0.00}  ·  80 mm till roll",
            $"LinenLuxe Receipt {order.OrderNumber}",
            SuggestedFileName(order),
            canvas,
            () => Render(order, lines))
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        }.ShowDialog();
    }

    /// <summary>Draw onto WPF visuals — for printing, and for checking the layout.</summary>
    public static WpfInvoiceCanvas RenderVisuals(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        EnsureFonts();
        var canvas = new WpfInvoiceCanvas(PaperW, Height(order, lines), 0);
        Draw(canvas, order, lines);
        canvas.Finish();
        return canvas;
    }

    public static string SuggestedFileName(PosOrder order)
    {
        var number = string.IsNullOrWhiteSpace(order.OrderNumber)
            ? order.Id.ToString("N")[..8]
            : order.OrderNumber;
        return number + "-receipt.pdf";
    }

    private static void EnsureFonts() => InvoiceService.EnsureFontsPublic();

    /// <summary>
    /// How tall the roll needs to be. Worked out before drawing because a PDF page
    /// has to know its size up front — a receipt roll doesn't, but the file does.
    /// </summary>
    private static double Height(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        var h = 6.0;                       // top margin
        h += 24;                           // logo
        h += 4.3 + 3.6 + 3.6 + 5;          // name, tagline, website, area
        h += 6;                            // rule + gap
        h += 5 + 4.5 + 4.5;                // INVOICE line, number, date
        h += 5;                            // customer name
        h += 4.5;                          // customer phone
        h += 6;                            // rule
        h += 5;                            // items heading

        foreach (var line in lines)
        {
            h += 8.5;                                                    // name + qty/rate row
            if (line.EstUnitPrice is { } est && est != line.UnitPrice)
                h += 3.6;                                                // repriced note
        }

        h += 6;                            // rule
        h += 4.8;                          // subtotal
        if (order.ExpressSurcharge > 0) h += 4.8;
        if (order.Discount > 0) h += 4.8;
        if (order.VatEnabled) h += 4.8;
        h += 9;                            // total band
        h += 5.5 + 4.5;                    // payment method + status
        h += 6;                            // rule
        h += 4.5 + 4.5;                    // thank you + website
        h += 8;                            // tail the cutter needs

        return h;
    }

    private static void Draw(IInvoiceCanvas c, PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        var mid = PaperW / 2;
        var right = PaperW - Margin;
        var y = 6.0;

        // ---------- Head ----------
        var logo = InvoiceService.LogoBytes();
        if (logo is not null)
        {
            const double w = 34;
            c.Image(logo, mid - w / 2, y, w, w / 1.5);
            y += w / 1.5 + 3;
        }

        // The name is not printed under the logo: the logo already says LinenLuxe
        // Laundry, in the brand's own lettering, and setting it again in plain type
        // only competes with it. What the logo cannot say goes here instead — the
        // tagline and the website. The phone moves to the foot of the slip: a
        // customer reads the top to see who this is from and the bottom to find out
        // how to reach them.
        c.Text(BizTagline, mid, y, F(7.5), Grey, TextAlign.Center);
        y += 4;
        c.Text(BizWebsite, mid, y, F(7.5, bold: true), Black, TextAlign.Center);
        y += 3.6;
        c.Text(BizArea, mid, y, F(7), Grey, TextAlign.Center);
        y += 5;

        Divider(c, ref y);

        // ---------- Order ----------
        c.Text("INVOICE", mid, y, F(10, bold: true), Black, TextAlign.Center);
        y += 5;
        c.Text(order.OrderNumber, mid, y, F(11, bold: true), Black, TextAlign.Center);
        y += 5;
        c.Text(LocalTime(order.CreatedAt).ToString("dd MMM yyyy  HH:mm"), mid, y, F(7.5), Grey, TextAlign.Center);
        y += 5.5;

        c.Text(order.CustomerName, Margin, y, F(9, bold: true), Black);
        y += 4.5;
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            c.Text(order.CustomerPhone!, Margin, y, F(8), Grey);
            y += 4.5;
        }

        Divider(c, ref y);

        // ---------- Items ----------
        // Two lines each rather than columns: 72 mm cannot hold an item name and
        // three figures side by side without truncating the name, and the name is
        // the part the customer checks their clothes against.
        c.Text("ITEMS", Margin, y, F(7.5, bold: true), Grey);
        y += 4.5;

        foreach (var line in lines)
        {
            var service = line.ServiceType == "press_only" ? "Press Only" : "Clean & Press";
            c.Text(Fit(c, line.ItemName, F(8.5), ContentW), Margin, y, F(8.5), Black);
            y += 4;

            c.Text($"{line.Quantity} x {Money(line.UnitPrice)}   ·   {service}", Margin, y, F(7.5), Grey);
            c.Text(Money(line.LineTotal), right, y, F(8.5, bold: true), Black, TextAlign.Right);
            y += 4.5;

            if (line.EstUnitPrice is { } est && est != line.UnitPrice)
            {
                c.Text($"final price after inspection — was {Money(est)}",
                    Margin, y, F(6.8, italic: true), Grey);
                y += 3.6;
            }
        }

        Divider(c, ref y);

        // ---------- Money ----------
        void Row(string label, string value, bool bold = false)
        {
            c.Text(label, Margin, y, F(8, bold), bold ? Black : Grey);
            c.Text(value, right, y, F(8, bold), Black, TextAlign.Right);
            y += 4.8;
        }

        Row(order.VatEnabled ? "Subtotal (excl. VAT)" : "Subtotal", Money(order.Subtotal));
        if (order.ExpressSurcharge > 0) Row("Express service (50%)", "+ " + Money(order.ExpressSurcharge));
        if (order.Discount > 0) Row("Discount", "− " + Money(order.Discount));
        if (order.VatEnabled) Row("VAT (5%)", Money(order.VatAmount));

        y += 1;
        c.Fill(Margin, y, ContentW, 8, Black);
        c.Text("TOTAL", Margin + 2, y + 5.6, F(9, bold: true), new Rgb(255, 255, 255));
        c.Text("AED " + Money(order.Total), right - 2, y + 5.6, F(10, bold: true), new Rgb(255, 255, 255), TextAlign.Right);
        y += 12;

        c.Text("Payment", Margin, y, F(8), Grey);
        c.Text(PaymentLabel(order.PaymentMethod), right, y, F(8, bold: true), Black, TextAlign.Right);
        y += 4.5;
        c.Text("Status", Margin, y, F(8), Grey);
        c.Text(order.IsPaid ? "PAID" : "PAYMENT PENDING", right, y, F(8, bold: true), Black, TextAlign.Right);
        y += 5.5;

        Divider(c, ref y);

        c.Text("Please keep this receipt to collect your order.", mid, y, F(7), Grey, TextAlign.Center);
        y += 4;
        c.Text(BizPhone, mid, y, F(8, bold: true), Black, TextAlign.Center);
    }

    private static void Divider(IInvoiceCanvas c, ref double y)
    {
        c.Line(Margin, y, PaperW - Margin, y, Rule, 0.25);
        y += 4.5;
    }

    /// <summary>Trim to the paper width — a wrapped item name would push the row
    /// out of the height already reserved for it.</summary>
    private static string Fit(IInvoiceCanvas c, string text, Face face, double maxWidth)
    {
        if (c.Width(text, face) <= maxWidth) return text;

        var cut = text;
        while (cut.Length > 1 && c.Width(cut + "…", face) > maxWidth)
            cut = cut[..^1];
        return cut + "…";
    }

    private static Face F(double size, bool bold = false, bool italic = false)
        => new(Body, size, bold, italic);

    private static string Money(decimal amount) => amount.ToString("N2");

    private static string PaymentLabel(string method) => method switch
    {
        "online" => "Online",
        "card" => "Card",
        _ => "Cash on delivery",
    };

    private static DateTime LocalTime(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
}
