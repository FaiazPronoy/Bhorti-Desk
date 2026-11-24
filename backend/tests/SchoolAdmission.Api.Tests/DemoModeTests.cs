using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Services;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class DemoModeTests
{
    [Fact]
    public async Task The_sign_in_code_comes_back_only_in_demo_mode()
    {
        await using var normal = new AdmissionApiFactory();
        var plain = await normal.CreateAnonymousClient().PostAsJsonAsync("/auth/otp/request", new { phone = "01712345678" });
        Assert.Equal(HttpStatusCode.Accepted, plain.StatusCode);
        Assert.Empty(await plain.Content.ReadAsByteArrayAsync());

        await using var demo = new AdmissionApiFactory(demoMode: true);
        var shown = await demo.CreateAnonymousClient().PostAsJsonAsync("/auth/otp/request", new { phone = "01712345678" });
        Assert.Matches("^[0-9]{6}$", (await shown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demoCode").GetString()!);
    }

    [Fact]
    public async Task The_daily_reset_wipes_the_data_once_per_day()
    {
        await using var api = new AdmissionApiFactory(demoMode: true);
        var client = api.CreateAnonymousClient();
        await ApiCalls.CreateClassAsync(client, 3);
        var reset = api.Services.GetRequiredService<DemoReset>();

        // A day far ahead, so the reset the app itself runs at start (for today) never
        // counts as this one.
        var day = new DateOnly(2099, 1, 1);
        Assert.True(await reset.ResetIfNewDayAsync(day));
        Assert.False(await reset.ResetIfNewDayAsync(day));
        Assert.Equal(0, (await ApiCalls.GetJsonAsync(client, "/classes")).GetArrayLength());
    }
}
