using AutoSubber.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class TokenEncryptionServiceTests
    {
        private readonly TokenEncryptionService _tokenEncryptionService;

        public TokenEncryptionServiceTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            
            _tokenEncryptionService = new TokenEncryptionService(dataProtectionProvider);
        }

        [Fact]
        public void Encrypt_WithValidPlaintext_ReturnsEncryptedString()
        {
            // Arrange
            var plaintext = "my-secret-token";

            // Act
            var encrypted = _tokenEncryptionService.Encrypt(plaintext);

            // Assert
            Assert.NotNull(encrypted);
            Assert.NotEmpty(encrypted);
            Assert.NotEqual(plaintext, encrypted);
        }

        [Fact]
        public void Encrypt_WithNullPlaintext_ReturnsEmptyString()
        {
            // Act
            var encrypted = _tokenEncryptionService.Encrypt(null!);

            // Assert
            Assert.Equal(string.Empty, encrypted);
        }

        [Fact]
        public void Encrypt_WithEmptyPlaintext_ReturnsEmptyString()
        {
            // Act
            var encrypted = _tokenEncryptionService.Encrypt(string.Empty);

            // Assert
            Assert.Equal(string.Empty, encrypted);
        }

        [Fact]
        public void Decrypt_WithValidCiphertext_ReturnsOriginalPlaintext()
        {
            // Arrange
            var plaintext = "my-secret-token";
            var encrypted = _tokenEncryptionService.Encrypt(plaintext);

            // Act
            var decrypted = _tokenEncryptionService.Decrypt(encrypted);

            // Assert
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Decrypt_WithNullCiphertext_ReturnsEmptyString()
        {
            // Act
            var decrypted = _tokenEncryptionService.Decrypt(null!);

            // Assert
            Assert.Equal(string.Empty, decrypted);
        }

        [Fact]
        public void Decrypt_WithEmptyCiphertext_ReturnsEmptyString()
        {
            // Act
            var decrypted = _tokenEncryptionService.Decrypt(string.Empty);

            // Assert
            Assert.Equal(string.Empty, decrypted);
        }

        [Fact]
        public void EncryptDecrypt_RoundTrip_PreservesOriginalValue()
        {
            // Arrange
            var originalValue = "access_token_12345_abcdef";

            // Act
            var encrypted = _tokenEncryptionService.Encrypt(originalValue);
            var decrypted = _tokenEncryptionService.Decrypt(encrypted);

            // Assert
            Assert.Equal(originalValue, decrypted);
        }

        [Fact]
        public void Encrypt_SameInputTwice_ProducesDifferentOutputs()
        {
            // Arrange
            var plaintext = "same-token-value";

            // Act
            var encrypted1 = _tokenEncryptionService.Encrypt(plaintext);
            var encrypted2 = _tokenEncryptionService.Encrypt(plaintext);

            // Assert - Data Protection includes randomness, so same input produces different ciphertext
            Assert.NotEqual(encrypted1, encrypted2);
            
            // But both decrypt to the same value
            Assert.Equal(plaintext, _tokenEncryptionService.Decrypt(encrypted1));
            Assert.Equal(plaintext, _tokenEncryptionService.Decrypt(encrypted2));
        }

        [Theory]
        [InlineData("short")]
        [InlineData("medium-length-token-value")]
        [InlineData("very-long-token-value-that-might-be-used-for-oauth-access-tokens-or-refresh-tokens-with-lots-of-characters")]
        [InlineData("special!@#$%^&*()characters")]
        [InlineData("unicode-characters-αβγδε")]
        public void EncryptDecrypt_WithVariousInputLengths_WorksCorrectly(string input)
        {
            // Act
            var encrypted = _tokenEncryptionService.Encrypt(input);
            var decrypted = _tokenEncryptionService.Decrypt(encrypted);

            // Assert
            Assert.Equal(input, decrypted);
        }
    }
}