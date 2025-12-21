namespace Rain.Domain.Enums
{
    public enum PaymentStatus
    {
        Pending = 0,
        Authorized = 1,
        Captured = 2,
        Refunded = 3,
        Failed = 4,
        Cancelled = 5
    }
}
