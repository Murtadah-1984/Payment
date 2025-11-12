using HotChocolate;
using HotChocolate.Authorization;
using MediatR;
using Payment.API.GraphQL.Mappings;
using Payment.API.GraphQL.Types;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Domain.Common;

namespace Payment.API.GraphQL.Queries;

/// <summary>
/// GraphQL queries for payment operations.
/// Follows Clean Architecture - delegates to Application layer via MediatR.
/// Stateless and Kubernetes-ready.
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
[Authorize]
public class PaymentQueries
{
    /// <summary>
    /// Gets a payment by ID.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="paymentId">Payment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment if found, null otherwise</returns>
    public async Task<PaymentType?> GetPaymentByIdAsync(
        [Service] IMediator mediator,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentByIdQuery(paymentId);
        var result = await mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return null;
        }

        return result.Value?.ToGraphQLType();
    }

    /// <summary>
    /// Gets a payment by order ID.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="orderId">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment if found, null otherwise</returns>
    public async Task<PaymentType?> GetPaymentByOrderIdAsync(
        [Service] IMediator mediator,
        string orderId,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentByOrderIdQuery(orderId);
        var payment = await mediator.Send(query, cancellationToken);

        return payment?.ToGraphQLType();
    }

    /// <summary>
    /// Gets all payments for a merchant.
    /// </summary>
    /// <param name="mediator">MediatR mediator for CQRS pattern</param>
    /// <param name="merchantId">Merchant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of payments for the merchant</returns>
    public async Task<IEnumerable<PaymentType>> GetPaymentsByMerchantAsync(
        [Service] IMediator mediator,
        string merchantId,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentsByMerchantQuery(merchantId);
        var payments = await mediator.Send(query, cancellationToken);

        return payments.Select(p => p.ToGraphQLType());
    }
}

