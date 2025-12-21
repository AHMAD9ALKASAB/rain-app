using System.Threading.Tasks;

namespace Rain.Web.Services
{
    public interface INotificationService
    {
        Task OrderCreatedAsync(int orderId);
        Task OrderAcceptedAsync(int orderId);
        Task OrderShippedAsync(int orderId);
        Task OrderDeliveredAsync(int orderId);
        Task PaymentSucceededAsync(int orderId, decimal amount, string currency);
        Task PaymentFailedAsync(int orderId);
        Task PaymentRefundedAsync(int orderId, decimal amount, string currency);
    }
}
