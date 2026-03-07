using Stripe;
using Stripe.Checkout;
using SportsStore.Models;

namespace SportsStore.Services.Payments;

public class StripePaymentService : IPaymentService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IConfiguration config, ILogger<StripePaymentService> logger)
    {
        _config = config;
        _logger = logger;

        // API key desde config (User Secrets / env vars)
        var secretKey = _config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe SecretKey is missing. Set Stripe:SecretKey in User Secrets or environment variables.");
        }

        StripeConfiguration.ApiKey = secretKey;
    }

    public async Task<(string SessionId, string SessionUrl)> CreateCheckoutSessionAsync(
        Order order,
        Cart cart,
        string successUrl,
        string cancelUrl)
    {
        // Moneda configurable (por defecto EUR)
        var currency = _config["Stripe:Currency"] ?? "eur";

        var lineItems = cart.Lines.Select(line =>
        {
            var unitAmount = (long)Math.Round(line.Product.Price * 100m, MidpointRounding.AwayFromZero);

            return new SessionLineItemOptions
            {
                Quantity = line.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = currency,
                    UnitAmount = unitAmount,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = line.Product.Name
                    }
                }
            };
        }).ToList();

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl, // incluirá orderId + session_id
            CancelUrl = cancelUrl,
            LineItems = lineItems,

            // metadata útil para debugging/auditoría
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.OrderID.ToString(),
                ["customerName"] = order.Name ?? ""
            }
        };

        _logger.LogInformation("Creating Stripe Checkout Session for OrderId={OrderId} Lines={Lines} Total={Total}",
            order.OrderID, cart.Lines.Count, cart.ComputeTotalValue());

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        _logger.LogInformation("Stripe session created. OrderId={OrderId} SessionId={SessionId}", order.OrderID, session.Id);

        return (session.Id, session.Url);
    }

    public async Task<(bool Paid, string? PaymentIntentId)> VerifyCheckoutSessionAsync(string sessionId)
    {
        var service = new SessionService();
        var session = await service.GetAsync(sessionId);

        // Stripe Checkout: session.PaymentStatus suele ser "paid" si pagó correctamente
        var paid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);

        return (paid, session.PaymentIntentId);
    }
}