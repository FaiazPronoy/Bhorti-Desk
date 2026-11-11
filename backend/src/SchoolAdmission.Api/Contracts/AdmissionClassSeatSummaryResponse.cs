namespace SchoolAdmission.Api.Contracts;

public record AdmissionClassSeatSummaryResponse(
    Guid Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);
