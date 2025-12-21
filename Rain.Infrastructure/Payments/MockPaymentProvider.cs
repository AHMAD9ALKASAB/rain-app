using System;
using System.Threading.Tasks;
using Rain.Domain.Enums;

namespace Rain.Infrastructure.Payments
{
    public class MockPaymentProvider : IPaymentProvider
    {
        public string Name => "Mock";

        public Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
        {
            var reference = Guid.NewGuid().ToString("N");
            var redirect = request.SuccessUrl + (request.SuccessUrl.Contains('?') ? "&" : "?") + "ref=" + reference;
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                RedirectUrl = redirect,
                ProviderReference = reference
            });
        }

        public Task<PaymentStatus> VerifyAsync(string providerReference)
        {
            return Task.FromResult(PaymentStatus.Captured);
        }
    }
}
