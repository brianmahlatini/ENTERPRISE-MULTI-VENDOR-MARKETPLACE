namespace MarketHub.Api.Models;

public record OrderItem(Guid ProductId, Guid SellerId, string Title, int Quantity, decimal UnitPrice);
