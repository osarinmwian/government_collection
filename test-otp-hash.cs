using System;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main()
    {
        string otp = "123456";
        using var sha512 = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(otp);
        var hash = sha512.ComputeHash(bytes);
        
        var result = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
        {
            result.Append(hash[i].ToString("X2"));
        }
        Console.WriteLine($"OTP: {otp}");
        Console.WriteLine($"Hash: {result}");
    }
}