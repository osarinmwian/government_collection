namespace GovernmentCollections.Domain.Settings;

public class RevPaySettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class RemitaSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class InterswitchSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ServicesUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string MerchantCode { get; set; } = string.Empty;
    public string RequestorId { get; set; } = string.Empty;
    public string TerminalId { get; set; } = string.Empty;
    public string PayableId { get; set; } = string.Empty;
    public string InstitutionId { get; set; } = string.Empty;
    public int TokenExpiryBuffer { get; set; } = 300;
}

public class BuyPowerSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}