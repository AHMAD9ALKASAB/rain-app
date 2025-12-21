using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using Rain.Domain.Enums;
using PaymentMethodEnum = Rain.Domain.Enums.PaymentMethod;

namespace Rain.Infrastructure.Payments
{
    public class StripePaymentProvider : IPaymentProvider
    {
        private readonly string _apiKey;
        public string Name => "Stripe";

        public StripePaymentProvider(IConfiguration config)
        {
            _apiKey = config["Stripe:ApiKey"] ?? string.Empty;
        }

        public async Task<PaymentResult> CreatePaymentAsync(PaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return new PaymentResult { Success = false, Error = "Stripe API key not configured" };
            }
            StripeConfiguration.ApiKey = _apiKey;

            var unitAmount = (long)(request.Amount * 100m); // assumes 2 decimal currencies like KWD/SAR
            var sessionOptions = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = request.SuccessUrl + (request.SuccessUrl.Contains("?") ? "&" : "?") + "ref={CHECKOUT_SESSION_ID}",
                CancelUrl = request.CancelUrl,
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        {"orderId", request.OrderId.ToString()},
                        {"method", request.Method.ToString()}
                    }
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = request.Currency.ToLower(),
                            UnitAmount = unitAmount,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Order #{request.OrderId}"
                            }
                        }
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    {"orderId", request.OrderId.ToString()},
                    {"method", request.Method.ToString()}
                }
            };

            // Payment methods mapping
            var methods = new List<string>();
            switch (request.Method)
            {
                case PaymentMethodEnum.Card:
                case PaymentMethodEnum.ApplePay:
                    methods.Add("card");
                    break;
                case PaymentMethodEnum.KNET:
                    methods.Add("knet"); // Requires Stripe account capability; will fallback if unsupported
                    break;
                case PaymentMethodEnum.Mada:
                    methods.Add("card"); // Mada routes through card rails on Stripe
                    break;
                case PaymentMethodEnum.BankTransfer:
                    methods.Add("customer_balance"); // example; requires configuration
                    break;
            }
            if (methods.Count > 0)
            {
                sessionOptions.PaymentMethodTypes = methods;
            }

            var service = new SessionService();
            var session = await service.CreateAsync(sessionOptions);

            return new PaymentResult
            {
                Success = true,
                RedirectUrl = session.Url,
                ProviderReference = session.Id
            };
        }

        public Task<PaymentStatus> VerifyAsync(string providerReference)
        {
            // With Stripe Checkout, final confirmation is best handled via Webhook
            return Task.FromResult(PaymentStatus.Captured);
        }
    }
}
