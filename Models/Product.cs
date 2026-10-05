namespace WarehouseInventory.Models;

// One row of the Products table. Supports branch/warehouse tracking and distinct categories.
public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Branch { get; set; } = "Main Warehouse";
    public string Category { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }

    // The low stock rule from the PRD: Quantity <= Reorder Level.
    public bool IsLowStock => Quantity <= ReorderLevel;

    public Product Copy() => (Product)MemberwiseClone();
}
