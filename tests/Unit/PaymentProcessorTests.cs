using System;
using OrderService.Domain;
using Xunit;

namespace OrderService.Unit.Tests;

[Trait("Category", "Unit")]
public class PaymentProcessorTests
{
    private static Order PendingOrder(decimal amount = 150_000m) => new()
    {
        Id = Guid.NewGuid(),
        Sku = "SKU-001",
        Quantity = 1,
        Amount = amount,
        PaymentStatus = PaymentStatus.Pending
    };

    [Fact]
    public void Apply_SuccessfulCode_MarksPaidAndSignalsPublish()
    {
        var order = PendingOrder();
        var now = DateTimeOffset.UtcNow;
        var result = new VnpayResult("00", "VNP123456", order.Amount);

        var transition = PaymentProcessor.Apply(order, result, now);

        Assert.Equal(PaymentStatus.Paid, transition.Status);
        Assert.True(transition.PublishOrderPlaced);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(now, order.PaidAt);
        Assert.Equal("VNP123456", order.VnpTransactionNo);
    }

    [Fact]
    public void Apply_DuplicateIpnOnPaidOrder_IsNoOpAndDoesNotPublishAgain()
    {
        var order = PendingOrder();
        var firstNow = DateTimeOffset.UtcNow;
        var first = PaymentProcessor.Apply(order, new VnpayResult("00", "VNP123456", order.Amount), firstNow);
        Assert.True(first.PublishOrderPlaced);

        // A retried/duplicate IPN arrives later with a different transaction number.
        var secondNow = firstNow.AddMinutes(5);
        var second = PaymentProcessor.Apply(order, new VnpayResult("00", "VNP999999", order.Amount), secondNow);

        Assert.Equal(PaymentStatus.Paid, second.Status);
        Assert.False(second.PublishOrderPlaced);
        // Original payment details are preserved — no clobbering by the duplicate.
        Assert.Equal(firstNow, order.PaidAt);
        Assert.Equal("VNP123456", order.VnpTransactionNo);
    }

    [Fact]
    public void Apply_FailureCode_MarksFailedAndDoesNotPublish()
    {
        var order = PendingOrder();
        var result = new VnpayResult("51", "VNP000000", order.Amount);

        var transition = PaymentProcessor.Apply(order, result, DateTimeOffset.UtcNow);

        Assert.Equal(PaymentStatus.Failed, transition.Status);
        Assert.False(transition.PublishOrderPlaced);
        Assert.Equal(PaymentStatus.Failed, order.PaymentStatus);
        Assert.Null(order.PaidAt);
        Assert.Null(order.VnpTransactionNo);
    }

    [Fact]
    public void Apply_CancelledCode_MarksCancelledAndDoesNotPublish()
    {
        var order = PendingOrder();
        var result = new VnpayResult("24", "VNP000000", order.Amount);

        var transition = PaymentProcessor.Apply(order, result, DateTimeOffset.UtcNow);

        Assert.Equal(PaymentStatus.Cancelled, transition.Status);
        Assert.False(transition.PublishOrderPlaced);
        Assert.Equal(PaymentStatus.Cancelled, order.PaymentStatus);
    }

    [Fact]
    public void Apply_AmountMismatch_ThrowsAndLeavesOrderUntouched()
    {
        var order = PendingOrder(amount: 150_000m);
        var result = new VnpayResult("00", "VNP123456", 999m);

        Assert.Throws<ArgumentException>(() => PaymentProcessor.Apply(order, result, DateTimeOffset.UtcNow));

        // State must not change on a rejected callback.
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.PaidAt);
        Assert.Null(order.VnpTransactionNo);
    }
}
