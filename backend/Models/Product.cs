namespace MarketHub.Api.Models;

public record Product(Guid Id, Guid SellerId, string Title, string Category, decimal Price, int Inventory, string ImageUrl, string Description, bool Active, DateTimeOffset CreatedAt);
