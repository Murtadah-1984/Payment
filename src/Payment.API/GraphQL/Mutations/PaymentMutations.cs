using HotChocolate;
using HotChocolate.Authorization;
using MediatR;
using Payment.API.GraphQL.Mappings;
using Payment.API.GraphQL.Types;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Domain.Common;

namespace Payment.API.GraphQL.Mutations;

/// <summary>
/// GraphQL mutations for payment operations.
/// Follows Clean Architecture - delegates to Application layer via MediatR.
/// Stateless and Kubernetes-ready.
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
[Authorize]
public class PaymentMutations
{
    /// <summary>
    /// Creates a new payment.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="input">Payment creation input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created payment</returns>
    public async Task<PaymentType> CreatePaymentAsync(
        [Service] IMediator mediator,
        CreatePaymentInput input,
        CancellationToken cancellationToken)
    {
        var dto = input.ToApplicationDto();
        var command = new CreatePaymentCommand(
            RequestId: dto.RequestId,
            Amount: dto.Amount,
            Currency: dto.Currency,
            PaymentMethod: dto.PaymentMethod,
            Provider: dto.Provider,
            MerchantId: dto.MerchantId,
            OrderId: dto.OrderId,
            ProjectCode: dto.ProjectCode,
            IdempotencyKey: dto.IdempotencyKey,
            SystemFeePercent: dto.SystemFeePercent,
            SplitRule: dto.SplitRule != null ? new SplitRuleDto(
                dto.SplitRule.SystemFeePercent,
                dto.SplitRule.Accounts.Select(a => new SplitAccountDto(
                    a.AccountType,
                    a.AccountIdentifier,
                    a.Percentage
                )).ToList()
            ) : null,
            Metadata: dto.Metadata,
            CallbackUrl: dto.CallbackUrl,
            CustomerEmail: dto.CustomerEmail,
            CustomerPhone: dto.CustomerPhone,
            NfcToken: dto.NfcToken,
            DeviceId: dto.DeviceId,
            CustomerId: dto.CustomerId
        );

        var result = await mediator.Send(command, cancellationToken);
        return result.ToGraphQLType();
    }

    /// <summary>
    /// Marks a payment as processing.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="paymentId">Payment ID</param>
    /// <param name="transactionId">Transaction ID from payment provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated payment</returns>
    public async Task<PaymentType> ProcessPaymentAsync(
        [Service] IMediator mediator,
        Guid paymentId,
        string transactionId,
        CancellationToken cancellationToken)
    {
        var command = new ProcessPaymentCommand(paymentId, transactionId);
        var result = await mediator.Send(command, cancellationToken);
        return result.ToGraphQLType();
    }

    /// <summary>
    /// Marks a payment as completed.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="paymentId">Payment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated payment</returns>
    public async Task<PaymentType> CompletePaymentAsync(
        [Service] IMediator mediator,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var command = new CompletePaymentCommand(paymentId);
        var result = await mediator.Send(command, cancellationToken);
        return result.ToGraphQLType();
    }

    /// <summary>
    /// Marks a payment as failed.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="paymentId">Payment ID</param>
    /// <param name="reason">Reason for failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated payment</returns>
    public async Task<PaymentType?> FailPaymentAsync(
        [Service] IMediator mediator,
        Guid paymentId,
        string reason,
        CancellationToken cancellationToken)
    {
        var command = new FailPaymentCommand(paymentId, reason);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
        {
            return null;
        }

        return result.Value?.ToGraphQLType();
    }

    /// <summary>
    /// Refunds a completed payment.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="paymentId">Payment ID</param>
    /// <param name="refundTransactionId">Transaction ID for the refund</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refunded payment</returns>
    public async Task<PaymentType?> RefundPaymentAsync(
        [Service] IMediator mediator,
        Guid paymentId,
        string refundTransactionId,
        CancellationToken cancellationToken = default)
    {
        var command = new RefundPaymentCommand(paymentId, refundTransactionId);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
        {
            return null;
        }

        return result.Value?.ToGraphQLType();
    }
}

