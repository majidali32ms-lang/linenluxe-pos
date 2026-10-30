using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LinenLuxe.POS.Services.Invoicing;

/// <summary>
/// Feeds already-drawn pages to the Windows print system, one visual per sheet.
///
/// WPF prints through a paginator rather than a list of visuals, and the plain
/// PrintVisual shortcut only handles a single page — a long order would have
/// been silently cut off at the bottom of sheet one.
/// </summary>
public sealed class VisualPaginator : DocumentPaginator
{
    private readonly IReadOnlyList<DrawingVisual> _pages;

    public VisualPaginator(IReadOnlyList<DrawingVisual> pages, Size pageSize)
    {
        _pages = pages;
        PageSize = pageSize;
    }

    public override bool IsPageCountValid => true;
    public override int PageCount => _pages.Count;
    public override Size PageSize { get; set; }
    public override IDocumentPaginatorSource? Source => null;

    public override DocumentPage GetPage(int pageNumber)
    {
        var visual = _pages[pageNumber];
        return new DocumentPage(visual, PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}
