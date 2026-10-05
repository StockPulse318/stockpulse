using System.Windows;
using System.Windows.Media;
using WarehouseInventory.Models;

namespace WarehouseInventory.ViewModels;

// What one row of the grid shows: a Product plus the bits only the screen cares about
// (bar length, where the reorder tick sits, status colour).
public class ProductRow
{
    private static readonly Brush Green = Make(0x2E, 0x7D, 0x32);
    private static readonly Brush Amber = Make(0xE5, 0xA8, 0x00);
    private static readonly Brush Red = Make(0xC4, 0x2B, 0x1C);
    private static readonly Brush AmberText = Make(0x8A, 0x63, 0x00);

    public ProductRow(Product p, int scaleMax)
    {
        Product = p;
        LevelPercent = Clamp(p.Quantity * 100.0 / scaleMax);
        double tick = Clamp(p.ReorderLevel * 100.0 / scaleMax);
        BeforeTick = new GridLength(tick, GridUnitType.Star);
        AfterTick = new GridLength(100 - tick, GridUnitType.Star);

        if (p.Quantity == 0) { StatusText = "Out of stock"; BarBrush = Red; StatusBrush = Red; }
        else if (p.IsLowStock) { StatusText = "Restock"; BarBrush = Amber; StatusBrush = AmberText; }
        else { StatusText = ""; BarBrush = Green; StatusBrush = Green; }
    }

    public Product Product { get; }
    public string Id => Product.Id;
    public string Name => Product.Name;
    public string Category => Product.Category;
    public int Quantity => Product.Quantity;
    public int ReorderLevel => Product.ReorderLevel;
    public decimal UnitPrice => Product.UnitPrice;

    public double LevelPercent { get; }
    public GridLength BeforeTick { get; }
    public GridLength AfterTick { get; }
    public string StatusText { get; }
    public Brush BarBrush { get; }
    public Brush StatusBrush { get; }

    private static double Clamp(double v) => Math.Max(0, Math.Min(100, v));

    private static Brush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
