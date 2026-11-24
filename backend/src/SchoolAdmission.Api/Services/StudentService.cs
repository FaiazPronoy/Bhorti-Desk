using Dapper;
using Npgsql;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public class StudentService(AdmissionDatabase database, ILogger<StudentService> logger)
{
    private static readonly string[] Genders = ["MALE", "FEMALE", "OTHER"];

    public async Task<List<StudentResponse>> ListForGuardianAsync(Guid guardianId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<StudentRow>("""
            select id, birth_registration_number, full_name, date_of_birth, gender
              from students where guardian_id = @guardianId order by created_at
            """, new { guardianId });
        return rows.Select(row => row.ToResponse()).ToList();
    }

    public async Task<StudentResponse> AddAsync(Guid guardianId, AddStudentRequest? request)
    {
        request ??= new AddStudentRequest(null, null, null, null);
        var errors = new Dictionary<string, string>();

        var brn = request.BirthRegistrationNumber?.Trim() ?? "";
        var name = (request.FullName ?? "").Trim();
        var gender = request.Gender?.Trim().ToUpperInvariant() ?? "";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (brn.Length != 17 || !brn.All(char.IsAsciiDigit))
        {
            errors["birthRegistrationNumber"] = "The birth registration number must be exactly 17 digits.";
        }
        if (name.Length < 2 || name.Length > 100 || name.Any(char.IsControl))
        {
            errors["fullName"] = "Write the student's full name, 2 to 100 characters.";
        }
        if (request.DateOfBirth is not { } dob)
        {
            errors["dateOfBirth"] = "Enter the date of birth.";
        }
        else if (dob > today.AddYears(-3) || dob < today.AddYears(-20))
        {
            errors["dateOfBirth"] = "A school student is between 3 and 20 years old.";
        }
        // A Bangladeshi birth registration number starts with the year of birth. A number
        // that does not match the date of birth is a typo or a borrowed number.
        else if (!errors.ContainsKey("birthRegistrationNumber") && brn[..4] != dob.Year.ToString())
        {
            errors["birthRegistrationNumber"] = "The first four digits must be the year of birth.";
        }
        if (!Genders.Contains(gender))
        {
            errors["gender"] = "Choose the student's gender.";
        }

        if (errors.Count > 0)
        {
            throw new FieldValidationException(errors);
        }

        await using var connection = await database.OpenConnectionAsync();
        var owner = await connection.QuerySingleOrDefaultAsync<Guid?>(
            "select guardian_id from students where birth_registration_number = @brn", new { brn });
        if (owner is not null && owner != guardianId)
        {
            logger.LogWarning("A guardian tried to add a birth registration number that belongs to another account");
            throw AdmissionRuleException.StudentTaken();
        }
        if (owner == guardianId)
        {
            throw new FieldValidationException(new() { ["birthRegistrationNumber"] = "This student is already on your account." });
        }

        try
        {
            var row = await connection.QuerySingleAsync<StudentRow>("""
                insert into students (id, guardian_id, birth_registration_number, full_name, date_of_birth, gender)
                values (@id, @guardianId, @brn, @name, @dob, @gender)
                returning id, birth_registration_number, full_name, date_of_birth, gender
                """, new { id = Guid.CreateVersion7(), guardianId, brn, name, dob = request.DateOfBirth!.Value, gender });
            return row.ToResponse();
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Another family added the same number a moment ago.
            throw AdmissionRuleException.StudentTaken();
        }
    }

    // True when this birth registration number is a student on this guardian's account.
    public async Task<bool> GuardianOwnsAsync(Guid guardianId, string birthRegistrationNumber)
    {
        await using var connection = await database.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<bool>(
            "select exists (select 1 from students where guardian_id = @guardianId and birth_registration_number = @brn)",
            new { guardianId, brn = birthRegistrationNumber });
    }

    // Npgsql reads a date column as DateTime by default; the API sends DateOnly.
    private class StudentRow
    {
        public Guid Id { get; set; }
        public string BirthRegistrationNumber { get; set; } = "";
        public string FullName { get; set; } = "";
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = "";

        public StudentResponse ToResponse() =>
            new(Id, BirthRegistrationNumber, FullName, DateOnly.FromDateTime(DateOfBirth), Gender);
    }
}
