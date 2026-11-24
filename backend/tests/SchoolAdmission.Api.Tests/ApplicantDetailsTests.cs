using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class ApplicantDetailsTests
{
    private const string Student = "20200123456789012";

    private static object GoodForm(string birth = "2020-03-14") => new
    {
        studentNameBn = "আয়েশা রহমান",
        studentNameEn = "Ayesha Rahman",
        dateOfBirth = birth,
        guardianName = "Karim Rahman",
        guardianPhone = "+880 1712-345678",
        previousSchool = "",
    };

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string holdId, object form, string student = Student)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/applications/{holdId}/applicant") { Content = JsonContent.Create(form) };
        request.Headers.Add("X-Student-Id", student);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> GetApplicationAsync(HttpClient client, string holdId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/applications/{holdId}");
        request.Headers.Add("X-Student-Id", Student);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task The_owner_saves_details_and_gets_a_human_serial()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, Student);

        var saved = await PutAsync(client, holdId, GoodForm());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var application = await GetApplicationAsync(client, holdId);
        Assert.Matches(@"^BD-\d{4}-\d{7}$", application.GetProperty("serial").GetString()!);
        Assert.Equal("01712345678", application.GetProperty("applicant").GetProperty("guardianPhone").GetString());
    }

    [Fact]
    public async Task Another_student_cannot_read_or_write_the_details()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, Student);

        var write = await PutAsync(client, holdId, GoodForm(), ApiCalls.NewStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("HOLD_NOT_OWNED", await ApiCalls.ErrorCodeAsync(write));
    }

    [Fact]
    public async Task The_age_rule_is_checked_on_the_cutoff_date()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var rules = await staff.PutAsJsonAsync($"/office/classes/{classId}/rules", new { minAge = 7, maxAge = 8, ageCutoffDate = "2027-01-01", feeTaka = 500 });
        Assert.Equal(HttpStatusCode.OK, rules.StatusCode);
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, Student);

        var response = await PutAsync(client, holdId, GoodForm());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fieldErrors");
        Assert.Equal("applicant.error.tooYoung", fields.GetProperty("dateOfBirth").GetString());
    }

    [Theory]
    [InlineData("2020-01-01", "2027-01-01", 7)]
    [InlineData("2020-12-31", "2027-01-01", 6)]
    [InlineData("2020-01-02", "2027-01-01", 6)]
    [InlineData("2020-02-29", "2027-02-28", 6)]
    public void Age_counts_completed_years(string birth, string on, int expected) =>
        Assert.Equal(expected, ApplicantRules.AgeOn(DateOnly.Parse(birth), DateOnly.Parse(on)));

    [Theory]
    [InlineData("studentNameBn", "Ayesha")]
    [InlineData("studentNameEn", "আয়েশা")]
    [InlineData("guardianPhone", "01212345678")]
    [InlineData("guardianPhone", "0171234567")]
    public void A_bad_field_is_named(string field, string value)
    {
        var request = new ApplicantRequest(
            field == "studentNameBn" ? value : "আয়েশা রহমান",
            field == "studentNameEn" ? value : "Ayesha Rahman",
            new DateOnly(2020, 3, 14), "Karim Rahman",
            field == "guardianPhone" ? value : "01712345678", null);

        var (_, errors) = ApplicantRules.Check(request, Student, null, new DateOnly(2026, 11, 24));

        Assert.True(errors.ContainsKey(field));
    }

    [Fact]
    public void The_birth_year_must_match_the_number()
    {
        var request = new ApplicantRequest("আয়েশা রহমান", "Ayesha Rahman", new DateOnly(2019, 3, 14), "Karim Rahman", "01712345678", null);

        var (_, errors) = ApplicantRules.Check(request, Student, null, new DateOnly(2026, 11, 24));

        Assert.Equal("applicant.error.birthYear", errors["dateOfBirth"]);
    }

    [Fact]
    public void A_class_rule_for_the_2027_policy_accepts_only_children_born_in_2020()
    {
        var rules = new ClassRulesResponse(Guid.NewGuid(), 6, 6, new DateOnly(2027, 1, 1), 0);
        var form = (int year) => new ApplicantRequest("আয়েশা রহমান", "Ayesha Rahman", new DateOnly(year, 6, 1), "Karim Rahman", "01712345678", null);

        Assert.Empty(ApplicantRules.Check(form(2020), "20200123456789012", rules, new DateOnly(2026, 11, 24)).Errors);
        Assert.Equal("applicant.error.tooYoung", ApplicantRules.Check(form(2021), "20210123456789012", rules, new DateOnly(2026, 11, 24)).Errors["dateOfBirth"]);
        Assert.Equal("applicant.error.tooOld", ApplicantRules.Check(form(2019), "20190123456789012", rules, new DateOnly(2026, 11, 24)).Errors["dateOfBirth"]);
    }
}
