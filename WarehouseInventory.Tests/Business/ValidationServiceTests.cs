using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Validation;
using Xunit;

namespace WarehouseInventory.Tests.Business;

public class ValidationServiceTests
{
    private readonly ValidationService _service = new();

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-100")]
    [InlineData("abc")]
    [InlineData("3.14")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateStockAmount_RejectsInvalidAmounts(string? input)
    {
        Assert.Throws<ValidationException>(() => _service.ValidateStockAmount(input));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("25", 25)]
    [InlineData(" 100 ", 100)]
    public void ValidateStockAmount_AcceptsValidPositiveIntegers(string input, int expected)
    {
        int result = _service.ValidateStockAmount(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateStockOut_RejectsWhenRequestedAmountExceedsAvailable()
    {
        var ex = Assert.Throws<ValidationException>(() => _service.ValidateStockOut(currentQuantity: 10, requestedAmount: 11));
        Assert.Contains("exceeds available inventory", ex.Message);
    }

    [Fact]
    public void ValidateStockOut_AcceptsWhenRequestedAmountIsWithinAvailable()
    {
        // Should not throw
        _service.ValidateStockOut(currentQuantity: 10, requestedAmount: 10);
        _service.ValidateStockOut(currentQuantity: 10, requestedAmount: 5);
        _service.ValidateStockOut(currentQuantity: 10, requestedAmount: 1);
    }

    [Theory]
    [InlineData(10, 10, true)]   // Quantity == ReorderLevel
    [InlineData(5, 10, true)]    // Quantity < ReorderLevel
    [InlineData(0, 10, true)]    // Out of stock
    [InlineData(11, 10, false)]  // Quantity > ReorderLevel
    [InlineData(100, 10, false)] // Well stocked
    public void LowStockRule_EvaluatesQuantityLessThanOrEqualToReorderLevel(int quantity, int reorderLevel, bool expectedRestockNeeded)
    {
        bool result = _service.IsRestockNeeded(quantity, reorderLevel);
        Assert.Equal(expectedRestockNeeded, result);

        var product = new Product
        {
            Quantity = quantity,
            ReorderLevel = reorderLevel
        };
        Assert.Equal(expectedRestockNeeded, product.IsLowStock);
    }

    [Fact]
    public void RoleBasedAction_AllowsWarehouseManagerForProductCrud()
    {
        var manager = new User("manager", "National Lead", UserRole.WarehouseManager);
        Assert.True(manager.CanManageProducts);

        // All should succeed without throwing
        _service.ValidateRoleAction(manager, "AddProduct");
        _service.ValidateRoleAction(manager, "EditProduct");
        _service.ValidateRoleAction(manager, "DeleteProduct");
    }

    [Fact]
    public void RoleBasedAction_ProhibitsClerkFromProductCrud()
    {
        var clerk = new User("clerk", "General Clerk", UserRole.Clerk);
        Assert.False(clerk.CanManageProducts);

        var exAdd = Assert.Throws<ValidationException>(() => _service.ValidateRoleAction(clerk, "AddProduct"));
        Assert.Contains("Only a Warehouse Manager", exAdd.Message);

        var exEdit = Assert.Throws<ValidationException>(() => _service.ValidateRoleAction(clerk, "EditProduct"));
        Assert.Contains("Only a Warehouse Manager", exEdit.Message);

        var exDelete = Assert.Throws<ValidationException>(() => _service.ValidateRoleAction(clerk, "DeleteProduct"));
        Assert.Contains("Only a Warehouse Manager", exDelete.Message);
    }

    [Theory]
    [InlineData("", "Building Supplies", 10.0, 5, 0)]      // Empty name
    [InlineData("Cement", "", 10.0, 5, 0)]                // Empty category
    [InlineData("Cement", "Supplies", -1.0, 5, 0)]         // Negative price
    [InlineData("Cement", "Supplies", 10.0, -1, 0)]        // Negative reorder level
    [InlineData("Cement", "Supplies", 10.0, 5, -5)]        // Negative quantity
    public void ValidateProduct_RejectsInvalidFields(string name, string category, decimal price, int reorder, int qty)
    {
        Assert.Throws<ValidationException>(() => _service.ValidateProduct(name, category, price, reorder, qty));
    }
}
