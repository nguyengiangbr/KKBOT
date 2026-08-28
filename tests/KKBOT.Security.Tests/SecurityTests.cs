using System.IO;
using System.Threading.Tasks;
using KKBOT.Security.Abstractions;
using KKBOT.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KKBOT.Security.Tests
{
    public class SecurityTests
    {
        private static ServiceProvider CreateProvider(string? secretsPath = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("Security:SecretsPath", secretsPath ?? Path.GetTempFileName()),
                    new KeyValuePair<string, string?>("Security:EncryptionPassphrase", "test-passphrase"),
                    new KeyValuePair<string, string?>("Security:EncryptionSalt", "test-salt")
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(b => b.AddConsole());
            services.AddKkbotSecurity(config);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task SecretManager_CRUD_Works()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var provider = CreateProvider(tempFile);
                var secretManager = provider.GetRequiredService<ISecretManager>();

                await secretManager.SetSecretAsync("apiKey", "super-secret", CancellationToken.None);
                var value = await secretManager.GetSecretAsync("apiKey", CancellationToken.None);
                Assert.Equal("super-secret", value);

                await secretManager.DeleteSecretAsync("apiKey", CancellationToken.None);
                value = await secretManager.GetSecretAsync("apiKey", CancellationToken.None);
                Assert.Null(value);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void EncryptionService_EncryptDecrypt_RoundTrip()
        {
            var provider = CreateProvider();
            var encryption = provider.GetRequiredService<IEncryptionService>();

            var original = "Hello, KKBOT!";
            var cipher = encryption.Encrypt(original);
            var decrypted = encryption.Decrypt(cipher);
            Assert.Equal(original, decrypted);
        }

        [Fact]
        public async Task AuditLogger_Logs()
        {
            var provider = CreateProvider();
            var audit = provider.GetRequiredService<IAuditLogger>();
            await audit.LogAsync("TestAction", "user123", "details", CancellationToken.None);
            // No exception means success; in real test we'd capture logs.
        }
    }
}