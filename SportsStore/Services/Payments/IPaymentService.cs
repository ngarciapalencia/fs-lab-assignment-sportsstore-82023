using SportsStore.Models;

namespace SportsStore.Services.Payments;

public interface IPaymentService
{
    Task<(string SessionId, string SessionUrl)> CreateCheckoutSessionAsync(
        Order order,
        Cart cart,
        string successUrl,
        string cancelUrl);

    Task<(bool Paid, string? PaymentIntentId)> VerifyCheckoutSessionAsync(string sessionId);
}