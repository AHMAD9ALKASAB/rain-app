using System.Threading.Tasks;
using Rain.Domain.Entities;
using Rain.Domain.Enums;

namespace Rain.Infrastructure.Payments
{
    public class PaymentRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "KWD";
        public PaymentMethod Method { get; set; }
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? RedirectUrl { get; set; }
        public string? ProviderReference { get; set; }
        public string? Error { get; set; }
    }

    public interface IPaymentProvider
    {
        string Name { get; }
        Task<PaymentResult> CreatePaymentAsync(PaymentRequest request);
        Task<PaymentStatus> VerifyAsync(string providerReference);
    }
}
