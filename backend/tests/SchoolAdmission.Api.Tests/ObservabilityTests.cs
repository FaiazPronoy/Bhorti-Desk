using System.Net;
using System.Text.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class ObservabilityTests
{
    [Fact]
    public async Task Health_ready_and_metrics_answer()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ready")).StatusCode);
        var metrics = await ApiCalls.GetJsonAsync(client, "/metrics");
        Assert.True(metrics.TryGetProperty("holds_created_total", out _));
    }

    [Fact]
    public async Task Every_answer_carries_a_request_id_and_keeps_a_plain_one_from_the_caller()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();

        var plain = await client.GetAsync("/classes");
        Assert.Matches("^[0-9a-f]{32}$", plain.Headers.GetValues("X-Request-Id").Single());

        var request = new HttpRequestMessage(HttpMethod.Get, "/classes");
        request.Headers.Add("X-Request-Id", "load-test-0001");
        Assert.Equal("load-test-0001", (await client.SendAsync(request)).Headers.GetValues("X-Request-Id").Single());

        var odd = new HttpRequestMessage(HttpMethod.Get, "/classes");
        odd.Headers.Add("X-Request-Id", "<script>");
        Assert.NotEqual("<script>", (await client.SendAsync(odd)).Headers.GetValues("X-Request-Id").Single());
    }

    [Fact]
    public async Task The_api_description_lists_every_endpoint_and_marks_the_eight_frozen_ones()
    {
        await using var api = new AdmissionApiFactory();
        var document = await ApiCalls.GetJsonAsync(api.CreateAnonymousClient(), "/openapi/v1.json");
        var paths = document.GetProperty("paths");

        var frozen = 0;
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.Value.TryGetProperty("description", out var description)
                    && description.GetString()!.StartsWith("Frozen contract")) frozen++;
            }
        }
        Assert.Equal(8, frozen);
        Assert.True(paths.TryGetProperty("/applications/{holdId}/slip", out _));
        Assert.Equal(JsonValueKind.Object, paths.GetProperty("/office/reports").ValueKind);
    }
}
