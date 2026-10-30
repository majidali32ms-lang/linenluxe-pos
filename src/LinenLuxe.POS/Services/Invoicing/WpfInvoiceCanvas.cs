using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// Draws the same invoice onto WPF visuals, so it can go to a printer through
/// the normal Windows print dialog — the staff member picks the printer, the
/// paper and the copies, exactly as they would from any other application.
///
/// Each page becomes one <see cref="DrawingVisual"/>. WPF measures in
/// device-independent units of 1/96 inch, so millimetres are converted here in
/// the same way the PDF surface converts them to points.
/// </summary>
public sealed class WpfInvoiceCanvas : IInvoiceCanvas
{
    private const double UnitsPerMm = 96.0 / 25.4;

    private readonly double _pageW;
    private readonly double _pageH;
    private readonly double _footerReserve;

    private DrawingVisual _visual = null!;
    private DrawingContext _dc = null!;

    public WpfInvoiceCanvas(double pageWidthMm, double pageHeightMm, double footerReserveMm)
    {
        _pageW = pageWidthMm;
        _pageH = pageHeightMm;
        _footerReserve = footerReserveMm;
        Open();
    }

    /// <summary>One visual per page, in order, ready to be printed.</summary>
    public List<DrawingVisual> Pages { get; } = [];

    public Size PageSize => new(_pageW * UnitsPerMm, _pageH * UnitsPerMm);

    public double ContentBottom => _pageH - _footerReserve;

    private void Open()
    {
        _visual = new DrawingVisual();
        _dc = _visual.RenderOpen();

        // Paper is white. Printers leave it alone, but a preview rendered to an
        // image would otherwise come out transparent.
        _dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, PageSize.Width, PageSize.Height));
    }

    private void Close()
    {
        _dc.Close();
        Pages.Add(_visual);
    }

    public void NewPage()
    {
        Close();
        Open();
    }

    public void Finish() => Close();

    public void NeedRoom(ref double y, double height)
    {
        if (y + height <= ContentBottom) return;
        NewPage();
        y = 25;
    }

    public void Text(string text, double x, double y, Face face, Rgb colour, TextAlign align = TextAlign.Left)
    {
        var formatted = Format(text, face, colour);

        // The layout positions text by its baseline; WPF draws from the top-left.
        var left = align switch
        {
            TextAlign.Center => U(x) - formatted.Width / 2,
            TextAlign.Right => U(x) - formatted.Width,
            _ => U(x),
        };

        _dc.DrawText(formatted, new Point(left, U(y) - formatted.Baseline));
    }

    public void Line(double x1, double y1, double x2, double y2, Rgb colour, double width)
        => _dc.DrawLine(new Pen(Brush(colour), U(width)), new Point(U(x1), U(y1)), new Point(U(x2), U(y2)));

    public void Fill(double x, double y, double w, double h, Rgb colour)
        => _dc.DrawRectangle(Brush(colour), null, new Rect(U(x), U(y), U(w), U(h)));

    public void Rect(double x, double y, double w, double h, Rgb colour, double width)
        => _dc.DrawRectangle(null, new Pen(Brush(colour), U(width)), new Rect(U(x), U(y), U(w), U(h)));

    public void Dot(double x, double y, double r, Rgb colour)
        => _dc.DrawEllipse(Brush(colour), null, new Point(U(x), U(y)), U(r), U(r));

    public void Image(byte[] image, double x, double y, double w, double h)
    {
        var decoder = BitmapDecoder.Create(new MemoryStream(image),
            BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        _dc.DrawImage(decoder.Frames[0], new Rect(U(x), U(y), U(w), U(h)));
    }

    public double Width(string text, Face face) => Format(text, face, default).Width / UnitsPerMm;

    public List<string> Wrap(string text, Face face, double maxWidth)
        => TextWrap.Break(text, maxWidth, s => Width(s, face));

    /// <summary>
    /// Great Vibes ships inside the app rather than being installed, so WPF has to
    /// be pointed at the resource. Asking for it by name alone silently falls back
    /// to a default face — the flourish came out in plain sans on the printed
    /// sheet while the PDF had it right.
    /// </summary>
    private static FontFamily Family(string name)
        => name == WindowsFontResolver.ScriptFamily
            ? new FontFamily(
                new Uri("pack://application:,,,/LinenLuxe.POS;component/Assets/"),
                $"./#{WindowsFontResolver.ScriptFamily}")
            : new FontFamily(name);

    private static FormattedText Format(string text, Face face, Rgb colour)
    {
        var typeface = new Typeface(
            Family(face.Family),
            face.Italic ? FontStyles.Italic : FontStyles.Normal,
            face.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);

        // Font sizes are points, as in the PDF; WPF wants 1/96in units.
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            face.Size * 96.0 / 72.0,
            Brush(colour),
            1.0);
    }

    private static SolidColorBrush Brush(Rgb c)
        => new(Color.FromRgb(c.R, c.G, c.B));

    private static double U(double mm) => mm * UnitsPerMm;
}
