using MarketHub.Api.Models;

namespace MarketHub.Api.Contracts;

public record RegisterRequest(string Username, string Email, string Password, Role Role);
public record LoginRequest(string Identifier, string Password);
public record AuthResult(bool Success, string Message, User? User);
public record UserDto(Guid Id, string Username, string Email, Role Role)
{
    public static UserDto From(User user) => new(user.Id, user.Username, user.Email, user.Role);
}
