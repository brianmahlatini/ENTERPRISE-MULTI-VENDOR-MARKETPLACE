namespace MarketHub.Api.Contracts;

public record CartItemRequest(Guid ProductId, int Quantity);
public record CartLine(Guid ProductId, string Title, string ImageUrl, decimal Price, int Quantity, decimal LineTotal);
public record CartDto(List<CartLine> Items, decimal Total);
