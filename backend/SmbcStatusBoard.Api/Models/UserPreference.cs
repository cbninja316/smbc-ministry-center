namespace SmbcStatusBoard.Api.Models;

public class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
