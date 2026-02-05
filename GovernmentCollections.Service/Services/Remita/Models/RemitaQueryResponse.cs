using System.Text.Json.Serialization;

namespace GovernmentCollections.Service.Services.Remita.Models;

public class RemitaQueryResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public RemitaTransactionData? Data { get; set; }
}

public class RemitaTransactionData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("transactionRef")]
    public string TransactionRef { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("rrr")]
    public string Rrr { get; set; } = string.Empty;

    [JsonPropertyName("paid")]
    public bool Paid { get; set; }

    [JsonPropertyName("metadata")]
    public RemitaTransactionMetadata? Metadata { get; set; }
}

public class RemitaTransactionMetadata
{
    [JsonPropertyName("transactionDate")]
    public string TransactionDate { get; set; } = string.Empty;

    [JsonPropertyName("paymentDate")]
    public string PaymentDate { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("receiptUrl")]
    public string ReceiptUrl { get; set; } = string.Empty;

    [JsonPropertyName("extraData")]
    public object? ExtraData { get; set; }

    [JsonPropertyName("vendingData")]
    public object? VendingData { get; set; }

    [JsonPropertyName("customFields")]
    public List<RemitaCustomField>? CustomFields { get; set; }

    [JsonPropertyName("standingOrder")]
    public object? StandingOrder { get; set; }
}

public class RemitaCustomField
{
    [JsonPropertyName("variable_name")]
    public string VariableName { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}