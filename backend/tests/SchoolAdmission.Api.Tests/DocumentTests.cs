using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class DocumentTests
{
    private static readonly byte[] TinyPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    private static readonly byte[] FakeExe = "MZ this is not a picture"u8.ToArray();
    private static readonly byte[] SmallPdf = "%PDF-1.4\n%fake test certificate\n"u8.ToArray();

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string holdId, string kind, byte[] data, string student, string claimedType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(claimedType);
        content.Add(file, "file", "picture.png");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/applications/{holdId}/documents/{kind}") { Content = content };
        request.Headers.Add("X-Student-Id", student);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> ReadAsync(HttpClient client, string holdId, string kind, string student)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/applications/{holdId}/documents/{kind}");
        request.Headers.Add("X-Student-Id", student);
        return client.SendAsync(request);
    }

    private static async Task<(AdmissionApiFactory Api, HttpClient Client, string HoldId, string Student)> HoldAsync()
    {
        var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var student = ApiCalls.NewStudentId();
        return (api, client, await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student), student);
    }

    [Fact]
    public async Task The_owner_uploads_a_photo_and_reads_it_back_privately()
    {
        var (api, client, holdId, student) = await HoldAsync();
        await using var _ = api;

        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, holdId, "photo", TinyPng, student)).StatusCode);

        var read = await ReadAsync(client, holdId, "photo", student);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("image/png", read.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", read.Headers.CacheControl!.ToString());
        Assert.Equal(TinyPng, await read.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task A_renamed_program_is_refused_whatever_type_the_browser_claims()
    {
        var (api, client, holdId, student) = await HoldAsync();
        await using var _ = api;

        var response = await UploadAsync(client, holdId, "photo", FakeExe, student, "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("document.error.photoType", body.GetProperty("fieldErrors").GetProperty("file").GetString());
    }

    [Fact]
    public async Task A_pdf_is_fine_for_the_certificate_but_not_for_the_photo()
    {
        var (api, client, holdId, student) = await HoldAsync();
        await using var _ = api;

        Assert.Equal(HttpStatusCode.NoContent, (await UploadAsync(client, holdId, "certificate", SmallPdf, student, "application/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(client, holdId, "photo", SmallPdf, student, "application/pdf")).StatusCode);
    }

    [Fact]
    public async Task A_photo_over_the_size_limit_is_refused_without_a_500()
    {
        var (api, client, holdId, student) = await HoldAsync();
        await using var _ = api;
        var big = new byte[400 * 1024];
        TinyPng.CopyTo(big, 0);

        var response = await UploadAsync(client, holdId, "photo", big, student);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nobody_else_can_read_the_document_and_staff_reading_is_audited()
    {
        var (api, client, holdId, student) = await HoldAsync();
        await using var _ = api;
        await UploadAsync(client, holdId, "certificate", SmallPdf, student, "application/pdf");

        Assert.Equal(HttpStatusCode.Forbidden, (await ReadAsync(client, holdId, "certificate", ApiCalls.NewStudentId())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/office/applications/{holdId}/documents/certificate")).StatusCode);

        var staff = api.CreateSignedInClient();
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/office/applications/{holdId}/documents/certificate")).StatusCode);

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from audit_log where action = 'DOCUMENT_VIEWED' and target_id = @id", connection);
        command.Parameters.AddWithValue("id", holdId);
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task The_application_lists_what_was_uploaded()
    {
        var (api, client, holdId, student) = await HoldAsync();
        await using var _ = api;
        await UploadAsync(client, holdId, "photo", TinyPng, student);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/applications/{holdId}");
        request.Headers.Add("X-Student-Id", student);
        var application = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();

        var document = Assert.Single(application.GetProperty("documents").EnumerateArray());
        Assert.Equal("PHOTO", document.GetProperty("kind").GetString());
    }
}
