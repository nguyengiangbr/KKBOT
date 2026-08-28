using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Abstractions;
using KKBOT.AI.Ollama;
using KKBOT.AI.Ollama.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace KKBOT.AI.Tests
{
    public class OllamaServiceTests
    {
        [Fact]
        public async Task GenerateAsync_ReturnsAiResponse_FromOllamaJson()
        {
            // Arrange: mock HttpMessageHandler to return a known JSON payload
            var jsonResponse = "{\"model\":\"test-model\",\"response\":\"Hello world!\",\"done\":true,\"eval_count\":5,\"prompt_eval_count\":1,\"total_duration\":12345}";
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var options = Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434", Model = "test-model" });
            var logger = NullLogger<OllamaService>.Instance;
            var service = new OllamaService(httpClient, options, logger);

            // Act
            var result = await service.GenerateAsync("test prompt", CancellationToken.None);

            // Assert
            Assert.Equal("Hello world!", result.Content);
            Assert.Equal(6, result.Usage.Tokens); // eval + prompt
            Assert.Equal(12345, result.Usage.DurationMs);
        }
    }
}
