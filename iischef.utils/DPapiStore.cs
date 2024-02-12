using System;
using System.Security.Cryptography;

public static class DPapiStore
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    public static string Encode(string data, DataProtectionScope scope = DataProtectionScope.LocalMachine)
    {
        byte[] secretData = System.Text.Encoding.UTF8.GetBytes(data);
        byte[] encryptedData = ProtectedData.Protect(secretData, null, scope);
        return Convert.ToBase64String(encryptedData);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="encodedData"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    public static string Decode(string encodedData, DataProtectionScope scope = DataProtectionScope.LocalMachine)
    {
        byte[] secretData = Convert.FromBase64String(encodedData);
        byte[] decryptedData = ProtectedData.Unprotect(secretData, null, scope);
        return System.Text.Encoding.UTF8.GetString(decryptedData);
    }
}