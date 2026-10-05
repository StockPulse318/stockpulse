namespace WarehouseInventory.Models;

// One row of the Products table. Fields come straight from section 4 of the PRD.
public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }

    // The low stock rule from the PRD: Quantity <= Reorder Level. Kept in one place on purpose.
    public bool IsLowStock => Quantity <= ReorderLevel;

    public Product Copy() => (Product)MemberwiseClone();
}
