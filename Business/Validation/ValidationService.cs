using WarehouseInventory.Business.Models;

namespace WarehouseInventory.Business.Validation;

public interface IValidationService
{
    void ValidateCredentials(string? username, string? password);
    int ValidateStockAmount(string? amountText);
    void ValidateStockAmount(int amount);
    void ValidateStockOut(int currentQuantity, int requestedAmount);
    void ValidateProduct(string? name, string? category, decimal unitPrice, int reorderLevel, int? quantity = null);
    void ValidateRoleAction(User? user, string actionName);
    bool IsRestockNeeded(int quantity, int reorderLevel);
}

public sealed class ValidationService : IValidationService
{
    public void ValidateCredentials(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ValidationException("Username is required.", nameof(username));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ValidationException("Password is required.", nameof(password));
        }
    }

    public int ValidateStockAmount(string? amountText)
    {
        if (string.IsNullOrWhiteSpace(amountText))
        {
            throw new ValidationException("Amount is required.", nameof(amountText));
        }

        if (!int.TryParse(amountText.Trim(), out int amount))
        {
            throw new ValidationException("Amount must be a valid whole number.", nameof(amountText));
        }

        ValidateStockAmount(amount);
        return amount;
    }

    public void ValidateStockAmount(int amount)
    {
        if (amount <= 0)
        {
            throw new ValidationException("Stock movement amount must be greater than zero.", nameof(amount));
        }
    }

    public void ValidateStockOut(int currentQuantity, int requestedAmount)
    {
        ValidateStockAmount(requestedAmount);

        if (requestedAmount > currentQuantity)
        {
            throw new ValidationException(
                $"Stock-out amount ({requestedAmount}) exceeds available inventory quantity ({currentQuantity}).",
                nameof(requestedAmount));
        }
    }

    public void ValidateProduct(string? name, string? category, decimal unitPrice, int reorderLevel, int? quantity = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Product Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ValidationException("Category is required.", nameof(category));
        }

        if (unitPrice < 0)
        {
            throw new ValidationException("Unit Price cannot be negative.", nameof(unitPrice));
        }

        if (reorderLevel < 0)
        {
            throw new ValidationException("Reorder Level cannot be negative.", nameof(reorderLevel));
        }

        if (quantity.HasValue && quantity.Value < 0)
        {
            throw new ValidationException("Quantity cannot be negative.", nameof(quantity));
        }
    }

    public void ValidateRoleAction(User? user, string actionName)
    {
        if (user == null)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (actionName is "AddProduct" or "EditProduct" or "DeleteProduct")
        {
            if (user.Role != UserRole.WarehouseManager)
            {
                throw new ValidationException(
                    "Only a Warehouse Manager has permission to add, edit, or delete products.");
            }
        }
    }

    public bool IsRestockNeeded(int quantity, int reorderLevel)
    {
        return quantity <= reorderLevel;
    }
}
