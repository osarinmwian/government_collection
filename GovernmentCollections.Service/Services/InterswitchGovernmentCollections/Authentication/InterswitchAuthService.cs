using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using GovernmentCollections.Domain.DTOs.Interswitch;
using GovernmentCollections.Domain.Settings;

namespace GovernmentCollections.Service.Services.InterswitchGovernmentCollections;

public class InterswitchAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InterswitchAuthService> _logger;
    private readonly IMemoryCache _cache;
    private readonly InterswitchSettings _settings;
    private const string TOKEN_CACHE_KEY = "interswitch_token";

    public InterswitchAuthService(
        HttpClient httpClient,
        ILogger<InterswitchAuthService> logger,
        IMemoryCache cache,
        IOptions<InterswitchSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<InterswitchAuthResponse> AuthenticateAsync()
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(1);
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var requestUrl = _settings.BaseUrl;
                
                _logger.LogInformation("[OUTBOUND-{RequestId}] AuthenticateAsync: POST {Url} | BaseUrl={BaseUrl} | UserName={UserName} | Attempt={Attempt}", 
                    requestId, requestUrl, _settings.BaseUrl, _settings.UserName, attempt + 1);
                
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "PostmanRuntime/7.32.3");
                request.Headers.Add("Connection", "keep-alive");
                
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.UserName}:{_settings.Password}"));
                request.Headers.Add("Authorization", $"Basic {credentials}");
                
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "client_credentials")
                };
                request.Content = new FormUrlEncodedContent(formData);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
                
                _logger.LogInformation("[INBOUND-{RequestId}] AuthenticateAsync: Status={StatusCode} | Duration={Duration}ms", 
                    requestId, response.StatusCode, duration);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[ERROR-{RequestId}] Authentication failed. Status: {StatusCode} | Response: {Response}", 
                        requestId, response.StatusCode, responseContent);
                    response.EnsureSuccessStatusCode();
                }

                var authResponse = JsonSerializer.Deserialize<InterswitchAuthResponse>(responseContent);

                if (authResponse != null && !string.IsNullOrEmpty(authResponse.AccessToken))
                {
                    _logger.LogInformation("[SUCCESS-{RequestId}] Token received, expires in: {ExpiresIn} seconds", requestId, authResponse.ExpiresIn);
                    _logger.LogInformation("[AUTH-RESPONSE-{RequestId}] Full response: {Response}", requestId, responseContent);
                    
                    if (!string.IsNullOrEmpty(authResponse.TerminalId))
                    {
                        _logger.LogInformation("[AUTH-RESPONSE-{RequestId}] TerminalId from response: {TerminalId}", requestId, authResponse.TerminalId);
                    }
                    else
                    {
                        _logger.LogWarning("[AUTH-RESPONSE-{RequestId}] No TerminalId in response", requestId);
                    }
                    
                    var cacheExpiry = TimeSpan.FromSeconds(authResponse.ExpiresIn - _settings.TokenExpiryBuffer);
                    _cache.Set(TOKEN_CACHE_KEY, authResponse, cacheExpiry);
                    
                    return authResponse;
                }

                throw new InvalidOperationException("Authentication failed - no access token received");
            }
            catch (HttpRequestException ex) when ((ex.Message.Contains("forcibly closed") || ex.Message.Contains("connection was closed")) && attempt < maxRetries - 1)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                _logger.LogWarning("[RETRY-{RequestId}] Network connection error on attempt {Attempt}, retrying in {Delay}ms. Error: {Error}", 
                    requestId, attempt + 1, delay.TotalMilliseconds, ex.Message);
                await Task.Delay(delay);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < maxRetries - 1)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                _logger.LogWarning("[RETRY-{RequestId}] Timeout on attempt {Attempt}, retrying in {Delay}ms. Error: {Error}", 
                    requestId, attempt + 1, delay.TotalMilliseconds, ex.Message);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
                _logger.LogError(ex, "[ERROR-{RequestId}] AuthenticateAsync: Duration={Duration}ms | Exception: {Message}", requestId, duration, ex.Message);
                throw;
            }
        }
        
        throw new InvalidOperationException($"Authentication failed after {maxRetries} attempts");
    }

    public Task<bool> IsTokenValidAsync()
    {
        return Task.FromResult(_cache.TryGetValue(TOKEN_CACHE_KEY, out _));
    }

    public async Task<string> GetValidTokenAsync()
    {
        _logger.LogInformation("[TOKEN] Checking for cached token...");
        
        if (_cache.TryGetValue(TOKEN_CACHE_KEY, out InterswitchAuthResponse? cachedAuth) && cachedAuth != null)
        {
            _logger.LogInformation("[TOKEN] Using cached token");
            return cachedAuth.AccessToken;
        }

        _logger.LogInformation("[TOKEN] No cached token found, authenticating...");
        var authResponse = await AuthenticateAsync();
        return authResponse.AccessToken;
    }

    public Task<string> GetTerminalIdAsync()
    {
        _logger.LogInformation("[TERMINAL] Getting terminal ID...");
        
        if (_cache.TryGetValue(TOKEN_CACHE_KEY, out InterswitchAuthResponse? cachedAuth) && cachedAuth != null)
        {
            if (!string.IsNullOrEmpty(cachedAuth.TerminalId))
            {
                _logger.LogInformation("[TERMINAL] Using terminal ID from response: {TerminalId}", cachedAuth.TerminalId);
                return Task.FromResult(cachedAuth.TerminalId);
            }
        }

        _logger.LogWarning("[TERMINAL] No terminal ID found");
        return Task.FromResult(string.Empty);
    }

    public void ClearCachedToken()
    {
        _logger.LogInformation("[TOKEN] Clearing cached token...");
        _cache.Remove(TOKEN_CACHE_KEY);
    }
}