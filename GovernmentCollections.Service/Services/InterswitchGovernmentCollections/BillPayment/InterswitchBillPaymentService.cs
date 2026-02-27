using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net.Http.Json;
using GovernmentCollections.Domain.DTOs.Interswitch;
using GovernmentCollections.Domain.DTOs.Settlement;
using GovernmentCollections.Domain.Settings;
using GovernmentCollections.Service.Services.Settlement;

namespace GovernmentCollections.Service.Services.InterswitchGovernmentCollections.BillPayment;

public class InterswitchBillPaymentService : IInterswitchBillPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InterswitchBillPaymentService> _logger;
    private readonly IMemoryCache _cache;
    private readonly InterswitchSettings _settings;
    private readonly InterswitchAuthService _authService;
    private readonly ISettlementService _settlementService;
    private const string BILLERS_CACHE_KEY = "interswitch_government_billers";

    public InterswitchBillPaymentService(
        HttpClient httpClient,
        ILogger<InterswitchBillPaymentService> logger,
        IMemoryCache cache,
        IOptions<InterswitchSettings> settings,
        InterswitchAuthService authService,
        ISettlementService settlementService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
        _authService = authService;
        _settlementService = settlementService;
    }

    public async Task<InterswitchServicesResponse> GetGovernmentCategoriesAsync()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;
        var maxRetries = 3;
        var retryCount = 0;
        var baseDelay = TimeSpan.FromSeconds(2);

        while (retryCount < maxRetries)
        {
            try
            {
                var token = await _authService.GetValidTokenAsync();
                var terminalId = await _authService.GetTerminalIdAsync();
                var requestUrl = $"{_settings.ServicesUrl}/api/v5/services/categories";

                _logger.LogInformation("[DEBUG-{RequestId}] Attempt {Attempt} | Token: {Token} | TerminalId: {TerminalId}", 
                    requestId, retryCount + 1, token?.Substring(0, Math.Min(10, token.Length)) + "...", terminalId);

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("Authorization", $"Bearer {token}");
                if (!string.IsNullOrEmpty(terminalId))
                    request.Headers.Add("TerminalId", terminalId);

                _logger.LogInformation("[HEADERS-{RequestId}] Authorization: Bearer {TokenPrefix}..., TerminalID: {TerminalId}", 
                    requestId, token?.Substring(0, Math.Min(20, token.Length)), terminalId);

                _logger.LogInformation("[OUTBOUND-{RequestId}] GetGovernmentCategoriesAsync: GET {Url}", requestId, requestUrl);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;

                _logger.LogInformation("[INBOUND-{RequestId}] GetGovernmentCategoriesAsync: Status={StatusCode} | Duration={Duration}ms",
                    requestId, response.StatusCode, duration);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("[AUTH-ERROR-{RequestId}] 401 Response: {ResponseContent}", requestId, responseContent);
                    _logger.LogError("[AUTH-ERROR-{RequestId}] Request URL: {RequestUrl}", requestId, requestUrl);
                    _logger.LogError("[AUTH-ERROR-{RequestId}] All Headers: {Headers}", requestId, string.Join(", ", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                    
                    if (retryCount < maxRetries - 1)
                    {
                        _logger.LogWarning("[RETRY-{RequestId}] 401 Unauthorized, clearing token and retrying...", requestId);
                        _authService.ClearCachedToken();
                        retryCount++;
                        await Task.Delay(TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryCount)));
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();

                var servicesResponse = JsonSerializer.Deserialize<InterswitchServicesResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("[RAW-RESPONSE-{RequestId}] Response content: {ResponseContent}", requestId, responseContent);
                _logger.LogInformation("[PARSED-RESPONSE-{RequestId}] Categories count: {Count}", requestId, servicesResponse?.BillerCategories?.Count ?? 0);
                
                _logger.LogInformation("[SUCCESS-{RequestId}] Successfully retrieved service response", requestId);
                return servicesResponse ?? new InterswitchServicesResponse();
            }
            catch (HttpRequestException ex) when ((ex.Message.Contains("401") || ex.Message.Contains("forcibly closed") || ex.Message.Contains("connection was closed")) && retryCount < maxRetries - 1)
            {
                _logger.LogWarning("[RETRY-{RequestId}] Network/Auth exception, clearing token and retrying... Error: {Error}", requestId, ex.Message);
                _authService.ClearCachedToken();
                retryCount++;
                await Task.Delay(TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryCount)));
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && retryCount < maxRetries - 1)
            {
                _logger.LogWarning("[RETRY-{RequestId}] Timeout exception, retrying... Error: {Error}", requestId, ex.Message);
                retryCount++;
                await Task.Delay(TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryCount)));
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
                _logger.LogError(ex, "[ERROR-{RequestId}] GetGovernmentCategoriesAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
                throw;
            }
        }

        throw new InvalidOperationException($"Failed to get government categories after {maxRetries} attempts");
    }

    public async Task<List<InterswitchBiller>> GetBillersByCategoryAsync(int categoryId)
    {
        var response = await GetGovernmentCategoriesAsync();
        
            _logger.LogInformation("[OUTBOUND-RESPONSE]", response);
        var category = response?.BillerCategories?.FirstOrDefault(c => c.Id == categoryId);
        return category?.Billers ?? new List<InterswitchBiller>();
    }

    public async Task<List<InterswitchBiller>> GetGovernmentBillersAsync()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        if (_cache.TryGetValue(BILLERS_CACHE_KEY, out List<InterswitchBiller>? cachedBillers) && cachedBillers != null)
        {
            return cachedBillers;
        }

        var response = await GetGovernmentCategoriesAsync();
        var governmentCategories = response.BillerCategories?
            .Where(c => c.Name.Contains("Government", StringComparison.OrdinalIgnoreCase) || 
                       c.Name.Contains("State", StringComparison.OrdinalIgnoreCase) ||
                       c.Name.Contains("Tax", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? new List<InterswitchCategory>();

        var allBillers = new List<InterswitchBiller>();
        
        foreach (var category in governmentCategories)
        {
            var categoryBillers = await GetBillersForCategoryAsync(category.Id);
            allBillers.AddRange(categoryBillers);
        }

        if (allBillers.Any())
        {
            _cache.Set(BILLERS_CACHE_KEY, allBillers, TimeSpan.FromHours(1));
        }

        return allBillers;
    }

    private async Task<List<InterswitchBiller>> GetBillersForCategoryAsync(int categoryId)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        try
        {
            var token = await _authService.GetValidTokenAsync();
            var terminalId = await _authService.GetTerminalIdAsync();
            var requestUrl = $"{_settings.ServicesUrl}/api/v5/services?categoryId={categoryId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {token}");
            if (!string.IsNullOrEmpty(terminalId))
                request.Headers.Add("TerminalId", terminalId);

            _logger.LogInformation("[OUTBOUND-{RequestId}] GetBillersForCategoryAsync: GET {Url}", requestId, requestUrl);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("[INBOUND-{RequestId}] GetBillersForCategoryAsync: Status={StatusCode}", requestId, response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[WARNING-{RequestId}] Failed to get billers for category {CategoryId}: {Response}", requestId, categoryId, responseContent);
                return new List<InterswitchBiller>();
            }

            var billersResponse = JsonSerializer.Deserialize<InterswitchServicesResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return billersResponse?.BillerCategories?.FirstOrDefault()?.Billers ?? new List<InterswitchBiller>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR-{RequestId}] GetBillersForCategoryAsync: CategoryId={CategoryId} | Exception: {Message}", requestId, categoryId, ex.Message);
            return new List<InterswitchBiller>();
        }
    }

    public async Task<List<InterswitchPaymentItem>> GetServiceOptionsAsync(int serviceId)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;

        try
        {
            var token = await _authService.GetValidTokenAsync();
            var terminalId = await _authService.GetTerminalIdAsync();

            var requestUrl = $"{_settings.ServicesUrl}/api/v5/services/options?serviceId={serviceId}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            _httpClient.DefaultRequestHeaders.Add("TerminalID", terminalId);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            // Add required Interswitch headers
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");
            
            _httpClient.DefaultRequestHeaders.Add("Timestamp", timestamp);
            _httpClient.DefaultRequestHeaders.Add("Nonce", nonce);
            _httpClient.DefaultRequestHeaders.Add("MerchantCode", _settings.MerchantCode);
            _httpClient.DefaultRequestHeaders.Add("InstitutionId", _settings.InstitutionId);
            _httpClient.DefaultRequestHeaders.Add("PayableId", _settings.PayableId);
            
            if (!string.IsNullOrEmpty(_settings.RequestorId))
                _httpClient.DefaultRequestHeaders.Add("RequestorId", _settings.RequestorId);

            _logger.LogInformation("[OUTBOUND-{RequestId}] GetServiceOptionsAsync: GET {Url}", requestId, requestUrl);

            var response = await _httpClient.GetAsync(requestUrl);
            var responseContent = await response.Content.ReadAsStringAsync();
            var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;

            var serviceOptionsResponse = JsonSerializer.Deserialize<InterswitchServiceOptionsResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!response.IsSuccessStatusCode || serviceOptionsResponse?.PaymentItems == null)
            {
                return new List<InterswitchPaymentItem>();
            }

            return serviceOptionsResponse.PaymentItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR-{RequestId}] GetServiceOptionsAsync: {Message}", requestId, ex.Message);
            throw;
        }
    }

    public async Task<InterswitchPaymentResponse> ProcessTransactionAsync(InterswitchTransactionRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;

        try
        {
            var token = await _authService.GetValidTokenAsync();
            var terminalId = await _authService.GetTerminalIdAsync();
            var requestUrl = $"{_settings.ServicesUrl}/api/v5/Transactions";

            var transactionData = new
            {
                TerminalId = terminalId,
                paymentCode = request.PaymentCode,
                customerId = request.CustomerId,
                customerMobile = request.CustomerMobile,
                customerEmail = request.CustomerEmail,
                amount = request.Amount.ToString(),
                requestReference = request.RequestReference
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            _httpClient.DefaultRequestHeaders.Add("TerminalID", terminalId);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            // Add required Interswitch headers
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");
            
            _httpClient.DefaultRequestHeaders.Add("Timestamp", timestamp);
            _httpClient.DefaultRequestHeaders.Add("Nonce", nonce);
            _httpClient.DefaultRequestHeaders.Add("MerchantCode", _settings.MerchantCode);
            _httpClient.DefaultRequestHeaders.Add("InstitutionId", _settings.InstitutionId);
            _httpClient.DefaultRequestHeaders.Add("PayableId", _settings.PayableId);
            
            if (!string.IsNullOrEmpty(_settings.RequestorId))
                _httpClient.DefaultRequestHeaders.Add("RequestorId", _settings.RequestorId);

            var response = await _httpClient.PostAsJsonAsync(requestUrl, transactionData);
            var paymentResponse = await response.Content.ReadFromJsonAsync<InterswitchPaymentResponse>() ?? new InterswitchPaymentResponse();

            if (paymentResponse.ResponseCode == "00")
            {
                await _settlementService.ProcessSettlementAsync(request.RequestReference, request.CustomerId, request.Amount, $"Interswitch - {request.PaymentCode}", "INTERSWITCH");
            }

            return paymentResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR-{RequestId}] ProcessTransactionAsync: {Message}", requestId, ex.Message);
            throw;
        }
    }

    public async Task<InterswitchPaymentResponse> GetTransactionStatusAsync(string requestReference)
    {
        var token = await _authService.GetValidTokenAsync();
        var terminalId = await _authService.GetTerminalIdAsync();
        var requestUrl = $"{_settings.ServicesUrl}/api/v5/Transactions?requestRef={requestReference}";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _httpClient.DefaultRequestHeaders.Add("TerminalID", terminalId);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        
        // Add required Interswitch headers
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        
        _httpClient.DefaultRequestHeaders.Add("Timestamp", timestamp);
        _httpClient.DefaultRequestHeaders.Add("Nonce", nonce);
        _httpClient.DefaultRequestHeaders.Add("MerchantCode", _settings.MerchantCode);
        _httpClient.DefaultRequestHeaders.Add("InstitutionId", _settings.InstitutionId);
        _httpClient.DefaultRequestHeaders.Add("PayableId", _settings.PayableId);
        
        if (!string.IsNullOrEmpty(_settings.RequestorId))
            _httpClient.DefaultRequestHeaders.Add("RequestorId", _settings.RequestorId);

        var response = await _httpClient.GetAsync(requestUrl);
        return await response.Content.ReadFromJsonAsync<InterswitchPaymentResponse>() ?? new InterswitchPaymentResponse();
    }

    public async Task<InterswitchCustomerValidationResponse> ValidateCustomersAsync(InterswitchCustomerValidationBatchRequest request)
    {
        var token = await _authService.GetValidTokenAsync();
        var terminalId = await _authService.GetTerminalIdAsync();
        var requestUrl = $"{_settings.ServicesUrl}/api/v5/Transactions/validatecustomers";

        var validationData = new { customers = request.Customers, TerminalId = terminalId };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _httpClient.DefaultRequestHeaders.Add("TerminalID", terminalId);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        
        // Add required Interswitch headers
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        
        _httpClient.DefaultRequestHeaders.Add("Timestamp", timestamp);
        _httpClient.DefaultRequestHeaders.Add("Nonce", nonce);
        _httpClient.DefaultRequestHeaders.Add("MerchantCode", _settings.MerchantCode);
        _httpClient.DefaultRequestHeaders.Add("InstitutionId", _settings.InstitutionId);
        _httpClient.DefaultRequestHeaders.Add("PayableId", _settings.PayableId);
        
        if (!string.IsNullOrEmpty(_settings.RequestorId))
            _httpClient.DefaultRequestHeaders.Add("RequestorId", _settings.RequestorId);

        var response = await _httpClient.PostAsJsonAsync(requestUrl, validationData);
        return await response.Content.ReadFromJsonAsync<InterswitchCustomerValidationResponse>() ?? new InterswitchCustomerValidationResponse();
    }
}