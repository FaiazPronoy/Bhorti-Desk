namespace SchoolAdmission.Api.Domain;

public static class SeatHoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";

    // A row keeps saying HELD after its time is up, because nothing rewrites it at that
    // second. Every read goes through this expression, so an old hold is always reported
    // as EXPIRED. This is why no background job is needed. "h" is the seat_holds alias.
    public const string EffectiveStatusSql =
        "case when h.status = 'HELD' and h.expires_at <= clock_timestamp() then 'EXPIRED' else h.status end";
}
