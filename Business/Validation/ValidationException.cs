namespace WarehouseInventory.Business.Validation;

public class ValidationException : Exception
{
    public string? PropertyName { get; }

    public ValidationException(string message, string? propertyName = null) : base(message)
    {
        PropertyName = propertyName;
    }
}
