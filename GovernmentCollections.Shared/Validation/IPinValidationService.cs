namespace GovernmentCollections.Shared.Validation;

public interface IPinValidationService
{
    Task<bool> ValidatePinAsync(string username, string pin);
    Task<PinValidationResult> ValidatePinWithResultAsync(string username, string pin);
    Task<bool> Validate2FAAsync(string customerId, string secondFa, string secondFaType);
    Task<bool> ValidateWithEnforcementAsync(string username, string pin, string secondFa = null, string secondFaType = null);
}