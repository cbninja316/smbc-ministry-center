namespace SmbcStatusBoard.Api.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, string Username, string Role, string[] AllowedItemTypes);

public record SetPasswordRequest(string Token, string Password);

public record InviteUserRequest(string Email, string Username, string Role, string[] AllowedItemTypes);
