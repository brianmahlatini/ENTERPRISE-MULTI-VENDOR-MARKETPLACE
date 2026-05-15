namespace MarketHub.Api.Models;

public record User(Guid Id, string Username, string Email, string PasswordHash, Role Role, DateTimeOffset CreatedAt);
