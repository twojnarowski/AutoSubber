using Microsoft.AspNetCore.DataProtection;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of token encryption using ASP.NET Core Data Protection
    /// </summary>
    public class TokenEncryptionService : ITokenEncryptionService
    {
        private readonly IDataProtector _protector;
        
        public TokenEncryptionService(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider.CreateProtector("AutoSubber.GoogleTokens");
        }
        
        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;
                
            return _protector.Protect(plaintext);
        }
        
        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return string.Empty;
                
            return _protector.Unprotect(ciphertext);
        }
    }
}