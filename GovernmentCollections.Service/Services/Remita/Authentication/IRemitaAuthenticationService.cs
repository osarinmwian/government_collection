namespace GovernmentCollections.Service.Services.Remita.Authentication;

public interface IRemitaAuthenticationService
{
    Task<string> GetAccessTokenAsync();
    Task<string> GetAccessTokenForBaseUrl2Async();
    Task SetAuthHeaderAsync(HttpClient httpClient);
    Task SetAuthHeaderForBaseUrl2Async(HttpClient httpClient);
}