using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.DTOs;

public record ItemRequest(
    ItemType Type,
    string Name,
    DateTime? EventDate,
    DateTime? EventEndDate,
    string? Ministry,
    Urgency? Urgency,
    string RequestedBy,
    string Description,
    string? Email = null,
    BenevolenceData? BenevolenceData = null,
    ChurchEventData? ChurchEventData = null
);

public class ChurchEventData
{
    public string? EventTime { get; set; }  // legacy: kept for backward compat
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Location { get; set; }
    public decimal? Cost { get; set; }
    public bool RegistrationRequired { get; set; }
    public bool PromoteFacebook { get; set; }
    public bool PromoteFacebookEvent { get; set; }
    public bool PromoteText { get; set; }
    public bool PromoteEmail { get; set; }
    public bool ShowOnHomePage { get; set; }
    public string? HomeLinkName { get; set; }
    public string? HomeLinkUrl { get; set; }
    public bool IsOneAccordRegistration { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
}

public class BenevolenceData
{
    // Applicant section
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public decimal? AmountRequested { get; set; }
    public string? DateNeeded { get; set; }
    public string? ApplicantSignature { get; set; }
    public string? SignatureDate { get; set; }

    // Committee section
    public string? DateReviewed { get; set; }
    public string? RelationshipToChurch { get; set; }
    public string? Determination { get; set; } // ApprovedFull | ApprovedPart | NotApproved
    public string? DenialReason { get; set; }

    // If approved
    public string? AssistanceProvidedDescription { get; set; }
    public decimal? AssistanceCost { get; set; }
    public string? MethodOfAssistance { get; set; } // DirectPayment | CashGrant | Other
    public string? MethodOtherDescription { get; set; }
    public string? PayableTo { get; set; }
    public string? DateAssistanceProvided { get; set; }
}

public record EventRegisterRequest(int[]? UserIds, int[]? ChildIds);

public record ItemStatusUpdate(ItemStatus Status, int SortOrder);

public record ItemReorderRequest(int Id, ItemStatus Status, int SortOrder, string? CompletionNote = null);

public record ReceiptResponse(
    int Id,
    DateTime Date,
    string Ministry,
    string Description,
    decimal Amount,
    string SubmittedBy,
    bool IsDone
);
