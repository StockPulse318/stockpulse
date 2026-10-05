namespace WarehouseInventory.Business.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }
    public bool ServerIsLowStock { get; set; }

    public bool IsLowStock => ServerIsLowStock || Quantity <= ReorderLevel;

    public Product Clone() => new()
    {
        Id = Id,
        Name = Name,
        Category = Category,
        Quantity = Quantity,
        UnitPrice = UnitPrice,
        ReorderLevel = ReorderLevel,
        ServerIsLowStock = ServerIsLowStock
    };
}
