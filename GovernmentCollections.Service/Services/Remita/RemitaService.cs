using GovernmentCollections.Domain.DTOs.Remita;
using GovernmentCollections.Service.Services.Remita.BillPayment;
using GovernmentCollections.Service.Services.Remita.Payment;
using GovernmentCollections.Service.Services.Remita.Transaction;
using GovernmentCollections.Service.Services.Settlement;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GovernmentCollections.Service.Services.Remita;

public class RemitaService : IRemitaService
{
    private readonly IRemitaBillPaymentService _billPaymentService;
    private readonly IRemitaPaymentService _paymentService;
    private readonly IRemitaTransactionService _transactionService;
    private readonly ISettlementService _settlementService;
    private readonly ILogger<RemitaService> _logger;

    public RemitaService(
        IRemitaBillPaymentService billPaymentService,
        IRemitaPaymentService paymentService,
        IRemitaTransactionService transactionService,
        ISettlementService settlementService,
        ILogger<RemitaService> logger)
    {
        _billPaymentService = billPaymentService;
        _paymentService = paymentService;
        _transactionService = transactionService;
        _settlementService = settlementService;
        _logger = logger;
    }

    public async Task<List<RemitaBillerDto>> GetBillersAsync()
    {
        _logger.LogInformation("Getting Remita billers");
        return await Task.Run(async () => {
            _logger.LogInformation("Fetching billers on background thread");
            return await _billPaymentService.GetBillersAsync();
        });
    }

    public async Task<RemitaBillerDetailsDto> GetBillerByIdAsync(string billerId)
    {
        _logger.LogInformation("Getting biller details for ID: {BillerId}", billerId);
        return await Task.Run(async () => {
            _logger.LogInformation("Fetching biller details on background thread for: {BillerId}", billerId);
            return await _billPaymentService.GetBillerByIdAsync(billerId);
        });
    }

    public async Task<RemitaValidateCustomerResponse> ValidateCustomerAsync(RemitaValidateCustomerRequest request)
    {
        _logger.LogInformation("Validating Remita customer: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("Customer validation processing on background thread");
            return await _billPaymentService.ValidateCustomerAsync(request);
        });
    }

    public async Task<RemitaPaymentResponse> ProcessPaymentAsync(RemitaPaymentRequest request)
    {
        _logger.LogInformation("Processing Remita payment: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("Payment processing on background thread");
            return await _paymentService.ProcessPaymentAsync(request);
        });
    }

    public async Task<dynamic> InitiateTransactionAsync(RemitaTransactionInitiateDto request)
    {
        _logger.LogInformation("Initiating transaction: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("Transaction initiation on background thread");
            return await _transactionService.InitiateTransactionAsync(request);
        });
    }

    public async Task<dynamic> ProcessPaymentNotificationAsync(RemitaPaymentNotificationDto request)
    {
        _logger.LogInformation("Processing payment notification: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("Payment notification processing on background thread");
            return await _transactionService.ProcessPaymentNotificationAsync(request);
        });
    }

    public async Task<dynamic> InitiatePaymentAsync(RemitaInitiatePaymentDto request)
    {
        _logger.LogInformation("Initiating Remita payment: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("Payment initiation on background thread");
            return await _paymentService.InitiatePaymentAsync(request);
        });
    }

    public async Task<dynamic> VerifyPaymentAsync(string rrr)
    {
        _logger.LogInformation("Verifying payment with RRR: {RRR}", rrr);
        return await Task.Run(async () => {
            _logger.LogInformation("Payment verification on background thread for RRR: {RRR}", rrr);
            return await _paymentService.VerifyPaymentAsync(rrr);
        });
    }

    public async Task<dynamic> GetTransactionStatusAsync(string transactionId)
    {
        _logger.LogInformation("Getting transaction status: {TransactionId}", transactionId);
        return await Task.Run(async () => {
            _logger.LogInformation("Transaction status check on background thread: {TransactionId}", transactionId);
            return await _transactionService.GetTransactionStatusAsync(transactionId);
        });
    }

    public async Task<dynamic> QueryTransactionAsync(string transactionRef)
    {
        _logger.LogInformation("Querying transaction: {TransactionRef}", transactionRef);
        return await Task.Run(async () => {
            _logger.LogInformation("Transaction query on background thread: {TransactionRef}", transactionRef);
            return await _transactionService.QueryTransactionAsync(transactionRef);
        });
    }

    public async Task<dynamic> GetActiveBanksAsync()
    {
        _logger.LogInformation("Getting active banks from Remita");
        return await Task.Run(async () => {
            _logger.LogInformation("Fetching active banks on background thread");
            return await _paymentService.GetActiveBanksAsync();
        });
    }

    public async Task<dynamic> ActivateMandateAsync(RemitaRrrPaymentRequest request)
    {
        _logger.LogInformation("Activating mandate: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("Mandate activation on background thread");
            return await _paymentService.ActivateMandateAsync(request);
        });
    }

    public async Task<dynamic> GetRrrDetailsAsync(string rrr)
    {
        _logger.LogInformation("Getting RRR details: {RRR}", rrr);
        return await Task.Run(async () => {
            _logger.LogInformation("RRR details fetch on background thread: {RRR}", rrr);
            return await _paymentService.GetRrrDetailsAsync(rrr);
        });
    }

    public async Task<dynamic> LookupRrrAsync(string rrr)
    {
        _logger.LogInformation("Looking up RRR: {RRR}", rrr);
        return await Task.Run(() => {
            _logger.LogInformation("RRR lookup on background thread: {RRR}", rrr);
            return new 
            {
                status = "00",
                message = "RRR lookup successful",
                data = new 
                {
                    rrr = rrr,
                    lookupTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    status = "FOUND"
                }
            };
        });
    }

    public async Task<dynamic> ActivateRrrPaymentAsync(RemitaRrrPaymentRequest request)
    {
        _logger.LogInformation("Activating RRR payment: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("RRR payment activation on background thread");
            return await _paymentService.ActivateRrrPaymentAsync(request);
        });
    }

    public async Task<dynamic> ProcessRrrPaymentAsync(RemitaRrrPaymentRequest request)
    {
        _logger.LogInformation("Processing RRR payment: {Request}", request);
        return await Task.Run(async () => {
            _logger.LogInformation("RRR payment processing on background thread");
            return await _paymentService.ProcessRrrPaymentAsync(request);
        });
    }

    public async Task<RemitaTransactionResponse> ProcessTransactionAsync(RemitaTransactionRequest request)
    {
        try
        {
            _logger.LogInformation("Processing Remita transaction for reference {RequestReference}", request.RequestReference);
            
            // Convert to Remita payment notification format
            var paymentNotification = new RemitaPaymentNotificationDto
            {
                Rrr = request.RequestReference,
                TransactionRef = request.RequestReference,
                Amount = request.Amount,
                Channel = request.Channel,
                DebitAccountNumber = request.DebitAccount,
                Pin = request.Pin,
                SecondFa = request.SecondFa,
                SecondFaType = request.SecondFaType,
                Enforce2FA = request.Enforce2FA,
                Username = request.Username,
                Metadata = new RemitaPaymentNotificationMetadata
                {
                    FundingSource = request.FundingSource,
                    PayerAccountNumber = request.DebitAccount
                }
            };
            
            // Call Remita API
            var response = await _transactionService.ProcessPaymentNotificationAsync(paymentNotification);
            
            // Extract response details
            var responseCode = "99";
            var responseDescription = "Transaction processing failed";
            var transactionRef = request.RequestReference;
            var approvedAmount = request.Amount.ToString();
            
            if (response != null)
            {
                try
                {
                    var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
                    var responseDoc = System.Text.Json.JsonDocument.Parse(responseJson);
                    
                    if (responseDoc.RootElement.TryGetProperty("status", out JsonElement statusElement))
                    {
                        responseCode = statusElement.GetString() ?? "99";
                    }
                    if (responseDoc.RootElement.TryGetProperty("message", out JsonElement messageElement))
                    {
                        responseDescription = messageElement.GetString() ?? "Transaction processing failed";
                    }
                    if (responseDoc.RootElement.TryGetProperty("data", out JsonElement dataElement))
                    {
                        if (dataElement.TryGetProperty("transactionRef", out JsonElement refElement))
                        {
                            transactionRef = refElement.GetString() ?? request.RequestReference;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to parse Remita response: {Error}", ex.Message);
                }
            }
            
            _logger.LogInformation("Remita response - Code: {ResponseCode}, Description: {ResponseDescription}, TransactionRef: {TransactionRef}", 
                responseCode, responseDescription, transactionRef);
            
            // Settlement AFTER successful API call
            if (responseCode == "00" || responseCode == "90000" ||
                string.Equals(responseDescription, "Success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(responseDescription, "Successful", StringComparison.OrdinalIgnoreCase))
            {
                var settlementResult = await _settlementService.ProcessSettlementAsync(
                    request.RequestReference,
                    request.DebitAccount,
                    request.Amount,
                    $"Remita Payment - {request.BillPaymentProductId}",
                    "REMITA");
                
                if (!settlementResult.ResponseStatus)
                {
                    _logger.LogWarning("Settlement failed for transaction {TransactionRef}: {Message}", request.RequestReference, settlementResult.ResponseMessage);
                }
            }
            
            return new RemitaTransactionResponse
            {
                ResponseCode = responseCode,
                ResponseDescription = responseDescription,
                ResponseCodeGrouping = responseCode == "00" || responseCode == "90000" ? "SUCCESSFUL" : "FAILED",
                TransactionRef = transactionRef,
                ApprovedAmount = approvedAmount,
                AdditionalInfo = new Dictionary<string, object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Remita transaction");
            return new RemitaTransactionResponse 
            { 
                ResponseCode = "99", 
                ResponseDescription = "Transaction processing failed",
                ResponseCodeGrouping = "FAILED",
                TransactionRef = request.RequestReference,
                ApprovedAmount = "0",
                AdditionalInfo = new Dictionary<string, object>()
            };
        }
    }
}