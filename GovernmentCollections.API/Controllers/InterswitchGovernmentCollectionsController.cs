using GovernmentCollections.Domain.DTOs.Interswitch;
using GovernmentCollections.Service.Services.InterswitchGovernmentCollections;
using GovernmentCollections.Shared.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Channels;

namespace GovernmentCollections.API.Controllers;

[ApiController]
[Route("api/v1/interswitch/")]
public class InterswitchGovernmentCollectionsController : BaseController
{
    private readonly IInterswitchGovernmentCollectionsService _interswitchService;
    private readonly IPinValidationService _pinValidationService;
    private readonly ILogger<InterswitchGovernmentCollectionsController> _logger;

    public InterswitchGovernmentCollectionsController(
        IInterswitchGovernmentCollectionsService interswitchService,
        IPinValidationService pinValidationService,
        ILogger<InterswitchGovernmentCollectionsController> logger)
    {
        _interswitchService = interswitchService;
        _pinValidationService = pinValidationService;
        _logger = logger;
    }

    [HttpGet("government-collections/billers")]
    public async Task<IActionResult> GetGovernmentBillers()
    {
        _logger.LogInformation("GetGovernmentBillers called from OmniChannel");
        
        var billers = await GetBillersWithChannelAsync();
        var response = new { 
            Status = "SUCCESS", 
            Message = "Government billers retrieved successfully",
            Data = billers,
            Count = billers.Count
        };
        
        _logger.LogInformation("Returning {Count} government billers to OmniChannel", billers.Count);
        return Ok(response);
    }



    [HttpGet("government-collections/billers/{categoryId}")]
    public async Task<IActionResult> GetBillersByCategory(int categoryId)
    {
        _logger.LogInformation("GetBillersByCategory called - CategoryId: {CategoryId}", categoryId);
        
        var billers = await GetBillersByCategoryWithChannelAsync(categoryId);
        var response = new { 
            Status = "SUCCESS", 
            Message = "Billers retrieved successfully",
            Data = billers,
            Count = billers.Count
        };
        
        _logger.LogInformation("Returning {Count} billers to OmniChannel", billers.Count);
        return Ok(response);
    }

    [HttpGet("government-collections/billers/{categoryId}/{serviceId}")]
    public async Task<IActionResult> GetBillersWithServiceOptions(int categoryId, int serviceId)
    {
        _logger.LogInformation("GetBillersWithServiceOptions called - CategoryId: {CategoryId}, ServiceId: {ServiceId}", categoryId, serviceId);
        
        var billers = await _interswitchService.GetBillersByCategoryAsync(categoryId);
        var serviceOptions = await GetServiceOptionsData(serviceId);
        
        var response = new { 
            Status = "SUCCESS", 
            Message = "Billers and service options retrieved successfully",
            Data = billers,
            Count = billers.Count,
            ServiceOptions = serviceOptions
        };
        
        _logger.LogInformation("Returning {Count} billers with service options to OmniChannel", billers.Count);
        return Ok(response);
    }
    
    private async Task<object> GetServiceOptionsData(int serviceId)
    {
        var paymentItems = await _interswitchService.GetServiceOptionsAsync(serviceId);
        return new {
            PaymentItems = paymentItems,
            ResponseCode = "90000",
            ResponseCodeGrouping = "SUCCESSFUL"
        };
    }

    private async Task<Dictionary<int, object>> GetAllServiceOptionsAsync(IEnumerable<int> serviceIds)
    {
        var results = new Dictionary<int, object>();
        var channel = Channel.CreateUnbounded<(int ServiceId, object Options)>();
        var writer = channel.Writer;
        var reader = channel.Reader;
        
        var tasks = serviceIds.Select(async serviceId =>
        {
            var options = await GetServiceOptionsData(serviceId);
            await writer.WriteAsync((serviceId, options));
        });
        
        var readerTask = Task.Run(async () =>
        {
            await foreach (var (serviceId, options) in reader.ReadAllAsync())
            {
                results[serviceId] = options;
            }
        });
        
        await Task.WhenAll(tasks);
        writer.Complete();
        await readerTask;
        
        return results;
    }

    private async Task<List<InterswitchBiller>> GetBillersWithChannelAsync()
    {
        var channel = Channel.CreateUnbounded<InterswitchBiller>();
        var writer = channel.Writer;
        var reader = channel.Reader;
        var results = new List<InterswitchBiller>();
        
        var task = Task.Run(async () =>
        {
            var billers = await _interswitchService.GetGovernmentBillersAsync();
            foreach (var biller in billers)
                await writer.WriteAsync(biller);
            writer.Complete();
        });
        
        var readerTask = Task.Run(async () =>
        {
            await foreach (var biller in reader.ReadAllAsync())
                results.Add(biller);
        });
        
        await Task.WhenAll(task, readerTask);
        return results;
    }

    private async Task<List<InterswitchBiller>> GetBillersByCategoryWithChannelAsync(int categoryId)
    {
        var channel = Channel.CreateUnbounded<InterswitchBiller>();
        var writer = channel.Writer;
        var reader = channel.Reader;
        var results = new List<InterswitchBiller>();
        
        var task = Task.Run(async () =>
        {
            var billers = await _interswitchService.GetBillersByCategoryAsync(categoryId);
            foreach (var biller in billers)
                await writer.WriteAsync(biller);
            writer.Complete();
        });
        
        var readerTask = Task.Run(async () =>
        {
            await foreach (var biller in reader.ReadAllAsync())
                results.Add(biller);
        });
        
        await Task.WhenAll(task, readerTask);
        return results;
    }

    private async Task<List<InterswitchCategory>> GetCategoriesWithChannelAsync()
    {
        var channel = Channel.CreateUnbounded<InterswitchCategory>();
        var writer = channel.Writer;
        var reader = channel.Reader;
        var results = new List<InterswitchCategory>();
        
        var task = Task.Run(async () =>
        {
            var categories = await _interswitchService.GetGovernmentCategoriesAsync();
            foreach (var category in categories)
                await writer.WriteAsync(category);
            writer.Complete();
        });
        
        var readerTask = Task.Run(async () =>
        {
            await foreach (var category in reader.ReadAllAsync())
                results.Add(category);
        });
        
        await Task.WhenAll(task, readerTask);
        return results;
    }

    [HttpGet("government-collections/categories")]
    public async Task<IActionResult> GetGovernmentCategories()
    {
        var categories = await GetCategoriesWithChannelAsync();
        return Ok(new { 
            Status = "SUCCESS", 
            Message = "Government categories retrieved successfully",
            Data = categories,
            Count = categories.Count
        });
    }



    [HttpGet("transaction-history")]
    public async Task<IActionResult> GetTransactionHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("customerid")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { Status = "ERROR", Message = "User not authenticated" });
        
        var history = await _interswitchService.GetTransactionHistoryAsync(userId, page, pageSize);
        return Ok(new { 
            Status = "SUCCESS", 
            Message = "Transaction history retrieved successfully",
            Data = history
        });
    }



    [HttpGet("government-collections/billers/{categoryId}/service-options")]
    public async Task<IActionResult> GetAllServiceOptionsForCategory(int categoryId)
    {
        _logger.LogInformation("GetAllServiceOptionsForCategory called - CategoryId: {CategoryId}", categoryId);
        
        var billers = await GetBillersByCategoryWithChannelAsync(categoryId);
        var serviceIds = billers.Select(b => b.Id).Distinct();
        var serviceOptions = await GetAllServiceOptionsAsync(serviceIds);
        
        var response = new { 
            Status = "SUCCESS", 
            Message = "All service options for category retrieved successfully",
            Data = billers,
            Count = billers.Count,
            ServiceOptions = serviceOptions
        };
        
        _logger.LogInformation("Returning {Count} billers with all service options for category {CategoryId}", billers.Count, categoryId);
        return Ok(response);
    }

    [HttpGet("service-options/{serviceId}")]
    public async Task<IActionResult> GetServiceOptions(int serviceId)
    {
        _logger.LogInformation("GetServiceOptions called - ServiceId: {ServiceId}", serviceId);
        
        var response = await GetServiceOptionsData(serviceId);
        
        _logger.LogInformation("Returning service options to OmniChannel");
        return Ok(response);
    }

    [HttpPost("process-transaction")]
    public async Task<IActionResult> ProcessTransaction([FromBody] InterswitchTransactionRequest request)
    {
        _logger.LogInformation("ProcessTransaction called from OmniChannel - RequestRef: {RequestReference}, Amount: {Amount}", request?.RequestReference, request?.Amount);
        
        if (request == null)
        {
            _logger.LogWarning("ProcessTransaction - Request is null");
            return BadRequest(new { Status = "ERROR", Message = "Request cannot be null" });
        }
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ProcessTransaction - Invalid model state: {@ModelState}", ModelState);
            return BadRequest(ModelState);
        }

        // Validate PIN first
        if (!string.IsNullOrEmpty(request.Pin) && !string.IsNullOrEmpty(request.Username))
        {
            _logger.LogInformation("Validating PIN for user: {Username}", request.Username);
            
            var pinResult = await _pinValidationService.ValidatePinWithResultAsync(request.Username, request.Pin);
            if (!pinResult.IsValid)
            {
                var message = pinResult.ErrorType == PinValidationErrorType.InvalidPin ? "Invalid PIN" : "PIN validation failed";
                var responseCode = pinResult.ErrorType == PinValidationErrorType.InvalidPin ? "01" : "99";
                
                _logger.LogWarning("{Message} for user: {Username}", message, request.Username);
                return Ok(new { 
                    Status = "03", 
                    Message = message, 
                    Data = new { responseCode = responseCode, responseDescription = message }
                });
            }
            
            // Use same 2FA logic as Remita - check enforcement
            var isFullyValid = await _pinValidationService.ValidateWithEnforcementAsync(request.Username, request.Pin, request.SecondFa, request.SecondFaType);
            if (!isFullyValid)
            {
                // PIN is valid but 2FA failed
                var errorMessage = string.Equals(request.SecondFaType, "TOKEN", StringComparison.OrdinalIgnoreCase) 
                    ? "Invalid or expired TOKEN" 
                    : "Invalid or expired OTP";
                _logger.LogWarning("2FA validation failed for user: {Username}, Type: {SecondFaType}", request.Username, request.SecondFaType);
                return Ok(new { 
                    Status = "05", 
                    Message = errorMessage, 
                    Data = new { responseCode = "05", responseDescription = errorMessage }
                });
            }
            
            _logger.LogInformation("Validation successful for user: {Username}", request.Username);
        }

        var result = await _interswitchService.ProcessTransactionAsync(request);
        var response = new { 
            Status = "SUCCESS", 
            Message = "Transaction completed",
            Data = result
        };
        
        _logger.LogInformation("Returning transaction result to OmniChannel - RequestRef: {RequestReference}", request.RequestReference);
        return Ok(response);
    }

    [HttpGet("transaction-status/{requestReference}")]
    public async Task<IActionResult> GetTransactionStatus(string requestReference)
    {
        if (string.IsNullOrEmpty(requestReference))
            return BadRequest(new { Status = "ERROR", Message = "Request reference is required" });

        var result = await _interswitchService.GetTransactionStatusAsync(requestReference);
        
        return Ok(new { 
            Status = "SUCCESS", 
            Message = "Transaction status retrieved successfully",
            Data = result
        });
    }



    [HttpPost("government-collections/validate-customer")]
    public async Task<IActionResult> ValidateCustomer([FromBody] CustomerValidationRequest request)
    {
        _logger.LogInformation("ValidateCustomer endpoint called from OmniChannel - CustomerId: {CustomerId}, PaymentCode: {PaymentCode}", request?.CustomerId, request?.PaymentCode);
        
        if (request == null)
        {
            _logger.LogWarning("ValidateCustomer - Request is null");
            return BadRequest(new { Status = "ERROR", Message = "Request cannot be null" });
        }
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ValidateCustomer - Invalid model state: {@ModelState}", ModelState);
            return BadRequest(ModelState);
        }

        try
        {
            var batchRequest = new InterswitchCustomerValidationBatchRequest
            {
                Customers = new List<InterswitchCustomerInfo>
                {
                    new InterswitchCustomerInfo
                    {
                        CustomerId = request.CustomerId,
                        PaymentCode = request.PaymentCode
                    }
                }
            };

            var result = await _interswitchService.ValidateCustomersAsync(batchRequest);

            
            // Check if validation actually failed
            if (result?.ResponseCodeGrouping == "FAILED")
            {
                var errorResponse = new { 
                    Status = "FAILED", 
                    Message = "Customer validation failed",
                    Data = result
                };
                _logger.LogInformation("Returning validation failure to OmniChannel - CustomerId: {CustomerId}, ResponseCode: {ResponseCode}", request.CustomerId, result?.ResponseCode);
                return BadRequest(errorResponse);
            }
            
            var response = new { 
                Status = "SUCCESS", 
                Message = "Customer validation completed",
                Data = result
            };
            
            _logger.LogInformation("Returning validation result to OmniChannel - CustomerId: {CustomerId}", request.CustomerId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateCustomer: {Message}", ex.Message);
            throw;
        }
    }

}