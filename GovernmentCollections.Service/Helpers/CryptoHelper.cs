using System.Text.Json;
using System.Text;
using System.Security.Cryptography;

namespace GovernmentCollections.Service.Helpers;

public static class CryptoHelper
{
    public static string EncryptJson(JsonElement text, string key)
    {
        string stringified;

        if (text.ValueKind == JsonValueKind.String)
        {
            var s = text.GetString();
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("JSON string is empty.");

            try
            {
                using var doc = JsonDocument.Parse(s, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                stringified = doc.RootElement.GetRawText();
            }
            catch (JsonException)
            {
                stringified = JsonSerializer.Serialize(s);
            }
        }
        else
        {
            stringified = text.GetRawText();
        }

        return stringified.EncryptWithSecreteKey(key);
    }

    public static JsonElement DecryptJson(string cipherText, string key)
    {
        var normalized = CryptoService.NormalizeBase64(cipherText);
        var plain = normalized.DecryptWithSecreteKey(key);

        var parseOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        try
        {
            using var doc = JsonDocument.Parse(plain, parseOptions);
            var root = doc.RootElement.Clone();

            if (root.ValueKind == JsonValueKind.String)
            {
                var inner = root.GetString();
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    try
                    {
                        using var innerDoc = JsonDocument.Parse(inner, parseOptions);
                        root = innerDoc.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        // keep as string element
                    }
                }
            }

            return root;
        }
        catch (JsonException)
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(plain)).RootElement.Clone();
        }
    }
}

public static class CryptoService
{
    public static string NormalizeBase64(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Replace('-', '+').Replace('_', '/');
    }
}

public static class CryptoExtensions
{
    public static string EncryptWithSecreteKey(this string text, string key)
    {
        int KeySize = 32;  // 256-bit key size
        int IvSize = 16;   // AES block size (128 bits)
        int SaltSize = 16; // Recommended salt size (128 bits)
        byte[] salt = GenerateRandomBytes(SaltSize);
        byte[] keyBytes = GetFixedSizeBytes(key, KeySize, salt);
        byte[] iv = GenerateRandomBytes(IvSize); // Random IV for every encryption

        using var aesAlg = Aes.Create();
        aesAlg.Key = keyBytes;
        aesAlg.IV = iv;

        var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(text);
        }

        byte[] encryptedBytes = msEncrypt.ToArray();

        // Combine salt, IV, and encrypted data
        byte[] result = new byte[SaltSize + IvSize + encryptedBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
        Buffer.BlockCopy(encryptedBytes, 0, result, SaltSize + IvSize, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string DecryptWithSecreteKey(this string encryptedText, string key)
    {
        int KeySize = 32;
        int IvSize = 16;
        int SaltSize = 16;

        byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

        if (encryptedBytes.Length < (SaltSize + IvSize))
            throw new ArgumentException("Invalid encrypted payload length");

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];
        Buffer.BlockCopy(encryptedBytes, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(encryptedBytes, SaltSize, iv, 0, IvSize);

        byte[] keyBytes = GetFixedSizeBytes(key, KeySize, salt);

        int encryptedDataStartIndex = SaltSize + IvSize;
        int encryptedDataLength = encryptedBytes.Length - encryptedDataStartIndex;
        byte[] actualEncryptedData = new byte[encryptedDataLength];
        Buffer.BlockCopy(encryptedBytes, encryptedDataStartIndex, actualEncryptedData, 0, encryptedDataLength);

        using var aesAlg = Aes.Create();
        aesAlg.Key = keyBytes;
        aesAlg.IV = iv;

        var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

        using var msDecrypt = new MemoryStream(actualEncryptedData);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }

    private static byte[] GenerateRandomBytes(int size)
    {
        byte[] bytes = new byte[size];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    private static byte[] GetFixedSizeBytes(string input, int size, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(input, salt, 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(size);
    }
}