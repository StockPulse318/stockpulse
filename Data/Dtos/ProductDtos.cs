using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarehouseInventory.Data.Dtos;

[JsonConverter(typeof(ProductDtoConverter))]
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

/// <summary>
/// Reads both the legacy camelCase contract (productID/productName/unitPrice/...)
/// and the new snake_case contract (id/name/category_id/unit_price/...),
/// including nested category objects like { "category": { "id": 3, "name": "Beverages" } }.
/// </summary>
public sealed class ProductDtoConverter : JsonConverter<ProductDto>
{
    public override ProductDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var el = doc.RootElement;
        if (el.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Expected product object but got {el.ValueKind}.");
        }

        var dto = new ProductDto
        {
            ProductId = GetInt(el, "productID", "productId", "product_id", "id", "ID", "Id"),
            ProductName = GetString(el, "productName", "name", "product_name", "title") ?? string.Empty,
            Category = ReadCategory(el),
            Quantity = GetInt(el, "quantity", "qty", "stock", "stockQuantity"),
            UnitPrice = GetDecimal(el, "unitPrice", "unit_price", "price", "unit_price_cents"),
            ReorderLevel = GetInt(el, "reorderLevel", "reorder_level", "reorderPoint", "reorder_point", "minStock", "min_stock"),
            IsLowStock = GetBool(el, "isLowStock", "is_low_stock", "lowStock", "is_lowstock"),
            Branch = GetString(el, "branch", "branchName", "branch_name", "assignedBranch")
        };
        return dto;
    }

    public override void Write(Utf8JsonWriter writer, ProductDto value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("productID", value.ProductId);
        writer.WriteString("productName", value.ProductName);
        writer.WriteString("category", value.Category);
        writer.WriteNumber("quantity", value.Quantity);
        writer.WriteNumber("unitPrice", value.UnitPrice);
        writer.WriteNumber("reorderLevel", value.ReorderLevel);
        writer.WriteBoolean("isLowStock", value.IsLowStock);
        if (value.Branch != null)
        {
            writer.WriteString("branch", value.Branch);
        }
        writer.WriteEndObject();
    }

    private static string ReadCategory(JsonElement el)
    {
        // Direct string forms: "category": "Beverages", "categoryName", "category_name"
        foreach (var name in new[] { "category", "categoryName", "category_name" })
        {
            if (el.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString() ?? string.Empty;
                }
                if (prop.ValueKind == JsonValueKind.Object)
                {
                    // Nested: { "category": { "id": 3, "name": "Beverages" } }
                    var nested = GetString(prop, "name", "categoryName", "category_name", "title");
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested!;
                    }
                    if (prop.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                    {
                        return idProp.GetRawText();
                    }
                }
            }
        }

        // category_id numeric fallback (kept as string so grouping/filtering still works)
        foreach (var name in new[] { "category_id", "categoryId", "categoryID" })
        {
            if (el.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                {
                    return prop.GetRawText();
                }
                if (prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            // case-insensitive fallback
            foreach (var p in el.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                {
                    return p.Value.GetString();
                }
            }
        }
        return null;
    }

    private static int GetInt(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProp(el, name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
                {
                    return i;
                }
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        return 0;
    }

    private static decimal GetDecimal(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProp(el, name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
                {
                    return d;
                }
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        return 0m;
    }

    private static bool GetBool(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProp(el, name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var parsed))
                {
                    return parsed;
                }
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
                {
                    return i != 0;
                }
            }
        }
        return false;
    }

    private static bool TryGetProp(JsonElement el, string name, out JsonElement prop)
    {
        if (el.TryGetProperty(name, out prop))
        {
            return true;
        }
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                prop = p.Value;
                return true;
            }
        }
        prop = default;
        return false;
    }
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
    // New backend contract uses "amount"; legacy used "quantity".
    [JsonPropertyName("amount")]
    public int Amount { get; set; }
}
