using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using LinenLuxe.POS.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// Turns a saved POS order into the LinenLuxe A4 PDF invoice.
///
/// This is a port of the website's generator (webapp/src/lib/invoice.js), drawn
/// against the same millimetre grid so a customer gets the identical document
/// whether they booked online or walked in. Where the two must differ it is only
/// because the POS knows more: an order can carry an express surcharge and a
/// counter discount, which get their own rows in the totals block.
///
/// Drawn with PDFsharp directly rather than laid out with MigraDoc, because the
/// web version positions everything absolutely and a flow layout cannot be made
/// to land on the same coordinates.
///
/// Every figure comes from the order row, never recomputed — that is the whole
/// reason all five money columns are stored. An invoice reprinted months later
/// shows exactly what the customer agreed to, even if the price list has moved.
///
/// The heading says INVOICE, not TAX INVOICE: a UAE tax invoice must carry the
/// business's TRN, and we don't have one to print.
/// </summary>
public static class InvoiceService
{
    // ----- Palette, matching webapp/src/lib/invoice.js exactly -----
    private static readonly Rgb Navy = new(26, 42, 74);
    private static readonly Rgb Gold = new(201, 162, 75);
    private static readonly Rgb GoldStrong = new(184, 144, 47);
    private static readonly Rgb Ink = new(60, 60, 60);
    private static readonly Rgb Muted = new(120, 120, 120);
    private static readonly Rgb Hairline = new(222, 213, 192);
    private static readonly Rgb RowAlt = new(250, 247, 240);
    private static readonly Rgb FooterInk = new(235, 235, 235);
    private static readonly Rgb Paid = new(34, 120, 60);
    private static readonly Rgb White = new(255, 255, 255);

    // Helvetica on the web; Arial carries the same metrics on Windows.
    private const string Body = "Arial";

    // ----- Business details (webapp/src/lib/brand.js) -----
    private const string BizName = "LinenLuxe Laundry";
    private const string BizPhone = "+971 56 690 0126";
    private const string BizEmail = "info@linenluxelaundry.com";
    private const string BizWebsite = "linenluxelaundry.com";
    private const string BizArea = "Al Furjan, Dubai";

    // ----- Page geometry (millimetres) -----
    private const double PageW = 210;
    private const double PageH = 297;
    private const double Margin = 15;

    /// <summary>Bottom strip the flourish and footer band own; content stops here.</summary>
    private const double FooterReserve = 26;

    private static bool _fontsReady;

    /// <summary>Default place to file invoices, one PDF per order number.</summary>
    public static string InvoiceFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "LinenLuxe Invoices");

    /// <summary>The order number as a filename, e.g. "P00007.pdf".</summary>
    public static string SuggestedFileName(PosOrder order)
    {
        var number = string.IsNullOrWhiteSpace(order.OrderNumber)
            ? order.Id.ToString("N")[..8]
            : order.OrderNumber;
        return number + ".pdf";
    }

    /// <summary>Render the invoice to PDF bytes, so the caller decides where it goes.</summary>
    public static byte[] Render(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        EnsureFonts();

        using var doc = new PdfDocument();
        doc.Info.Title = $"LinenLuxe Invoice {order.OrderNumber}";
        doc.Info.Author = BizName;

        var canvas = new PdfInvoiceCanvas(doc, PageW, PageH, FooterReserve);
        Draw(canvas, order, lines);
        canvas.Finish();

        using var memory = new MemoryStream();
        doc.Save(memory, false);
        return memory.ToArray();
    }

    /// <summary>
    /// Show the A4 invoice, then let staff print it or save it as a PDF.
    ///
    /// A preview first, rather than straight to the printer: the Windows print box
    /// looks the same for the invoice and the till receipt and shows neither, so
    /// without this there is no way to tell which one is about to come out.
    ///
    /// The same Draw routine produces the preview, the print and the PDF, so all
    /// three are the same document rather than three renderings that can drift.
    /// </summary>
    public static void Print(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        var canvas = RenderVisuals(order, lines);

        new Views.PrintPreviewWindow(
            $"Invoice {order.OrderNumber}",
            $"{order.CustomerName}  ·  AED {order.Total:0.00}  ·  A4 sheet",
            $"LinenLuxe Invoice {order.OrderNumber}",
            SuggestedFileName(order),
            canvas,
            () => Render(order, lines))
        {
            Owner = Application.Current?.MainWindow,
        }.ShowDialog();
    }

    /// <summary>Draw the invoice onto WPF visuals — used for printing, and to
    /// check on screen that the printed sheet matches the PDF.</summary>
    public static WpfInvoiceCanvas RenderVisuals(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        EnsureFonts();
        var canvas = new WpfInvoiceCanvas(PageW, PageH, FooterReserve);
        Draw(canvas, order, lines);
        canvas.Finish();
        return canvas;
    }

    /// <summary>
    /// Convenience: render and write to the invoice folder, returning the path.
    /// Used for reprints and for checking the layout outside the app.
    /// </summary>
    public static string Create(PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        var bytes = Render(order, lines);
        Directory.CreateDirectory(InvoiceFolder);
        var path = Path.Combine(InvoiceFolder, SuggestedFileName(order));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // Remembered between saves so staff pick their folder once a shift, not once
    // an order.
    private static string? _lastFolder;

    /// <summary>
    /// Ask the staff member where to file this invoice. The order number is filled
    /// in as the name, so the saved file always says which order it belongs to.
    /// Returns null if they cancel — the order itself is already saved either way.
    /// </summary>
    public static string? AskWhereToSave(PosOrder order)
    {
        var folder = _lastFolder is not null && Directory.Exists(_lastFolder)
            ? _lastFolder
            : InvoiceFolder;
        Directory.CreateDirectory(folder);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = $"Save invoice {order.OrderNumber}",
            FileName = SuggestedFileName(order),
            DefaultExt = ".pdf",
            Filter = "PDF invoice (*.pdf)|*.pdf",
            InitialDirectory = folder,
            OverwritePrompt = true,
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true) return null;

        _lastFolder = Path.GetDirectoryName(dialog.FileName);
        return dialog.FileName;
    }

    /// <summary>Shared with the receipt, which needs the same fonts registered.</summary>
    internal static void EnsureFontsPublic() => EnsureFonts();

    private static void EnsureFonts()
    {
        if (_fontsReady) return;
        GlobalFontSettings.FontResolver ??= new WindowsFontResolver();
        _fontsReady = true;
    }

    // ==================== The document ====================

    private static void Draw(IInvoiceCanvas c, PosOrder order, IReadOnlyList<PosOrderItem> lines)
    {
        // ---------- Header ----------
        var logo = LoadLogo();
        if (logo is not null) c.Image(logo, Margin, 13, 54, 36);

        c.Text("INVOICE", PageW - Margin, 24, F(30, bold: true), Navy, TextAlign.Right);
        c.Line(PageW - Margin - 34, 27, PageW - Margin, 27, Gold, 0.8);

        var labelX = PageW - 78;
        var valueX = PageW - 50;
        var my = 35.0;
        (string, string)[] meta =
        [
            ("Invoice No", order.OrderNumber),
            ("Date", Date(order.CreatedAt)),
            ("Phone", BizPhone),
            ("Email", BizEmail),
            ("Website", BizWebsite),
        ];
        foreach (var (key, value) in meta)
        {
            c.Text(key, labelX, my, F(9, bold: true), Navy);
            c.Text(":", valueX - 3, my, F(9), Muted);
            c.Text(value, valueX, my, F(9), Ink);
            my += 5;
        }

        var y = Math.Max(54, my + 2);
        c.Line(Margin, y, PageW - Margin, y, Gold, 0.6);
        y += 8;

        // ---------- Bill to (left) + order details (right) ----------
        var colR = PageW / 2 + 6;
        var startY = y;

        c.Text("BILL TO", Margin, y, F(10, bold: true), GoldStrong);
        y += 6;
        c.Text(string.IsNullOrWhiteSpace(order.CustomerName) ? "Customer" : order.CustomerName,
            Margin, y, F(11, bold: true), Navy);
        y += 5;
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            c.Text(order.CustomerPhone!, Margin, y, F(9.5), Ink);
            y += 5;
        }
        var leftBottom = y;

        var ry = startY;
        c.Text("Pickup Date", colR, ry, F(9.5, bold: true), Navy);
        c.Text(": " + Date(PickupDate(order)), colR + 34, ry, F(9.5), Ink);
        ry += 6.5;

        c.Text("Payment Status", colR, ry, F(9.5, bold: true), Navy);
        c.Text(": " + (order.IsPaid ? "Paid" : "Payment Pending"), colR + 34, ry,
            F(9.5), order.IsPaid ? Paid : Ink);
        ry += 6.8;

        c.Text("Payment Method", colR, ry, F(9.5, bold: true), Navy);
        ry += 5.6;

        // Card is a counter-only method — the website offers COD and Online. It
        // takes the same tick-box row so the invoice still reads the same way.
        CheckBox(c, colR + 2, ry, order.PaymentMethod == "cod");
        c.Text("COD", colR + 7.5, ry, F(9.5), Ink);
        CheckBox(c, colR + 24, ry, order.PaymentMethod == "card");
        c.Text("Card", colR + 29.5, ry, F(9.5), Ink);
        CheckBox(c, colR + 46, ry, order.PaymentMethod == "online");
        c.Text("Online", colR + 51.5, ry, F(9.5), Ink);
        ry += 6.5;

        y = Math.Max(leftBottom, ry) + 4;

        // ---------- Items ----------
        y = Items(c, lines, y);
        y += 8;

        // ---------- Totals ----------
        y = Totals(c, order, y);

        // ---------- Amount in words ----------
        c.NeedRoom(ref y, 20);
        c.Text("Amount in Words:", Margin, y, F(9.5, bold: true), Navy);
        var words = c.Wrap(AmountInWords.Aed(order.Total), F(9.5, italic: true), PageW - 2 * Margin);
        var wy = y + 5;
        foreach (var line in words)
        {
            c.Text(line, Margin, wy, F(9.5, italic: true), Ink);
            wy += 4.6;
        }
        y = wy + 6;

        // ---------- Terms ----------
        c.NeedRoom(ref y, 34);
        c.Text("TERMS & CONDITIONS", Margin, y, F(10, bold: true), GoldStrong);
        y += 5;
        string[] terms =
        [
            "1. Please check your items upon delivery.",
            "2. Any complaint must be reported within 12 hours of delivery.",
            "3. In case of damage caused by us, compensation is limited to 10x the service charge.",
            "4. We are responsible for your items up to 1 month from the delivery date.",
            "5. This is the final invoice with confirmed prices after inspection.",
        ];
        foreach (var term in terms)
        {
            foreach (var line in c.Wrap(term, F(8.5), PageW - 2 * Margin))
            {
                c.Text(line, Margin, y, F(8.5), Muted);
                y += 4.4;
            }
        }

        y += 2.5;
        c.Text("Thank you for choosing LinenLuxe Laundry!", Margin, y, F(9), Navy);

        // ---------- "Thank You!" flourish ----------
        var scriptFont = Script(40);
        var ty = Math.Max(y + 16, PageH - 44);
        c.Text("Thank You!", PageW / 2, ty, scriptFont, GoldStrong, TextAlign.Center);

        var tw = c.Width("Thank You!", scriptFont);
        var lineY = ty - 4;
        const double gap = 7;
        c.Line(Margin + 16, lineY, PageW / 2 - tw / 2 - gap, lineY, Gold, 0.4);
        c.Line(PageW / 2 + tw / 2 + gap, lineY, PageW - Margin - 16, lineY, Gold, 0.4);
        c.Dot(Margin + 14, lineY, 0.8, Gold);
        c.Dot(PageW - Margin - 14, lineY, 0.8, Gold);
    }

    // ---------- Items table (autoTable "grid" theme, drawn by hand) ----------
    private static double Items(IInvoiceCanvas c, IReadOnlyList<PosOrderItem> lines, double y)
    {
        const double qtyW = 22, rateW = 34, amountW = 38;
        const double x0 = Margin;
        var x3End = PageW - Margin;
        var x3 = x3End - amountW;
        var x2 = x3 - rateW;
        var x1 = x2 - qtyW;

        const double hpad = 2.6;
        const double headH = 8.2;
        const double rowH = 9.3;

        void Head(double top)
        {
            c.Fill(x0, top, x3End - x0, headH, Navy);
            var baseline = top + headH / 2 + 1.3;
            c.Text("Item", x0 + hpad, baseline, F(10, bold: true), White);
            c.Text("Qty", x1 + hpad, baseline, F(10, bold: true), White);
            c.Text("Rate (AED)", x2 + hpad, baseline, F(10, bold: true), White);
            c.Text("Amount (AED)", x3 + hpad, baseline, F(10, bold: true), White);
            Grid(top, headH);
        }

        void Grid(double top, double h)
        {
            c.Rect(x0, top, x3End - x0, h, Hairline, 0.1);
            c.Line(x1, top, x1, top + h, Hairline, 0.1);
            c.Line(x2, top, x2, top + h, Hairline, 0.1);
            c.Line(x3, top, x3, top + h, Hairline, 0.1);
        }

        Head(y);
        y += headH;

        var striped = true;
        foreach (var line in lines)
        {
            // A repriced line carries two extra lines explaining itself, so it
            // needs a taller row — the same shape the web invoice gives it.
            var repriced = line.EstUnitPrice is { } est && est != line.UnitPrice;
            var thisRowH = repriced ? rowH + 7.0 : rowH;

            // A new page needs the column headings repeated, or the second page
            // is a wall of numbers with nothing saying what they are.
            if (y + thisRowH > c.ContentBottom)
            {
                c.NewPage();
                y = Margin + 10;
                Head(y);
                y += headH;
            }

            if (striped) c.Fill(x0, y, x3End - x0, thisRowH, RowAlt);
            striped = !striped;

            // Repriced rows top-align so the note sits under the item name;
            // ordinary rows stay centred.
            var baseline = repriced ? y + 5.9 : y + thisRowH / 2 + 1.3;

            // The service joins the item name, as the website's item names carry
            // it too ("Suit & Blazer Cleaning Services"), keeping one line a row.
            var service = line.ServiceType == "press_only" ? "Press Only" : "Clean & Press";
            c.Text($"{line.ItemName} — {service}", x0 + hpad, baseline, F(10), Ink);

            if (repriced)
            {
                var note = y + 10.2;
                c.Text("Final price after inspection — updated based on the item's condition.",
                    x0 + hpad, note, F(7, italic: true), GoldStrong);
                c.Text($"Estimated AED {Money(line.EstUnitPrice!.Value)}   ·   Final AED {Money(line.UnitPrice)}",
                    x0 + hpad, note + 3.4, F(7), Muted);
            }

            c.Text(line.Quantity.ToString(), (x1 + x2) / 2, baseline, F(10), Ink, TextAlign.Center);
            c.Text(Money(line.UnitPrice), x2 + rateW - hpad, baseline, F(10), Ink, TextAlign.Right);
            c.Text(Money(line.LineTotal), x3End - hpad, baseline, F(10), Ink, TextAlign.Right);

            Grid(y, thisRowH);
            y += thisRowH;
        }

        return y;
    }

    // ---------- Totals block ----------
    private static double Totals(IInvoiceCanvas c, PosOrder order, double y)
    {
        var labelX = PageW - Margin - 88;
        var valueX = PageW - Margin;

        c.NeedRoom(ref y, 40);

        void Row(string label, string value)
        {
            c.Text(label, labelX, y, F(10), Ink);
            c.Text(value, valueX, y, F(10), Ink, TextAlign.Right);
            y += 6;
        }

        Row(order.VatEnabled ? "Subtotal (excl. VAT)" : "Subtotal", Money(order.Subtotal));

        // POS-only lines. They appear only when they applied — a zero discount on
        // a printed invoice invites the question "what discount?".
        if (order.ExpressSurcharge > 0) Row("Express Service (50%)", "+ " + Money(order.ExpressSurcharge));
        if (order.Discount > 0) Row("Discount", "− " + Money(order.Discount));
        if (order.VatEnabled) Row("VAT (5%)", Money(order.VatAmount));

        // Total band: navy label, gold amount.
        y += 1;
        const double bandH = 11;
        var bandLeft = labelX - 4;
        var splitX = PageW - Margin - 50;
        c.Fill(bandLeft, y, splitX - bandLeft, bandH, Navy);
        c.Fill(splitX, y, valueX - splitX, bandH, Gold);
        c.Text("TOTAL AMOUNT", bandLeft + 4, y + 7.2, F(10.5, bold: true), White);
        c.Text("AED " + Money(order.Total), valueX - 3, y + 7.2, F(10.5, bold: true), Navy, TextAlign.Right);

        return y + bandH + 8;
    }

    // A small square, ticked in gold — same shape the web invoice draws.
    private static void CheckBox(IInvoiceCanvas c, double x, double y, bool ticked)
    {
        const double size = 3.4;
        var top = y - 2.9;
        c.Rect(x, top, size, size, Navy, 0.3);
        if (!ticked) return;
        c.Line(x + 0.6, top + 1.7, x + 1.5, top + 2.6, GoldStrong, 0.7);
        c.Line(x + 1.5, top + 2.6, x + 3.2, top + 0.5, GoldStrong, 0.7);
    }

    // ==================== helpers ====================

    private static Face F(double size, bool bold = false, bool italic = false) => new(Body, size, bold, italic);
    private static Face Script(double size) => new(WindowsFontResolver.ScriptFamily, size);

    private static string Money(decimal amount) => amount.ToString("N2");
    private static string Date(DateTime value) => value.ToString("dd MMM yyyy");

    // Collection date: express is next day, standard is 48 hours.
    private static DateTime PickupDate(PosOrder order) =>
        LocalTime(order.CreatedAt).Date.AddDays(order.IsExpress ? 1 : 2);

    // Timestamps arrive from Postgres in UTC; show them in the shop's own time.
    private static DateTime LocalTime(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();

    // ==================== the logo ====================

    private static byte[]? _logo;
    private static bool _logoTried;

    /// <summary>Wide enough for 300+ dpi at the 54 mm the logo is printed at.</summary>
    private const int LogoPixelWidth = 900;

    /// <summary>
    /// The logo ships inside the exe as a WPF resource, which PDFsharp can't open
    /// by path — it takes a stream instead.
    ///
    /// It is scaled down and re-encoded as JPEG first. The original is 1264px for
    /// the login screen; at print size that is far past what any printer resolves,
    /// and embedding it whole made every invoice ~900 KB. A shop writing 50 a day
    /// would have been filing gigabytes a year for detail nobody can see. JPEG,
    /// not PNG, because the artwork is a gold gradient on solid white with no
    /// transparency to keep, and PNG stores that very poorly.
    /// </summary>
    /// <summary>Shared with the receipt, so the artwork is prepared once.</summary>
    internal static byte[]? LogoBytes() => LoadLogo();

    private static byte[]? LoadLogo()
    {
        if (_logoTried) return _logo;
        _logoTried = true;

        try
        {
            // Named assembly, not the short "pack://application:,,,/…" form: that
            // resolves against whichever assembly is the entry point, so it would
            // find the logo in the running POS but not when an invoice is rendered
            // from anywhere else — which is how its layout gets checked.
            Application.ResourceAssembly ??= typeof(InvoiceService).Assembly;

            var uri = new Uri("pack://application:,,,/LinenLuxe.POS;component/Assets/linenluxe-logo-invoice.png");
            var resource = Application.GetResourceStream(uri)?.Stream;
            if (resource is null) return null;

            byte[] jpeg;
            using (resource)
            {
                var frame = BitmapDecoder
                    .Create(resource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
                    .Frames[0];

                BitmapSource source = frame;
                if (frame.PixelWidth > LogoPixelWidth)
                {
                    var scale = (double)LogoPixelWidth / frame.PixelWidth;
                    source = new TransformedBitmap(frame,
                        new System.Windows.Media.ScaleTransform(scale, scale));
                }

                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(source));

                using var memory = new MemoryStream();
                encoder.Save(memory);
                jpeg = memory.ToArray();
            }

            // Handed to whichever surface is drawing; each decodes it its own way.
            _logo = jpeg;
            return _logo;
        }
        catch
        {
            return null;   // the invoice still prints, just without the artwork
        }
    }
}
