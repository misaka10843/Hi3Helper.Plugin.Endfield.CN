using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Hi3Helper.Plugin.Endfield.Utils;

/// <summary>
///     Provides AES-256-CBC decryption for Endfield game files (e.g., config.ini, game_files).
/// </summary>
public static class EndfieldCrypto
{
    // AES-256-CBC Key retrieved via reverse engineering
    private static readonly byte[] AesKey = new byte[]
    {
        0xC0, 0xF3, 0x0E, 0x1C, 0xE7, 0x63, 0xBB, 0xC2, 0x1C, 0xC3, 0x55, 0xA3, 0x43, 0x03, 0xAC, 0x50,
        0x39, 0x94, 0x44, 0xBF, 0xF6, 0x8C, 0x4A, 0x22, 0xAF, 0x39, 0x8C, 0x0A, 0x16, 0x6E, 0xE1, 0x43
    };

    // IV retrieved via reverse engineering
    private static readonly byte[] AesIv = new byte[]
    {
        0x33, 0x46, 0x78, 0x61, 0x19, 0x27, 0x50, 0x64, 0x95, 0x01, 0x93, 0x72, 0x64, 0x60, 0x84, 0x00
    };

    /// <summary>
    ///     Decrypts the file and returns the raw byte array.
    ///     Suitable for binary files or encrypted files with unknown formats.
    /// </summary>
    public static byte[] DecryptFileToBytes(string filePath)
    {
        if (!File.Exists(filePath)) return Array.Empty<byte>();

        var fileBytes = File.ReadAllBytes(filePath);

        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(fileBytes, 0, fileBytes.Length);
    }

    /// <summary>
    ///     Decrypts the file and converts the output to a UTF-8 string.
    ///     Suitable for text-based files like config.ini or JSON-formatted verification lists.
    /// </summary>
    public static string DecryptFileToString(string filePath)
    {
        try
        {
            var decryptedBytes = DecryptFileToBytes(filePath);
            if (decryptedBytes.Length == 0) return string.Empty;

            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Encrypts a UTF-8 string and writes it to a file.
    ///     Used for saving modified config.ini back to the game folder.
    /// </summary>
    public static void EncryptStringToFile(string content, string filePath)
    {
        try
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);

            using var aes = Aes.Create();
            aes.Key = AesKey;
            aes.IV = AesIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var encryptedBytes = encryptor.TransformFinalBlock(contentBytes, 0, contentBytes.Length);

            File.WriteAllBytes(filePath, encryptedBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Failed to encrypt and write to file: {filePath}", ex);
        }
    }
}