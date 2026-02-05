namespace GovernmentCollections.Service.Services.RevPay.Validation;

public interface IPinValidationService
{
    Task<bool> ValidatePinAsync(string username, string pin);
    Task<bool> Validate2FAAsync(string userId, string secondFa, string secondFaType);
}