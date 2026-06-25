namespace SmbcStatusBoard.Api.Models;

public class ChildLinkSuggestion
{
    public int Id { get; set; }
    public int RequestingUserId { get; set; }
    public User RequestingUser { get; set; } = null!;
    public int NewChildId { get; set; }
    public Child NewChild { get; set; } = null!;
    public int SuggestedChildId { get; set; }
    public Child SuggestedChild { get; set; } = null!;
    public bool IsResolved { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
