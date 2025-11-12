namespace Payment.Domain.ValueObjects;

public sealed record PaymentMethod(string Value)
{
    public static PaymentMethod CreditCard => new("CreditCard");
    public static PaymentMethod DebitCard => new("DebitCard");
    public static PaymentMethod PayPal => new("PayPal");
    public static PaymentMethod BankTransfer => new("BankTransfer");
    public static PaymentMethod Crypto => new("Crypto");
    public static PaymentMethod TapToPay => new("TapToPay");

    public static PaymentMethod FromString(string value)
    {
        var validMethods = new[] { "CreditCard", "DebitCard", "PayPal", "BankTransfer", "Crypto", "TapToPay", "Wallet", "Card", "Cash" };
        if (!validMethods.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid payment method: {value}", nameof(value));
        }

        return new PaymentMethod(value);
    }
}

