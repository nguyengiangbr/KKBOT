// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;
using System.Text;
using KKBOT.Security.Abstractions;

namespace KKBOT.Security;

/// <summary>
/// AES‑256 encryption with a key derived from a passphrase (PBKDF2). 
/// For production, store the key in a hardware security module or Vault.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesEncryptionService(string passphrase, string salt)
    {
#pragma warning disable SYSLIB0060
        using var pbkdf2 = new Rfc2898DeriveBytes(passphrase, Encoding.UTF8.GetBytes(salt), 100_000, HashAlgorithmName.SHA256);
#pragma warning restore SYSLIB0060
        _key = pbkdf2.GetBytes(32); // 256 bits
        _iv = pbkdf2.GetBytes(16);  // 128 bits
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}