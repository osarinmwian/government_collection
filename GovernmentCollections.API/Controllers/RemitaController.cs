using GovernmentCollections.Domain.DTOs.Remita;
using GovernmentCollections.Service.Services.Remita;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GovernmentCollections.API.Controllers;

[ApiController]
public class RemitaController : ControllerBase
{
    private readonly IRemitaService _remitaService;
    private readonly ILogger<RemitaController> _logService;

    public RemitaController(IRemitaService remitaService, ILogger<RemitaController> logger)
    {
        _remitaService = remitaService;
        _logService = logger;
    }

    private IActionResult HandleServiceResponse(dynamic response)
    {
        if (response?.status != null && response.status?.ToString() != "00")
            return BadRequest(response);
        return Ok(response);
    }

    [HttpGet("api/v1/send/api/bgatesvc/v3/billpayment/billers")]
    public async Task<IActionResult> GetBillers()
    {
        _logService.LogInformation("GetBillers endpoint called from OmniChannel");
        var result = await _remitaService.GetBillersAsync();
        _logService.LogInformation($"Returning {result?.Count ?? 0} billers to OmniChannel");
        return HandleServiceResponse(new { status = "00", message = "Request processed successfully", data = result });
    }

    [HttpGet("api/v1/send/api/bgatesvc/v3/billpayment/biller/{billerId}/products")]
    public async Task<IActionResult> GetBillerProducts(string billerId)
    {
        if (string.IsNullOrEmpty(billerId)) 
            return BadRequest(new { status = "01", message = "BillerId is required", data = (object?)null });
        
        var result = await _remitaService.GetBillerByIdAsync(billerId);
        return Ok(result);
    }

    [HttpPost("api/v1/send/api/bgatesvc/v3/billpayment/biller/customer/validation")]
    public async Task<IActionResult> ValidateCustomer([FromBody] RemitaValidateCustomerRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(new { status = "01", message = "Invalid request", data = (object?)null });
        
        var result = await _remitaService.ValidateCustomerAsync(request);
        return Ok(result);
    }

    [HttpPost("api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/initiate")]
    public async Task<IActionResult> InitiateTransaction([FromBody] RemitaTransactionInitiateDto request)
    {
        if (request == null)
            return BadRequest(new { status = "01", message = "Request body is required", data = (object?)null });
            
        if (!ModelState.IsValid) 
            return BadRequest(new { status = "01", message = "Invalid request", data = ModelState });

        try
        {
            var result = await _remitaService.InitiateTransactionAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logService.LogError(ex, "Failed to initiate Remita transaction");
            return Ok(new { responseData = "{}", responseCode = 500, responseMsg = $"Authentication failed: {ex.Message}" });
        }
    }

    [HttpPost("api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/paymentnotification")]
    public async Task<IActionResult> ProcessPaymentNotification([FromBody] RemitaPaymentNotificationDto request)
    {
        if (request == null)
            return BadRequest(new { status = "01", message = "Request body is required", data = (object?)null });
            
        if (!ModelState.IsValid) 
            return BadRequest(new { status = "01", message = "Invalid request", data = ModelState });
        
        if (string.IsNullOrEmpty(request.Rrr))
            return BadRequest(new { status = "01", message = "RRR is required", data = (object?)null });

        if (string.IsNullOrEmpty(request.DebitAccountNumber))
            return BadRequest(new { status = "01", message = "DebitAccountNumber is required", data = (object?)null });

        var result = await _remitaService.ProcessPaymentNotificationAsync(request);
        return Ok(result);
    }

    [HttpPost("api/v1/send/api/bgatesvc/v3/billpayment/initiate-payment")]
    public async Task<IActionResult> InitiatePayment([FromBody] RemitaInitiatePaymentDto request)
    {
        if (request == null)
            return BadRequest(new { status = "01", message = "Request body is required", data = (object?)null });

        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        if (string.IsNullOrEmpty(request.AccountNumber))
            return BadRequest(new { status = "01", message = "AccountNumber is required", data = (object?)null });
        
        var result = await _remitaService.InitiatePaymentAsync(request);
        return Ok(result);
    }

    [HttpGet("api/v1/send/api/bgatesvc/v3/billpayment/transactionId/{transactionId}")]
    public async Task<IActionResult> TransactionStatus(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId)) 
            return BadRequest(new { Status = "ERROR", Message = "TransactionId is required" });
        
        var result = await _remitaService.GetTransactionStatusAsync(transactionId);
        return Ok(result);
    }

    [HttpGet("api/v1/send/api/bgatesvc/v3/billpayment/transaction-status/{transactionRef}")]
    public async Task<IActionResult> QueryTransaction(string transactionRef)
    {
        if (string.IsNullOrEmpty(transactionRef))
            return BadRequest(new { status = "01", message = "Transaction reference is required", data = (object?)null });
        
        _logService.LogInformation("QueryTransaction endpoint called for transactionRef: {TransactionRef}", transactionRef);
        
        var result = await _remitaService.QueryTransactionAsync(transactionRef);
        
        var resultString = result?.ToString() ?? "null";
        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(_logService, "QueryTransaction result: {Result}", resultString);
        
        return Ok(result);
    }


    [HttpPost("api/v1/send/api/bgatesvc/v3/billpayment/biller/activate/rrr/transaction/mandate")]
    public async Task<IActionResult> RrrActivateMandate([FromBody] RemitaRrrPaymentRequest request)
    {
        if (request == null)
            return BadRequest(new { status = "01", message = "Request body is required", data = (object?)null });
            
        if (string.IsNullOrEmpty(request.Rrr))
            return BadRequest(new { status = "01", message = "RRR is required", data = (object?)null });

        if (string.IsNullOrEmpty(request.AccountNumber))
            return BadRequest(new { status = "01", message = "AccountNumber is required", data = (object?)null });
        
        var result = await _remitaService.ActivateMandateAsync(request);
        return Ok(result);
    }

    [HttpGet("api/v1/send/api/bgatesvc/v3/billpayment/biller/transaction/lookup/{rrr}")]
    public async Task<IActionResult> GetRrrDetails(string rrr)
    {
        if (string.IsNullOrEmpty(rrr))
            return BadRequest(new { status = "01", message = "RRR is required", data = (object?)null });
        
        var result = await _remitaService.GetRrrDetailsAsync(rrr);
        return Ok(result);
    }

    [HttpPost("api/v1/send/api/bgatesvc/v3/billpayment/biller/rrr/transaction/payment")]
    public async Task<IActionResult> RrrTransactionPay([FromBody] RemitaRrrPaymentRequest request)
    {
        if (request == null)
            return BadRequest(new { status = "01", message = "Request body is required", data = (object?)null });
            
        if (string.IsNullOrEmpty(request.Rrr))
            return BadRequest(new { status = "01", message = "RRR is required", data = (object?)null });

        if (string.IsNullOrEmpty(request.AccountNumber))
            return BadRequest(new { status = "01", message = "AccountNumber is required", data = (object?)null });
        
        var result = await _remitaService.ProcessRrrPaymentAsync(request);
        return Ok(result);
    }


    [HttpGet("api/remita/banks")]
    public async Task<IActionResult> GetActiveBanks()
    {
        var result = await _remitaService.GetActiveBanksAsync();
        return Ok(result);
    }


}