namespace SchoolAdmission.Api.Services;

// Simple counters since this process started, read by GET /metrics. Per process, so with
// two backends each reports its own numbers.
public static class SeasonMetrics
{
    private static long holdsCreated;
    private static long seatConflicts;
    private static long offersMade;
    private static long paymentCallbacks;

    public static void HoldCreated() => Interlocked.Increment(ref holdsCreated);
    public static void SeatConflict() => Interlocked.Increment(ref seatConflicts);
    public static void AddOffers(int count) => Interlocked.Add(ref offersMade, count);
    public static void PaymentCallback() => Interlocked.Increment(ref paymentCallbacks);

    public static Dictionary<string, long> Snapshot() => new()
    {
        ["holds_created_total"] = Interlocked.Read(ref holdsCreated),
        ["seat_conflicts_total"] = Interlocked.Read(ref seatConflicts),
        ["waitlist_offers_total"] = Interlocked.Read(ref offersMade),
        ["payment_callbacks_total"] = Interlocked.Read(ref paymentCallbacks),
    };
}
