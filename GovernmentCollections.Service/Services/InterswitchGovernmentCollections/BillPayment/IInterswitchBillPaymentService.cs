using GovernmentCollections.Domain.DTOs.Interswitch;

namespace GovernmentCollections.Service.Services.InterswitchGovernmentCollections.BillPayment;

public interface IInterswitchBillPaymentService
{
    Task<List<InterswitchBiller>> GetGovernmentBillersAsync();
    Task<List<InterswitchBiller>> GetBillersByCategoryAsync(int categoryId);
    Task<InterswitchServicesResponse> GetGovernmentCategoriesAsync();
    Task<List<InterswitchPaymentItem>> GetServiceOptionsAsync(int serviceId);
    Task<InterswitchPaymentResponse> ProcessTransactionAsync(InterswitchTransactionRequest request);
    Task<InterswitchPaymentResponse> GetTransactionStatusAsync(string requestReference);
    Task<InterswitchCustomerValidationResponse> ValidateCustomersAsync(InterswitchCustomerValidationBatchRequest request);
}