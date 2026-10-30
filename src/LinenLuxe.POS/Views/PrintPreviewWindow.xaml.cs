using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LinenLuxe.POS.Services.Invoicing;

namespace LinenLuxe.POS.Views;

/// <summary>
/// Shows what is about to be printed, before a printer is chosen.
///
/// The Windows print box looks identical for the A4 invoice and the 80 mm
/// receipt and offers no preview of its own, so staff had no way to tell which
/// of the two they were about to send — and getting it wrong on a thermal roll
/// wastes paper and a customer's time.
/// </summary>
public partial class PrintPreviewWindow : Window
{
    /// <summary>Enough to read the small print without making the window huge.</summary>
    private const double PreviewDpi = 110;

    private readonly WpfInvoiceCanvas _canvas;
    private readonly Func<byte[]> _pdf;
    private readonly string _suggestedFileName;
    private readonly string _documentName;

    public PrintPreviewWindow(
        string title,
        string subtitle,
        string documentName,
        string suggestedFileName,
        WpfInvoiceCanvas canvas,
        Func<byte[]> pdf)
    {
        InitializeComponent();
        Services.Configuration.WindowChrome.ApplyBrand(this);

        _canvas = canvas;
        _pdf = pdf;
        _suggestedFileName = suggestedFileName;
        _documentName = documentName;

        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        Title = title;

        PageList.ItemsSource = canvas.Pages.Select(Rasterise).ToList();
        PageCountText.Text = canvas.Pages.Count == 1
            ? "1 page"
            : $"{canvas.Pages.Count} pages";
    }

    /// <summary>
    /// Turn a drawn page into a picture for the screen. The same visuals go to the
    /// printer untouched, so what is shown here is the document itself rather than
    /// a second rendering of it that could disagree.
    /// </summary>
    private BitmapSource Rasterise(DrawingVisual page)
    {
        var scale = PreviewDpi / 96.0;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(_canvas.PageSize.Width * scale),
            (int)Math.Ceiling(_canvas.PageSize.Height * scale),
            PreviewDpi, PreviewDpi, PixelFormats.Pbgra32);
        bitmap.Render(page);
        bitmap.Freeze();
        return bitmap;
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() != true) return;

        dialog.PrintDocument(new VisualPaginator(_canvas.Pages, _canvas.PageSize), _documentName);
        Close();
    }

    private void OnSavePdf(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LinenLuxe Invoices");
        Directory.CreateDirectory(folder);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save as PDF",
            FileName = _suggestedFileName,
            DefaultExt = ".pdf",
            Filter = "PDF (*.pdf)|*.pdf",
            InitialDirectory = folder,
            OverwritePrompt = true,
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true) return;

        File.WriteAllBytes(dialog.FileName, _pdf());
        Close();
    }
}
