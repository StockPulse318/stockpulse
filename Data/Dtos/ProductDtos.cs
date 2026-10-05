using System.Text.Json.Serialization;

namespace WarehouseInventory.Data.Dtos;

public sealed class ProductDto
{
    [JsonPropertyName("productID")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("reorderLevel")]
    public int ReorderLevel { get; set; }

    [JsonPropertyName("isLowStock")]
    public bool IsLowStock { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
}

public sealed class CreateProductRequestDto
{
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("reorderLevel")]
    public int ReorderLevel { get; set; }
}

public sealed class UpdateProductRequestDto
{
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("reorderLevel")]
    public int ReorderLevel { get; set; }
}

public sealed class StockMovementRequestDto
{
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
