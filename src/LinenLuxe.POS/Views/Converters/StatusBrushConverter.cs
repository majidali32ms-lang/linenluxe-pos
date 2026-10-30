using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LinenLuxe.POS.Models;

namespace LinenLuxe.POS.Views.Converters;

/// <summary>
/// Colours an order-status badge. Six statuses would be six near-identical
/// DataTriggers repeated at every place a badge appears; one converter keeps the
/// colour of a status defined once.
///
/// The scale runs cool → warm → green: the further an order has travelled, the
/// closer it is to being off the shop's hands. Staff scanning the queue read the
/// colour before they read the word.
///
/// Pass "bg" for the badge fill, anything else for its text.
/// </summary>
public sealed class StatusBrushConverter : IValueConverter
{
    // Text colours; the fill is the same hue at low opacity.
    private static readonly Dictionary<string, Color> Ink = new()
    {
        [OrderStatus.Booked] = Color.FromRgb(0x6E, 0x72, 0x80),          // grey — not started
        [OrderStatus.PickedUp] = Color.FromRgb(0x2A, 0x62, 0xB8),        // blue — in our hands
        [OrderStatus.Processing] = Color.FromRgb(0x9A, 0x75, 0x24),      // gold — being worked on
        [OrderStatus.Ready] = Color.FromRgb(0x1B, 0x6B, 0x3F),           // green — done, waiting
        [OrderStatus.OutForDelivery] = Color.FromRgb(0x0A, 0x1A, 0x3A),  // navy — on its way
        [OrderStatus.Delivered] = Color.FromRgb(0x4A, 0x50, 0x5C),       // slate — closed
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? "";
        var colour = Ink.TryGetValue(key, out var c) ? c : Ink[OrderStatus.Booked];

        if (!string.Equals(parameter as string, "bg", StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(colour);

        // Same hue, barely there — a tint the label sits on without fighting it.
        return new SolidColorBrush(Color.FromArgb(0x22, colour.R, colour.G, colour.B));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Status badges are display-only.");
}
