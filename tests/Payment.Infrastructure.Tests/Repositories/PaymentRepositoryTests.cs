using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Repositories;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Infrastructure.Tests.Repositories;

public class PaymentRepositoryTests : IDisposable
{
    private readonly PaymentDbContext _context;
    private readonly PaymentRepository _repository;

    public PaymentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PaymentDbContext(options);
        _repository = new PaymentRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldAddPayment_WhenValid()
    {
        // Arrange
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

        // Act
        await _repository.AddAsync(payment);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.Payments.FirstOrDefaultAsync(p => p.Id.Value == payment.Id.Value);
        result.Should().NotBeNull();
        result!.OrderId.Should().Be("order-456");
    }

    [Fact]
    public async Task GetByOrderIdAsync_ShouldReturnPayment_WhenExists()
    {
        // Arrange
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByOrderIdAsync("order-456");

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be("order-456");
    }

    [Fact]
    public async Task GetByOrderIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByOrderIdAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByMerchantIdAsync_ShouldReturnPayments_WhenExists()
    {
        // Arrange
        var payment1 = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-1");

        var payment2 = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(200.75m),
            Currency.EUR,
            PaymentMethod.DebitCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-2");

        _context.Payments.AddRange(payment1, payment2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByMerchantIdAsync("merchant-123");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.MerchantId.Should().Be("merchant-123"));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

