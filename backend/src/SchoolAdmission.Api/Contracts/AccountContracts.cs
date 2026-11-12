namespace SchoolAdmission.Api.Contracts;

public record RequestOtpRequest(string? Phone);
public record VerifyOtpRequest(string? Phone, string? Code);
public record StaffLoginRequest(string? Email, string? Password);
public record MeResponse(string Kind, Guid Id, string? Role);

public record AddStudentRequest(string? BirthRegistrationNumber, string? FullName, DateOnly? DateOfBirth, string? Gender);
public record StudentResponse(Guid Id, string BirthRegistrationNumber, string FullName, DateOnly DateOfBirth, string Gender);
