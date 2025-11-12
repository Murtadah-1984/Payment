using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Commands;

/// <summary>
/// Command to handle payment provider callbacks/webhooks.
/// Follows CQRS pattern and Single Responsibility Principle.
/// </summary>
public sealed record HandlePaymentCallbackCommand(
    string Provider,
    Dictionary<string, string> CallbackData) : IRequest<PaymentDto?>;

