using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Data;

namespace GovernmentCollections.Shared.Validation;

public class PinValidationService : IPinValidationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PinValidationService> _logger;
    private const int CommandTimeoutSeconds = 30;

    public PinValidationService(IConfiguration configuration, ILogger<PinValidationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ValidatePinAsync(string username, string pin)
    {
        var result = await ValidatePinWithResultAsync(username, pin);
        return result.IsValid;
    }

    public async Task<PinValidationResult> ValidatePinWithResultAsync(string username, string pin)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("PIN validation failed: Username is null or empty");
            return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.ValidationFailed, ErrorMessage = "Username is required" };
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            _logger.LogWarning("PIN validation failed: PIN is null or empty for user {Username}", username);
            return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.ValidationFailed, ErrorMessage = "PIN is required" };
        }

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("Database connection string not configured");
                return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.DatabaseError, ErrorMessage = "PIN validation failed" };
            }
            
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            const string query = "SELECT transactionpin, bvn, pinstatus FROM OmniProfiles WHERE username = @Username AND profilestatus = 'Active'";
            using var command = new SqlCommand(query, connection) { CommandTimeout = CommandTimeoutSeconds };
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = username;
            
            _logger.LogInformation("Executing PIN validation query for username: {Username}", username);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var storedPin = reader["transactionpin"]?.ToString();
                var bvn = reader["bvn"]?.ToString();
                var pinStatus = reader["pinstatus"]?.ToString();
                
                _logger.LogInformation("Found user {Username} - PIN exists: {HasPin}, BVN exists: {HasBvn}, PIN status: {PinStatus}", 
                    username, !string.IsNullOrEmpty(storedPin), !string.IsNullOrEmpty(bvn), pinStatus);
                
                if (string.IsNullOrEmpty(storedPin) || string.IsNullOrEmpty(bvn))
                {
                    _logger.LogWarning("PIN validation failed: Missing PIN or BVN for user {Username}", username);
                    return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.ValidationFailed, ErrorMessage = "PIN validation failed" };
                }

                if (!string.Equals(pinStatus, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("PIN validation failed: PIN status is {PinStatus} for user {Username}", pinStatus, username);
                    return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.ValidationFailed, ErrorMessage = "PIN validation failed" };
                }
                    
                var hashedPin = HashCustomerPin(pin, bvn);
                var isValid = string.Equals(hashedPin, storedPin, StringComparison.Ordinal);
                
                _logger.LogInformation("PIN hash comparison - Input: {InputHash}, Stored: {StoredHash}, Match: {IsValid}", 
                    hashedPin, storedPin, isValid);
                _logger.LogInformation("PIN validation {Result} for user {Username}", isValid ? "successful" : "failed", username);
                
                if (isValid)
                {
                    return new PinValidationResult { IsValid = true, ErrorType = PinValidationErrorType.None };
                }
                else
                {
                    return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.InvalidPin, ErrorMessage = "Invalid PIN" };
                }
            }
            
            _logger.LogWarning("PIN validation failed: User {Username} not found or inactive", username);
            return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.UserNotFound, ErrorMessage = "PIN validation failed" };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database error during PIN validation for user {Username}", username);
            return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.DatabaseError, ErrorMessage = "PIN validation failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating PIN for user {Username}", username);
            return new PinValidationResult { IsValid = false, ErrorType = PinValidationErrorType.ValidationFailed, ErrorMessage = "PIN validation failed" };
        }
    }

    public async Task<bool> Validate2FAAsync(string username, string secondFa, string secondFaType)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(secondFa))
            return false;

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var hashedOtp = HashOtp(secondFa);
            
            const string selectQuery = @"
                SELECT id FROM UserOTPs 
                WHERE username = @Username AND tokencode = @HashedOtp";
                    
            using var selectCommand = new SqlCommand(selectQuery, connection);
            selectCommand.Parameters.Add("@Username", SqlDbType.NVarChar, 200).Value = username;
            selectCommand.Parameters.Add("@HashedOtp", SqlDbType.NVarChar).Value = hashedOtp;
            
            var result = await selectCommand.ExecuteScalarAsync();
            return result != null;
        }
        catch
        {
            return false;
        }
    }
    
    private string HashOtp(string otp)
    {
        using var sha512 = System.Security.Cryptography.SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(otp);
        var hash = sha512.ComputeHash(bytes);
        
        var result = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
        {
            result.Append(hash[i].ToString("X2"));
        }
        return result.ToString();
    }

    public async Task<bool> ValidateWithEnforcementAsync(string username, string pin, string secondFa = null, string secondFaType = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(pin))
            return false;

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            const string query = "SELECT TwoFaStatus FROM OmniProfiles WHERE username = @Username AND profilestatus = 'Active'";
            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = username;
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var twoFaStatus = reader["TwoFaStatus"]?.ToString();
                var isEnforced = string.Equals(twoFaStatus, "true", StringComparison.OrdinalIgnoreCase);
                
                reader.Close();
                
                var pinValid = await ValidatePinAsync(username, pin);
                if (!pinValid) return false;
                
                if (isEnforced && !string.IsNullOrWhiteSpace(secondFa))
                {
                    return await Validate2FAAsync(username, secondFa, secondFaType);
                }
                
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    private string HashCustomerPin(string pin, string bvn)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(bvn))
            throw new ArgumentException("PIN and BVN cannot be null or empty");
            
        string salt = "KeySaltXXXYYYZZ";
        var combined = $"{salt}:{pin}:{bvn}";
        using var sha512 = System.Security.Cryptography.SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = sha512.ComputeHash(bytes);
        
        var result = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
        {
            result.Append(hash[i].ToString("X2"));
        }
        return result.ToString();
    }
}