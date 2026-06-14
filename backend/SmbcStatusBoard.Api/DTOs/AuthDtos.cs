namespace SmbcStatusBoard.Api.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, string Username, string Role, string[] AllowedItemTypes);

public record SetPasswordRequest(string Token, string Password);

public record InviteUserRequest(string FirstName, string LastName, string Email, string Role, string[] AllowedItemTypes);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string Password);

public record ChangePasswordRequest(string NewPassword);

public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string ConfirmPassword, string? BirthDate = null);

public record UpdateUserRoleRequest(string Role);

public record UpdateBirthDateRequest(string? BirthDate);
