using System;
using Rain.Domain.Enums;

namespace Rain.Domain.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Provider { get; set; }
        public string? ProviderReference { get; set; }
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
