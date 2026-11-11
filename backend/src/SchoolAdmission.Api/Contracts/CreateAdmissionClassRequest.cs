namespace SchoolAdmission.Api.Contracts;

// Both fields are nullable so a missing field reaches our own checks and gets a clear
// message, instead of quietly becoming "" or 0.
public record CreateAdmissionClassRequest(string? Name, int? TotalSeats);
