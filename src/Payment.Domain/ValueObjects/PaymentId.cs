namespace Payment.Domain.ValueObjects;

public sealed record PaymentId(Guid Value)
{
    public static PaymentId NewId() => new(Guid.NewGuid());
    public static PaymentId FromGuid(Guid value) => new(value);
}

