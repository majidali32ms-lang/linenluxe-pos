namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>A colour, independent of PDFsharp and WPF.</summary>
public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>A typeface request, resolved by whichever surface is drawing.</summary>
public readonly record struct Face(string Family, double Size, bool Bold = false, bool Italic = false);

public enum TextAlign { Left, Center, Right }

/// <summary>
/// A page being drawn on, measured in millimetres.
///
/// The invoice is laid out once and can then go to a PDF or to a printer. Two
/// copies of that layout would drift apart the first time one of them was
/// tweaked, and a customer would end up holding a different document from the
/// one on file.
///
/// Millimetres because the layout is a port of the website's jsPDF code, which
/// works in millimetres; each surface converts on the way through.
/// </summary>
public interface IInvoiceCanvas
{
    /// <summary>Lowest y a row may occupy before running into the footer.</summary>
    double ContentBottom { get; }

    void Text(string text, double x, double y, Face face, Rgb colour, TextAlign align = TextAlign.Left);
    void Line(double x1, double y1, double x2, double y2, Rgb colour, double width);
    void Fill(double x, double y, double w, double h, Rgb colour);
    void Rect(double x, double y, double w, double h, Rgb colour, double width);
    void Dot(double x, double y, double r, Rgb colour);

    /// <summary>JPEG or PNG bytes; each surface decodes them its own way.</summary>
    void Image(byte[] image, double x, double y, double w, double h);

    /// <summary>Width of a string in millimetres.</summary>
    double Width(string text, Face face);

    /// <summary>Break text to fit a width in millimetres, on word boundaries.</summary>
    List<string> Wrap(string text, Face face, double maxWidth);

    void NewPage();

    /// <summary>Start a new page if the next block wouldn't fit on this one.</summary>
    void NeedRoom(ref double y, double height);

    /// <summary>Close the last page off.</summary>
    void Finish();
}
