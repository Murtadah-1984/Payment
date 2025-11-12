namespace Payment.Domain.ValueObjects;

public sealed record PaymentProvider(string Name)
{
    public static PaymentProvider ZainCash => new("ZainCash");
    public static PaymentProvider AsiaHawala => new("AsiaHawala");
    public static PaymentProvider Stripe => new("Stripe");
    public static PaymentProvider FIB => new("FIB");
    public static PaymentProvider Square => new("Square");
    public static PaymentProvider Helcim => new("Helcim");
    public static PaymentProvider AmazonPaymentServices => new("AmazonPaymentServices");
    public static PaymentProvider Telr => new("Telr");
    public static PaymentProvider Checkout => new("Checkout");
    public static PaymentProvider Verifone => new("Verifone");
    public static PaymentProvider Paytabs => new("Paytabs");
    public static PaymentProvider Tap => new("Tap");
    public static PaymentProvider TapToPay => new("TapToPay");

    public static PaymentProvider FromString(string name)
    {
        var validProviders = new[] { "ZainCash", "AsiaHawala", "Stripe", "FIB", "Square", "Helcim", "AmazonPaymentServices", "Telr", "Checkout", "Verifone", "Paytabs", "Tap", "TapToPay" };
        if (!validProviders.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid payment provider: {name}", nameof(name));
        }

        return new PaymentProvider(name);
    }
}

