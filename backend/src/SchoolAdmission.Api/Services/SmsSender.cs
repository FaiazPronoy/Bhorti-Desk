namespace SchoolAdmission.Api.Services;

// Sends a text message. A real provider (for example an SSL Wireless or Twilio client)
// implements this; until one is set up, nothing leaves the server.
public interface ISmsSender
{
    Task SendAsync(string phone, string message);
}

public class NoSmsSender : ISmsSender
{
    public Task SendAsync(string phone, string message) => Task.CompletedTask;
}
