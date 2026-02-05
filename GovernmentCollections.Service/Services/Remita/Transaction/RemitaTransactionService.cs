using GovernmentCollections.Domain.DTOs.Remita;
using GovernmentCollections.Service.Services.Settlement;
using GovernmentCollections.Service.Services.Remita.Authentication;
using GovernmentCollections.Service.Services.Remita.Models;
using GovernmentCollections.Shared.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace GovernmentCollections.Service.Services.Remita.Transaction;

public class RemitaTransactionService : IRemitaTransactionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RemitaTransactionService> _logger;
    private readonly IPinValidationService _pinValidationService;
    private readonly ISettlementService _settlementService;
    private readonly IRemitaAuthenticationService _authService;

    public RemitaTransactionService(HttpClient httpClient, IConfiguration configuration, ILogger<RemitaTransactionService> logger, 
        IPinValidationService pinValidationService, ISettlementService settlementService, IRemitaAuthenticationService authService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _pinValidationService = pinValidationService;
        _settlementService = settlementService;
        _authService = authService;
    }

    public async Task<dynamic> InitiateTransactionAsync(RemitaTransactionInitiateDto request)
    {
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/remita/exapp/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/initiate";
            
            _logger.LogInformation("Setting authentication header for Remita request");
            await _authService.SetAuthHeaderAsync(_httpClient);
            
            var initiateRequest = new
            {
                billPaymentProductId = request.BillPaymentProductId,
                amount = request.Amount,
                transactionRef = request.TransactionRef,
                name = request.Name,
                email = request.Email,
                phoneNumber = request.PhoneNumber,
                customerId = request.CustomerId,
                metadata = request.Metadata
            };
            
            var json = JsonSerializer.Serialize(initiateRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogInformation("Sending request to Remita: {Url}", requestUrl);
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Remita initiate response: {Response}", responseContent);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Remita API returned error status: {StatusCode}", response.StatusCode);
                return new { responseData = "{}", responseCode = 500, responseMsg = "Internal Server Error" };
            }
            
            using var document = JsonDocument.Parse(responseContent);
            return document.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Remita transaction");
            return new { responseData = "{}", responseCode = 500, responseMsg = $"Service error: {ex.Message}" };
        }
    }

    public async Task<dynamic> ProcessPaymentNotificationAsync(RemitaPaymentNotificationDto request)
    {
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/remita/exapp/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/paymentnotification";
            
            await _authService.SetAuthHeaderAsync(_httpClient);
            
            var paymentRequest = new
            {
                rrr = request.Rrr,
                transactionRef = request.TransactionRef,
                amount = request.Amount,
                channel = request.Channel,
                debitAccountNumber = request.DebitAccountNumber,
                pin = request.Pin,
                secondFa = request.SecondFa,
                secondFaType = request.SecondFaType,
                enforce2FA = request.Enforce2FA,
                metadata = request.Metadata
            };
            
            var json = JsonSerializer.Serialize(paymentRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Remita payment notification response: {Response}", responseContent);
            
            using var document = JsonDocument.Parse(responseContent);
            return document.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Remita payment notification");
            return new { status = "99", message = "Failed to process payment notification", data = (object?)null };
        }
    }

    public async Task<dynamic> ProcessTransactionWithAuthAsync(RemitaTransactionInitiateDto request)
    {
        try
        {
            // Step 1: Initiate transaction
            var initiateResult = await InitiateTransactionAsync(request);
            dynamic initResult = initiateResult;
            
            if (initResult?.status != "00")
            {
                return initiateResult;
            }
            
            return new { status = "00", message = "Transaction initiated successfully", data = initResult.data };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessTransactionWithAuthAsync");
            return new { status = "99", message = "Service temporarily unavailable. Please try again later.", data = (object?)null };
        }
    }



    public async Task<dynamic> GetTransactionStatusAsync(string transactionId)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            await _authService.SetAuthHeaderForBaseUrl2Async(_httpClient);
            
            var baseUrl = _configuration["Remita:BaseUrl2"];
            var requestUrl = $"{baseUrl}/services/connect-gateway/api/v1/integration/query/transaction/{transactionId}";
            
            _logger.LogInformation("[OUTBOUND-{RequestId}] GetTransactionStatusAsync: GET {Url}", requestId, requestUrl);
            
            var response = await _httpClient.GetAsync(requestUrl);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] GetTransactionStatusAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            if (string.IsNullOrWhiteSpace(responseContent))
                return new { Status = "ERROR", Data = new object() };
            
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                return new { Status = response.IsSuccessStatusCode ? "SUCCESS" : "ERROR", Data = document.RootElement.Clone() };
            }
            catch
            {
                return new { Status = "ERROR", Data = new object() };
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] GetTransactionStatusAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            return new { Status = "ERROR", Data = new object() };
        }
    }

    public async Task<dynamic> QueryTransactionAsync(string transactionRef)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("[INFO-{RequestId}] Querying transaction reference: {TransactionRef}", requestId, transactionRef);
            
            var token = await _authService.GetAccessTokenAsync();
            var baseUrl = _configuration["Remita:BaseUrl2"];
            var publicKey2 = _configuration["Remita:PublicKey2"];
            var requestUrl = $"{baseUrl}/services/connect-gateway/api/v1/integration/query/transaction/{transactionRef}";
            
            _logger.LogInformation("[DEBUG-{RequestId}] Using PublicKey2: {PublicKey}", requestId, publicKey2);
            _logger.LogInformation("[OUTBOUND-{RequestId}] QueryTransactionAsync: GET {Url}", requestId, requestUrl);
            
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            if (!string.IsNullOrEmpty(publicKey2))
            {
                request.Headers.Add("publicKey", publicKey2);
            }
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] QueryTransactionAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return new { status = "99", message = "No response received", data = (object?)null };
            }
            
            var result = JsonSerializer.Deserialize<RemitaQueryResponse>(responseContent);
            return (object?)(result ?? new RemitaQueryResponse { Status = "99", Message = "Empty response", Data = null });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[ERROR-{RequestId}] QueryTransactionAsync: Failed to parse JSON response", requestId);
            return new { status = "99", message = "Invalid response format", data = (object?)null };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] QueryTransactionAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            return new { status = "99", message = "Service temporarily unavailable", data = (object?)null };
        }
    }
}