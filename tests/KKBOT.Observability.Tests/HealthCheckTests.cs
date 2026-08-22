using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using KKBOT.Observability;
using Xunit;

namespace KKBOT.Observability.Tests;
{
    public class HealthCheckTests
    {
        [Fact]
        public void AddKkbotHealthChecks_RegistersHealthCheck()
        {
            var services = new ServiceCollection();
            services.AddKkbotHealthChecks();
            var provider = services.BuildServiceProvider();
            var healthChecks = provider.GetService<HealthCheckService>();
            Assert.NotNull(healthChecks);
        }
    }
}
