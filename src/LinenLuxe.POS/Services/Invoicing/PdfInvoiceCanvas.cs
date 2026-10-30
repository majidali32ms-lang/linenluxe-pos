using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// Draws the invoice into a PDF. PDFsharp works in points, so every coordinate
/// is converted from millimetres on the way through.
/// </summary>
public sealed class PdfInvoiceCanvas : IInvoiceCanvas
{
    private readonly PdfDocument _doc;
    private readonly double _pageW;
    private readonly double _pageH;
    private readonly double _footerReserve;
    private XGraphics _gfx;

    public PdfInvoiceCanvas(PdfDocument doc, double pageWidthMm, double pageHeightMm, double footerReserveMm)
    {
        _doc = doc;
        _pageW = pageWidthMm;
        _pageH = pageHeightMm;
        _footerReserve = footerReserveMm;
        _gfx = Open();
    }

    public double ContentBottom => _pageH - _footerReserve;

    private XGraphics Open()
    {
        var page = _doc.AddPage();
        page.Width = XUnit.FromMillimeter(_pageW);
        page.Height = XUnit.FromMillimeter(_pageH);
        return XGraphics.FromPdfPage(page);
    }

    public void NewPage()
    {
        _gfx.Dispose();
        _gfx = Open();
    }

    public void Finish() => _gfx.Dispose();

    public void NeedRoom(ref double y, double height)
    {
        if (y + height <= ContentBottom) return;
        NewPage();
        y = 25;
    }

    public void Text(string text, double x, double y, Face face, Rgb colour, TextAlign align = TextAlign.Left)
    {
        var format = align switch
        {
            TextAlign.Center => XStringFormats.BaseLineCenter,
            TextAlign.Right => XStringFormats.BaseLineRight,
            _ => XStringFormats.BaseLineLeft,
        };
        _gfx.DrawString(text, Font(face), new XSolidBrush(Colour(colour)), Pt(x), Pt(y), format);
    }

    public void Line(double x1, double y1, double x2, double y2, Rgb colour, double width)
        => _gfx.DrawLine(new XPen(Colour(colour), Pt(width)), Pt(x1), Pt(y1), Pt(x2), Pt(y2));

    public void Fill(double x, double y, double w, double h, Rgb colour)
        => _gfx.DrawRectangle(new XSolidBrush(Colour(colour)), Pt(x), Pt(y), Pt(w), Pt(h));

    public void Rect(double x, double y, double w, double h, Rgb colour, double width)
        => _gfx.DrawRectangle(new XPen(Colour(colour), Pt(width)), Pt(x), Pt(y), Pt(w), Pt(h));

    public void Dot(double x, double y, double r, Rgb colour)
        => _gfx.DrawEllipse(new XSolidBrush(Colour(colour)), Pt(x - r), Pt(y - r), Pt(2 * r), Pt(2 * r));

    public void Image(byte[] image, double x, double y, double w, double h)
    {
        // XImage reads lazily, so the stream has to outlive this call.
        var xImage = XImage.FromStream(new MemoryStream(image));
        _gfx.DrawImage(xImage, Pt(x), Pt(y), Pt(w), Pt(h));
    }

    public double Width(string text, Face face) => Mm(_gfx.MeasureString(text, Font(face)).Width);

    public List<string> Wrap(string text, Face face, double maxWidth)
        => TextWrap.Break(text, maxWidth, s => Width(s, face));

    private static XFont Font(Face face)
    {
        var style = XFontStyleEx.Regular;
        if (face.Bold) style |= XFontStyleEx.Bold;
        if (face.Italic) style |= XFontStyleEx.Italic;
        return new XFont(face.Family, face.Size, style);
    }

    private static XColor Colour(Rgb c) => XColor.FromArgb(c.R, c.G, c.B);

    private static double Pt(double mm) => XUnit.FromMillimeter(mm).Point;
    private static double Mm(double pt) => XUnit.FromPoint(pt).Millimeter;
}

/// <summary>Word wrapping, shared by both surfaces so they break lines alike.</summary>
public static class TextWrap
{
    public static List<string> Break(string text, double maxWidth, Func<string, double> measure)
    {
        var result = new List<string>();
        var line = "";

        foreach (var word in text.Split(' '))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && measure(candidate) > maxWidth)
            {
                result.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0) result.Add(line);
        return result.Count > 0 ? result : [""];
    }
}
