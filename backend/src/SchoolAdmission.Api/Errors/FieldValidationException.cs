namespace SchoolAdmission.Api.Errors;

// Bad form fields. Becomes 400 INVALID_REQUEST with a message under each field.
public class FieldValidationException(Dictionary<string, string> fieldErrors) : Exception("Some information is not correct.")
{
    public Dictionary<string, string> FieldErrors { get; } = fieldErrors;
}
