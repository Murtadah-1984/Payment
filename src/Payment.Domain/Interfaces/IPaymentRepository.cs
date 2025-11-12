using Payment.Domain.Common;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Interfaces;

public interface IPaymentRepository : IRepository<PaymentEntity>
{
    Task<PaymentEntity?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task<PaymentEntity?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentEntity>> GetByMerchantIdAsync(string merchantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentEntity>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);
    
    // Pagination support (Database Optimization #10)
    Task<PagedResult<PaymentEntity>> GetPagedByMerchantIdAsync(
        string merchantId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    
    Task<PagedResult<PaymentEntity>> GetPagedByStatusAsync(
        PaymentStatus status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

