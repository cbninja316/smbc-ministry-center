using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmbcStatusBoard.Api.DTOs;

namespace SmbcStatusBoard.Api.Models;

public enum ItemType
{
    ChurchEvent,
    FacilityUse,
    Benevolence,
    Maintenance,
    SecretaryRequest
}

public enum ItemStatus { ToDo, InProgress, Done }

public enum Urgency { Low, Medium, Urgent }

public class Item
{
    public int Id { get; set; }
    public ItemType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? EventDate { get; set; }
    public string? Ministry { get; set; }
    public Urgency? Urgency { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Description { get; set; } = string.Empty;
    public ItemStatus Status { get; set; } = ItemStatus.ToDo;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; } = 0;

    [JsonIgnore]
    public string? BenevolenceData { get; set; }

    [NotMapped]
    public BenevolenceData? BenevolenceDetails =>
        BenevolenceData != null
            ? JsonSerializer.Deserialize<BenevolenceData>(BenevolenceData)
            : null;

    [JsonIgnore]
    public string? ChurchEventData { get; set; }

    [NotMapped]
    public ChurchEventData? ChurchEventDetails =>
        ChurchEventData != null
            ? JsonSerializer.Deserialize<ChurchEventData>(ChurchEventData)
            : null;
}
