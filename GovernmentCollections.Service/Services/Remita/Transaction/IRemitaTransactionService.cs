using GovernmentCollections.Domain.DTOs.Remita;

namespace GovernmentCollections.Service.Services.Remita.Transaction;

public interface IRemitaTransactionService
{
    Task<dynamic> InitiateTransactionAsync(RemitaTransactionInitiateDto request);
    Task<dynamic> ProcessPaymentNotificationAsync(RemitaPaymentNotificationDto request);
    Task<dynamic> ProcessTransactionWithAuthAsync(RemitaTransactionInitiateDto request);
    Task<dynamic> GetTransactionStatusAsync(string transactionId);
    Task<dynamic> QueryTransactionAsync(string transactionRef);
}