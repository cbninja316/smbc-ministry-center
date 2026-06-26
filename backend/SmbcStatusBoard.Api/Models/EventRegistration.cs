namespace SmbcStatusBoard.Api.Models;

public class EventRegistration
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    // Either UserId OR ChildId is set — not both
    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? ChildId { get; set; }
    public Child? Child { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public string? StripePaymentIntentId { get; set; }
    public decimal? AmountPaid { get; set; }
}
