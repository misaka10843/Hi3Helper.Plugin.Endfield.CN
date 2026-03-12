using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Hi3Helper.Plugin.Endfield.Utils;

public static class ConfigTool
{
    // 逆向获取的AES-256-CBC密钥
    private static readonly byte[] AesKey = new byte[]
    {
        0xC0, 0xF3, 0x0E, 0x1C, 0xE7, 0x63, 0xBB, 0xC2, 0x1C, 0xC3, 0x55, 0xA3, 0x43, 0x03, 0xAC, 0x50,
        0x39, 0x94, 0x44, 0xBF, 0xF6, 0x8C, 0x4A, 0x22, 0xAF, 0x39, 0x8C, 0x0A, 0x16, 0x6E, 0xE1, 0x43
    };

    // 逆向获取的IV
    private static readonly byte[] AesIv = new byte[]
    {
        0x33, 0x46, 0x78, 0x61, 0x19, 0x27, 0x50, 0x64, 0x95, 0x01, 0x93, 0x72, 0x64, 0x60, 0x84, 0x00
    };

    public static string ReadConfig(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;

        try
        {
            var fileBytes = File.ReadAllBytes(filePath);
            
            using var aes = Aes.Create();
            aes.Key = AesKey;
            aes.IV = AesIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(fileBytes, 0, fileBytes.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string? ParseVersion(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("version="))
            {
                return trimmed.Split('=')[1].Trim();
            }
        }

        return null;
    }
}