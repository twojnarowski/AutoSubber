namespace AutoSubber.Services
{
    /// <summary>
    /// Service for encrypting and decrypting sensitive tokens
    /// </summary>
    public interface ITokenEncryptionService
    {
        /// <summary>
        /// Encrypts a plaintext token
        /// </summary>
        /// <param name="plaintext">The token to encrypt</param>
        /// <returns>Encrypted token string</returns>
        string Encrypt(string plaintext);
        
        /// <summary>
        /// Decrypts an encrypted token
        /// </summary>
        /// <param name="ciphertext">The encrypted token</param>
        /// <returns>Decrypted plaintext token</returns>
        string Decrypt(string ciphertext);
    }
}