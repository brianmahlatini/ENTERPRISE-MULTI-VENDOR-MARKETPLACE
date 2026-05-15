using MarketHub.Api.Models;

namespace MarketHub.Api.Contracts;

public record ProductUpsertRequest(string Title, string Category, decimal Price, int Inventory, string ImageUrl, string Description);
public record ProductDto(Guid Id, Guid SellerId, string Title, string Category, decimal Price, int Inventory, string ImageUrl, string Description, bool Active)
{
    public static ProductDto From(Product product) => new(product.Id, product.SellerId, product.Title, product.Category, product.Price, product.Inventory, product.ImageUrl, product.Description, product.Active);
}
