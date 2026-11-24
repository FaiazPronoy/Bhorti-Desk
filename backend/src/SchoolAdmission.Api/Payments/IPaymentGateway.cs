namespace SchoolAdmission.Api.Payments;

// The strategy for talking to a payment gateway. The app only ever has the sandbox one;
// a real gateway would be a second class with the same two jobs.
public interface IPaymentGateway
{
    string Name { get; }

    // Where to send the guardian to pay. For a real gateway this is its hosted page.
    string HostedPageUrl(Guid paymentId);

    // True when the callback body really came from this gateway.
    bool IsGenuine(string body, string? signature);
}
