using FluentAssertions;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Validators;
using Xunit;

namespace Payment.Application.Tests.Validators;

public class CreatePaymentCommandValidatorTests
{
    private readonly CreatePaymentCommandValidator _validator;

    public CreatePaymentCommandValidatorTests()
    {
        _validator = new CreatePaymentCommandValidator();
    }

    private CreatePaymentCommand CreateValidCommand(
        decimal? amount = null,
        string? currency = null,
        string? merchantId = null,
        string? orderId = null,
        string? projectCode = null,
        Dictionary<string, string>? metadata = null,
        string? callbackUrl = null,
        string? customerEmail = null,
        string? customerPhone = null)
    {
        return new CreatePaymentCommand(
            Guid.NewGuid(),
            amount ?? 100.50m,
            currency ?? "USD",
            "CreditCard",
            "ZainCash",
            merchantId ?? "merchant-123",
            orderId ?? "order-456",
            projectCode ?? "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            metadata,
            callbackUrl,
            customerEmail,
            customerPhone);
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenAllFieldsAreValid()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #region Amount Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_ShouldBeInvalid_WhenAmountIsZeroOrNegative(decimal amount)
    {
        // Arrange
        var command = CreateValidCommand(amount: amount);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenAmountExceedsMaximum()
    {
        // Arrange
        var command = CreateValidCommand(amount: 1_000_001m);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenAmountIsAtMaximum()
    {
        // Arrange
        var command = CreateValidCommand(amount: 1_000_000m);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Currency Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Validate_ShouldBeInvalid_WhenCurrencyIsInvalidLength(string currency)
    {
        // Arrange
        var command = CreateValidCommand(currency: currency);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency");
    }

    [Theory]
    [InlineData("XXX")] // Invalid ISO code
    [InlineData("ABC")] // Invalid ISO code
    public void Validate_ShouldBeInvalid_WhenCurrencyIsNotValidISO(string currency)
    {
        // Arrange
        var command = CreateValidCommand(currency: currency);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency" && 
            e.ErrorMessage.Contains("valid ISO 4217"));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("AED")]
    [InlineData("IQD")]
    public void Validate_ShouldBeValid_WhenCurrencyIsValidISO(string currency)
    {
        // Arrange
        var command = CreateValidCommand(currency: currency);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region PaymentMethod Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("InvalidMethod")]
    public void Validate_ShouldBeInvalid_WhenPaymentMethodIsInvalid(string paymentMethod)
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            paymentMethod,
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentMethod");
    }

    #endregion

    #region MerchantId Validation Tests

    [Fact]
    public void Validate_ShouldBeInvalid_WhenMerchantIdIsEmpty()
    {
        // Arrange
        var command = CreateValidCommand(merchantId: "");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MerchantId");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenMerchantIdExceedsMaximumLength()
    {
        // Arrange
        var command = CreateValidCommand(merchantId: new string('a', 101));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MerchantId" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Theory]
    [InlineData("merchant@123")] // Contains @
    [InlineData("merchant#123")] // Contains #
    [InlineData("merchant$123")] // Contains $
    [InlineData("merchant<script>")] // Contains <
    public void Validate_ShouldBeInvalid_WhenMerchantIdContainsInvalidCharacters(string merchantId)
    {
        // Arrange
        var command = CreateValidCommand(merchantId: merchantId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MerchantId" && 
            e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory]
    [InlineData("merchant-123")]
    [InlineData("merchant_123")]
    [InlineData("merchant.123")]
    [InlineData("merchant123")]
    public void Validate_ShouldBeValid_WhenMerchantIdHasValidCharacters(string merchantId)
    {
        // Arrange
        var command = CreateValidCommand(merchantId: merchantId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region OrderId Validation Tests

    [Fact]
    public void Validate_ShouldBeInvalid_WhenOrderIdIsEmpty()
    {
        // Arrange
        var command = CreateValidCommand(orderId: "");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderId");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenOrderIdExceedsMaximumLength()
    {
        // Arrange
        var command = CreateValidCommand(orderId: new string('a', 101));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderId" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Theory]
    [InlineData("order@123")] // Contains @
    [InlineData("order#123")] // Contains #
    [InlineData("order.123")] // Contains . (not allowed for OrderId)
    [InlineData("order 123")] // Contains space
    public void Validate_ShouldBeInvalid_WhenOrderIdContainsInvalidCharacters(string orderId)
    {
        // Arrange
        var command = CreateValidCommand(orderId: orderId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderId");
    }

    [Theory]
    [InlineData("order-123")]
    [InlineData("order_123")]
    [InlineData("order123")]
    public void Validate_ShouldBeValid_WhenOrderIdHasValidCharacters(string orderId)
    {
        // Arrange
        var command = CreateValidCommand(orderId: orderId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region ProjectCode Validation Tests

    [Fact]
    public void Validate_ShouldBeInvalid_WhenProjectCodeIsEmpty()
    {
        // Arrange
        var command = CreateValidCommand(projectCode: "");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProjectCode");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenProjectCodeExceedsMaximumLength()
    {
        // Arrange
        var command = CreateValidCommand(projectCode: new string('a', 101));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProjectCode" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    #endregion

    #region Metadata Validation Tests

    [Fact]
    public void Validate_ShouldBeValid_WhenMetadataIsNull()
    {
        // Arrange
        var command = CreateValidCommand(metadata: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenMetadataIsEmpty()
    {
        // Arrange
        var command = CreateValidCommand(metadata: new Dictionary<string, string>());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenMetadataExceedsMaximumKeys()
    {
        // Arrange
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 51; i++)
        {
            metadata[$"key{i}"] = "value";
        }
        var command = CreateValidCommand(metadata: metadata);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Metadata" && 
            e.ErrorMessage.Contains("exceeds size limits"));
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenMetadataKeyExceedsMaximumLength()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { new string('a', 101), "value" }
        };
        var command = CreateValidCommand(metadata: metadata);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Metadata");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenMetadataValueExceedsMaximumLength()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "key", new string('a', 1001) }
        };
        var command = CreateValidCommand(metadata: metadata);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Metadata");
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("onerror=alert('xss')")]
    [InlineData("onclick=alert('xss')")]
    [InlineData("onload=alert('xss')")]
    [InlineData("vbscript:alert('xss')")]
    [InlineData("data:text/html,<script>alert('xss')</script>")]
    [InlineData("&#x3C;script&#x3E;")]
    [InlineData("&#60;script&#62;")]
    [InlineData("eval('malicious')")]
    [InlineData("expression('malicious')")]
    public void Validate_ShouldBeInvalid_WhenMetadataContainsDangerousContent(string dangerousValue)
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "key", dangerousValue }
        };
        var command = CreateValidCommand(metadata: metadata);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Metadata" && 
            e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory]
    [InlineData("key@123")] // Contains @
    [InlineData("key#123")] // Contains #
    [InlineData("key.123")] // Contains . (not allowed for metadata keys)
    [InlineData("key 123")] // Contains space
    public void Validate_ShouldBeInvalid_WhenMetadataKeyContainsInvalidCharacters(string invalidKey)
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { invalidKey, "value" }
        };
        var command = CreateValidCommand(metadata: metadata);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Metadata");
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenMetadataIsValid()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key_3", "value3" },
            { "key-4", "value4" }
        };
        var command = CreateValidCommand(metadata: metadata);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region CallbackUrl Validation Tests

    [Fact]
    public void Validate_ShouldBeValid_WhenCallbackUrlIsNull()
    {
        // Arrange
        var command = CreateValidCommand(callbackUrl: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://example.com/callback")] // HTTP not allowed
    [InlineData("ftp://example.com/callback")] // Invalid scheme
    [InlineData("not-a-url")] // Not a URL
    [InlineData("https://")] // Invalid URL
    public void Validate_ShouldBeInvalid_WhenCallbackUrlIsInvalid(string callbackUrl)
    {
        // Arrange
        var command = CreateValidCommand(callbackUrl: callbackUrl);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CallbackUrl");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenCallbackUrlExceedsMaximumLength()
    {
        // Arrange
        var callbackUrl = "https://example.com/" + new string('a', 2048);
        var command = CreateValidCommand(callbackUrl: callbackUrl);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CallbackUrl" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenCallbackUrlIsValidHttps()
    {
        // Arrange
        var command = CreateValidCommand(callbackUrl: "https://example.com/callback");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region CustomerEmail Validation Tests

    [Fact]
    public void Validate_ShouldBeValid_WhenCustomerEmailIsNull()
    {
        // Arrange
        var command = CreateValidCommand(customerEmail: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    [InlineData("invalid@.com")]
    public void Validate_ShouldBeInvalid_WhenCustomerEmailIsInvalid(string email)
    {
        // Arrange
        var command = CreateValidCommand(customerEmail: email);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerEmail");
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenCustomerEmailExceedsMaximumLength()
    {
        // Arrange
        var email = new string('a', 250) + "@example.com";
        var command = CreateValidCommand(customerEmail: email);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerEmail" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenCustomerEmailIsValid()
    {
        // Arrange
        var command = CreateValidCommand(customerEmail: "customer@example.com");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region CustomerPhone Validation Tests

    [Fact]
    public void Validate_ShouldBeValid_WhenCustomerPhoneIsNull()
    {
        // Arrange
        var command = CreateValidCommand(customerPhone: null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenCustomerPhoneExceedsMaximumLength()
    {
        // Arrange
        var phone = new string('1', 21);
        var command = CreateValidCommand(customerPhone: phone);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerPhone" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Theory]
    [InlineData("+1234567890")]
    [InlineData("(123) 456-7890")]
    [InlineData("123-456-7890")]
    [InlineData("1234567890")]
    [InlineData("+1 (123) 456-7890")]
    public void Validate_ShouldBeValid_WhenCustomerPhoneIsValid(string phone)
    {
        // Arrange
        var command = CreateValidCommand(customerPhone: phone);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid-phone")]
    [InlineData("abc123")]
    [InlineData("++1234567890")] // Double plus
    public void Validate_ShouldBeInvalid_WhenCustomerPhoneIsInvalid(string phone)
    {
        // Arrange
        var command = CreateValidCommand(customerPhone: phone);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerPhone");
    }

    #endregion

    #region Tap-to-Pay Validation Tests

    [Fact]
    public void Validate_ShouldBeInvalid_WhenTapToPayProvider_AndNfcTokenMissing()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            null, // NfcToken missing
            null,
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NfcToken" && 
            e.ErrorMessage.Contains("NFC token is required"));
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenTapToPayPaymentMethod_AndNfcTokenMissing()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "Stripe", // Different provider but TapToPay method
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            null, // NfcToken missing
            null,
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NfcToken");
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenTapToPay_AndNfcTokenProvided()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token", // NfcToken provided
            "device-123",
            "customer-456");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenOtherProvider_AndNfcTokenNotRequired()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "Stripe",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            null, // NfcToken not required for non-TapToPay
            null,
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNfcTokenExceedsMaximumLength()
    {
        // Arrange
        var longToken = new string('a', 5001); // Exceeds 5000 character limit
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            longToken,
            null,
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NfcToken" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenDeviceIdExceedsMaximumLength()
    {
        // Arrange
        var longDeviceId = new string('a', 201); // Exceeds 200 character limit
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            longDeviceId,
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeviceId" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenDeviceIdContainsInvalidCharacters()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            "device@123", // Contains @
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeviceId" && 
            e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory]
    [InlineData("device-123")]
    [InlineData("device_123")]
    [InlineData("device.123")]
    [InlineData("device123")]
    public void Validate_ShouldBeValid_WhenDeviceIdHasValidCharacters(string deviceId)
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            deviceId,
            null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenCustomerIdExceedsMaximumLength()
    {
        // Arrange
        var longCustomerId = new string('a', 201); // Exceeds 200 character limit
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            null,
            longCustomerId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerId" && 
            e.ErrorMessage.Contains("must not exceed"));
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenCustomerIdContainsInvalidCharacters()
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            null,
            "customer@123"); // Contains @

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerId" && 
            e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory]
    [InlineData("customer-123")]
    [InlineData("customer_123")]
    [InlineData("customer.123")]
    [InlineData("customer123")]
    public void Validate_ShouldBeValid_WhenCustomerIdHasValidCharacters(string customerId)
    {
        // Arrange
        var command = new CreatePaymentCommand(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            null,
            customerId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion
}

