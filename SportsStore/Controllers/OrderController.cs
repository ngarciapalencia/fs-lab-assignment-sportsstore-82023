using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SportsStore.Models;
using SportsStore.Services.Payments;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SportsStore.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderRepository repository;
        private readonly Cart cart;
        private readonly ILogger<OrderController> _logger;
        private readonly IPaymentService _paymentService;

        public OrderController(
            IOrderRepository repoService,
            Cart cartService,
            IPaymentService paymentService,
            ILogger<OrderController> logger)
        {
            repository = repoService;
            cart = cartService;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet]
        public ViewResult Checkout() => View(new Order());

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            _logger.LogInformation("Checkout POST started. LinesInCart={LinesCount}", cart.Lines.Count());

            if (cart.Lines.Count() == 0)
            {
                _logger.LogWarning("Checkout blocked: cart is empty");
                ModelState.AddModelError("", "Sorry, your cart is empty!");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Checkout failed validation. ErrorCount={ErrorCount}", ModelState.ErrorCount);
                return View();
            }

            try
            {
                // Guardamos pedido "Pending" antes de pagar (para tener OrderId)
                order.Lines = cart.Lines.ToArray();
                order.PaymentStatus = "Pending";

                repository.SaveOrder(order);

                _logger.LogInformation("Pending order saved. OrderId={OrderId}", order.OrderID);

                var successBaseUrl = Url.Action(
                    action: nameof(PaymentSuccess),
                    controller: "Order",
                    values: new { orderId = order.OrderID },
                    protocol: "http")!;

                var successUrl = $"{successBaseUrl}?session_id={{CHECKOUT_SESSION_ID}}";

                var cancelUrl = Url.Action(
                    action: nameof(PaymentCancel),
                    controller: "Order",
                    values: new { orderId = order.OrderID },
                    protocol: "http")!;

                var (sessionId, sessionUrl) = await _paymentService.CreateCheckoutSessionAsync(order, cart, successUrl, cancelUrl);

                order.StripeSessionId = sessionId;
                repository.SaveOrder(order);

                _logger.LogInformation("Redirecting to Stripe. OrderId={OrderId} SessionId={SessionId}", order.OrderID, sessionId);

                return Redirect(sessionUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during checkout/payment session creation");
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(int orderId, string session_id)
        {
            _logger.LogInformation("Stripe success callback. OrderId={OrderId} SessionId={SessionId}", orderId, session_id);

            var order = repository.Orders.FirstOrDefault(o => o.OrderID == orderId);
            if (order == null)
            {
                _logger.LogWarning("PaymentSuccess: order not found. OrderId={OrderId}", orderId);
                return RedirectToPage("/Completed");
            }

            try
            {
                var (paid, paymentIntentId) = await _paymentService.VerifyCheckoutSessionAsync(session_id);

                if (!paid)
                {
                    _logger.LogWarning("Payment not paid according to Stripe. OrderId={OrderId} SessionId={SessionId}", orderId, session_id);
                    order.PaymentStatus = "Failed";
                    repository.SaveOrder(order);
                    return RedirectToAction(nameof(PaymentCancel), new { orderId });
                }

                order.PaymentStatus = "Paid";
                order.StripeSessionId = session_id;
                order.StripePaymentIntentId = paymentIntentId;
                order.PaidAtUtc = DateTime.UtcNow;

                repository.SaveOrder(order);

                _logger.LogInformation(
                    ">>> PaymentSuccess reached. SessionId={SessionId} CartLinesBefore={Lines}",
                    HttpContext.Session.Id,
                    cart.Lines.Count()
                );

                cart.Clear();                 // llama al override de SessionCart (Remove("Cart") / SetJson)
                HttpContext.Session.Remove("Cart"); // refuerzo: borra la clave exacta

                _logger.LogInformation(
                    ">>> Cart cleared. SessionId={SessionId} CartLinesAfter={Lines} Keys={Keys}",
                    HttpContext.Session.Id,
                    cart.Lines.Count(),
                    string.Join(",", HttpContext.Session.Keys)
                );

                return RedirectToPage("/Completed", new { orderId = orderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Stripe session. OrderId={OrderId} SessionId={SessionId}", orderId, session_id);
                throw;
            }
        }

        [HttpGet]
        public IActionResult PaymentCancel(int orderId)
        {
            _logger.LogWarning("Stripe payment cancelled. OrderId={OrderId}", orderId);

            var order = repository.Orders.FirstOrDefault(o => o.OrderID == orderId);
            if (order != null)
            {
                order.PaymentStatus = "Cancelled";
                repository.SaveOrder(order);
            }

            return RedirectToAction(nameof(Checkout));
        }
    }
}