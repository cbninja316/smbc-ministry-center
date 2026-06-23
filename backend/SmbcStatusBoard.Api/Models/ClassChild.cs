namespace SmbcStatusBoard.Api.Models;

public class ClassChild
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
