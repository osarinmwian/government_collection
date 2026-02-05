namespace GovernmentCollections.Shared.Validation;

public class PinValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public PinValidationErrorType ErrorType { get; set; }
}

public enum PinValidationErrorType
{
    None,
    InvalidPin,
    ValidationFailed,
    UserNotFound,
    DatabaseError
}