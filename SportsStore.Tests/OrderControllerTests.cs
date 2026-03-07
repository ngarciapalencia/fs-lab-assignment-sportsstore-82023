using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using SportsStore.Controllers;
using SportsStore.Models;
using SportsStore.Services.Payments;
using System.Threading.Tasks;
using Xunit;

namespace SportsStore.Tests
{
    public class OrderControllerTests
    {
        private OrderController CreateController(IOrderRepository repo, Cart cart)
        {
            var paymentMock = new Mock<IPaymentService>();
            var loggerMock = new Mock<ILogger<OrderController>>();

            paymentMock
                .Setup(p => p.CreateCheckoutSessionAsync(
                    It.IsAny<Order>(),
                    It.IsAny<Cart>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(("sess_test_123", "https://checkout.stripe.com/test-session"));

            var controller = new OrderController(
                repo,
                cart,
                paymentMock.Object,
                loggerMock.Object
            );

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("http://localhost/order/paymentsuccess");

            controller.Url = urlHelperMock.Object;

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            return controller;
        }

        [Fact]
        public async Task Cannot_Checkout_Empty_Cart()
        {
            // Arrange
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            Cart cart = new Cart();
            Order order = new Order();
            OrderController target = CreateController(mock.Object, cart);

            // Act
            IActionResult actionResult = await target.Checkout(order);
            ViewResult? result = actionResult as ViewResult;

            // Assert
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Cannot_Checkout_Invalid_ShippingDetails()
        {
            // Arrange
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            Cart cart = new Cart();
            cart.AddItem(new Product(), 1);

            OrderController target = CreateController(mock.Object, cart);
            target.ModelState.AddModelError("error", "error");

            // Act
            IActionResult actionResult = await target.Checkout(new Order());
            ViewResult? result = actionResult as ViewResult;

            // Assert
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Can_Checkout_And_Submit_Order()
        {
            // Arrange
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            Cart cart = new Cart();
            cart.AddItem(new Product(), 1);

            OrderController target = CreateController(mock.Object, cart);

            var order = new Order
            {
                Name = "Test User",
                Line1 = "123 Main Street",
                City = "Dublin",
                State = "Leinster",
                Country = "Ireland"
            };

            // Act
            IActionResult actionResult = await target.Checkout(order);
            RedirectResult? result = actionResult as RedirectResult;

            // Assert
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Exactly(2));
            Assert.NotNull(result);
            Assert.Contains("stripe.com", result!.Url);
        }
    }
}