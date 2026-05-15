namespace MarketHub.Api.Models;

public record Order(Guid Id, Guid BuyerId, List<OrderItem> Items, decimal Total, string Status, DateTimeOffset CreatedAt);
