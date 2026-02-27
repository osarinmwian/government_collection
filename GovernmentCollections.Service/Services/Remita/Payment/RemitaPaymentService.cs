using GovernmentCollections.Domain.DTOs.Remita;
using GovernmentCollections.Service.Services.Remita.Authentication;
using GovernmentCollections.Service.Services.Settlement;
using GovernmentCollections.Shared.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace GovernmentCollections.Service.Services.Remita.Payment;

public class RemitaPaymentService : IRemitaPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RemitaPaymentService> _logger;
    private readonly IRemitaAuthenticationService _authService;
    private readonly ISettlementService _settlementService;
    private readonly IPinValidationService _pinValidationService;

    public RemitaPaymentService(HttpClient httpClient, IConfiguration configuration, ILogger<RemitaPaymentService> logger, 
        IRemitaAuthenticationService authService, ISettlementService settlementService, IPinValidationService pinValidationService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _authService = authService;
        _settlementService = settlementService;
        _pinValidationService = pinValidationService;
    }

    public async Task<RemitaPaymentResponse> ProcessPaymentAsync(RemitaPaymentRequest request)
    {
        await _authService.SetAuthHeaderAsync(_httpClient);
        var transactionRef = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        
        var initRequest = new RemitaTransactionInitiateDto
        {
            BillPaymentProductId = request.ProductId,
            Amount = request.Amount,
            TransactionRef = transactionRef,
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.Phone,
            CustomerId = request.CustomerId
        };
        
        var baseUrl = _configuration["Remita:BaseUrl"];
        var initUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/initiate";
        
        var json = JsonSerializer.Serialize(initRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var initResponse = await _httpClient.PostAsync(initUrl, content);
        
        if (!initResponse.IsSuccessStatusCode)
        {
            return new RemitaPaymentResponse { Status = "ERROR", Rrr = "", Amount = 0, Paid = false, ReceiptUrl = "" };
        }
        
        var rrr = request.TransactionRef;
        
        var payRequest = new RemitaPaymentProcessDto
        {
            Rrr = rrr,
            TransactionRef = transactionRef,
            Amount = request.Amount,
            Channel = "internetbanking",
            Metadata = new RemitaPaymentMetadata
            {
                FundingSource = "HERITAGE",
                PayerAccountNumber = "2035468030"
            }
        };
        
        var payUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/pay";
        var payJson = JsonSerializer.Serialize(payRequest);
        var payContent = new StringContent(payJson, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(payUrl, payContent);
        
        var queryUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/query/{transactionRef}";
        var queryResponse = await _httpClient.GetAsync(queryUrl);
        var queryResponseContent = await queryResponse.Content.ReadAsStringAsync();
        
        if (queryResponse.IsSuccessStatusCode)
        {
            var queryResult = JsonSerializer.Deserialize<RemitaTransactionQueryResponse>(queryResponseContent);
            return new RemitaPaymentResponse
            {
                Status = "SUCCESS",
                Rrr = queryResult?.Data?.Rrr ?? rrr,
                Amount = queryResult?.Data?.Amount ?? request.Amount,
                Paid = queryResult?.Data?.Paid ?? true,
                ReceiptUrl = queryResult?.Data?.Metadata?.ReceiptUrl ?? ""
            };
        }
        
        return new RemitaPaymentResponse
        {
            Status = "SUCCESS",
            Rrr = rrr,
            Amount = request.Amount,
            Paid = true,
            ReceiptUrl = ""
        };
    }

    public Task<dynamic> InitiatePaymentAsync(RemitaInitiatePaymentDto request)
    {
        var invoiceRequest = new RemitaInvoiceRequest
        {
            ServiceTypeId = _configuration["Remita:ServiceTypeId"] ?? string.Empty,
            Amount = request.Amount,
            OrderId = request.OrderId ?? Guid.NewGuid().ToString(),
            PayerName = request.PayerName,
            PayerEmail = request.PayerEmail,
            PayerPhone = request.PayerPhone,
            Description = request.Description
        };
        
        return Task.FromResult<dynamic>(new { Status = "SUCCESS", Data = invoiceRequest });
    }

    public async Task<dynamic> VerifyPaymentAsync(string rrr)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/remita/verify/{rrr}";
            
            _logger.LogInformation("[OUTBOUND-{RequestId}] VerifyPaymentAsync: GET {Url}", requestId, requestUrl);
            
            var response = await _httpClient.GetAsync(requestUrl);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] VerifyPaymentAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            if (!response.IsSuccessStatusCode)
            {
                return new { Status = "ERROR", Message = "Payment verification failed", Data = responseContent };
            }
            
            try
            {
                var data = JsonSerializer.Deserialize<object>(responseContent);
                return new { Status = "SUCCESS", Data = data };
            }
            catch (JsonException)
            {
                return new { Status = "ERROR", Message = "Invalid response format", Data = responseContent };
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] VerifyPaymentAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            return new { Status = "ERROR", Message = ex.Message, Data = (object?)null };
        }
    }

    public async Task<dynamic> GetActiveBanksAsync()
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/api/v1/send/api/rpgsvc/v3/rpg/banks";
            
            _logger.LogInformation("[OUTBOUND-{RequestId}] GetActiveBanksAsync: POST {Url}", requestId, requestUrl);
            
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] GetActiveBanksAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            return new { Status = response.IsSuccessStatusCode ? "SUCCESS" : "ERROR", Data = JsonSerializer.Deserialize<object>(responseContent) ?? new object() };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] GetActiveBanksAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            throw;
        }
    }

    public async Task<dynamic> ActivateMandateAsync(RemitaRrrPaymentRequest request)
    {
        await _authService.SetAuthHeaderAsync(_httpClient);
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/paymentnotification";
            
            var json = JsonSerializer.Serialize(request);
            _logger.LogInformation("[OUTBOUND-{RequestId}] ActivateMandateAsync: POST {Url}", requestId, requestUrl);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] ActivateMandateAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            return new { Status = response.IsSuccessStatusCode ? "SUCCESS" : "ERROR", Data = JsonSerializer.Deserialize<object>(responseContent) ?? new object() };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] ActivateMandateAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            throw;
        }
    }

    public async Task<dynamic> GetRrrDetailsAsync(string rrr)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/rrr/{rrr}";
            
            _logger.LogInformation("[OUTBOUND-{RequestId}] GetRrrDetailsAsync: GET {Url}", requestId, requestUrl);
            
            var response = await _httpClient.GetAsync(requestUrl);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] GetRrrDetailsAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            return new { Status = response.IsSuccessStatusCode ? "SUCCESS" : "ERROR", Data = JsonSerializer.Deserialize<object>(responseContent) ?? new object() };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] GetRrrDetailsAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            throw;
        }
    }

    public async Task<dynamic> ActivateRrrPaymentAsync(RemitaRrrPaymentRequest request)
    {
        await _authService.SetAuthHeaderAsync(_httpClient);
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/pay";
            
            var json = JsonSerializer.Serialize(request);
            _logger.LogInformation("[OUTBOUND-{RequestId}] ActivateRrrPaymentAsync: POST {Url}", requestId, requestUrl);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] ActivateRrrPaymentAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            return new { Status = response.IsSuccessStatusCode ? "SUCCESS" : "ERROR", Data = JsonSerializer.Deserialize<object>(responseContent) ?? new object() };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] ActivateRrrPaymentAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            throw;
        }
    }

    public async Task<dynamic> ProcessRrrPaymentAsync(RemitaRrrPaymentRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!string.IsNullOrEmpty(request.Pin) && !string.IsNullOrEmpty(request.Username))
            {
                var isPinValid = await _pinValidationService.ValidatePinAsync(request.Username, request.Pin);
                if (!isPinValid)
                {
                    return new { status = "03", message = "Invalid PIN", data = (object?)null };
                }
            }
            
            await _authService.SetAuthHeaderAsync(_httpClient);
            var baseUrl = _configuration["Remita:BaseUrl"];
            var requestUrl = $"{baseUrl}/api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/pay";
            
            var json = JsonSerializer.Serialize(request);
            _logger.LogInformation("[OUTBOUND-{RequestId}] ProcessRrrPaymentAsync: POST {Url}", requestId, requestUrl);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            
            _logger.LogInformation("[INBOUND-{RequestId}] ProcessRrrPaymentAsync: Status={StatusCode} | Duration={Duration}ms | Response: {Response}", 
                requestId, response.StatusCode, duration, responseContent);
            
            var result = JsonSerializer.Deserialize<object>(responseContent) ?? new object();
            
            if (response.IsSuccessStatusCode)
            {
                var settlementResult = await _settlementService.ProcessSettlementAsync(
                    request.Rrr,
                    request.AccountNumber,
                    request.Amount,
                    $"Remita RRR Payment - {request.Rrr}",
                    "REMITA");
                _logger.LogInformation("[SETTLEMENT-{RequestId}] Settlement result: {Status} - {Message}", requestId, settlementResult.ResponseStatus, settlementResult.ResponseMessage);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
            _logger.LogError(ex, "[ERROR-{RequestId}] ProcessRrrPaymentAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
            throw;
        }
    }
}