namespace WarehouseInventory.Services;

// Thrown by the business layer when an operation isn't allowed (duplicate ID,
// stock going below zero, and so on). The message is shown to the user as is.
public class InventoryException : Exception
{
    public InventoryException(string message) : base(message) { }
}
