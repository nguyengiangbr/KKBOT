using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugin.Sample;
using KKBOT.Plugins.Loader;
using KKBOT.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.Plugin.Loader.Tests
{
    public class PluginLoaderTests
    {
        [Fact]
        public async Task LoadPluginsAsync_FindsSamplePlugin()
        {
            // Arrange: locate the compiled Sample plugin DLL.
            var sampleAssembly = typeof(SampleGameModule).Assembly;
            var samplePath = Path.GetDirectoryName(sampleAssembly.Location)!;
            // The test will treat this folder as the plugin directory.

            var logger = NullLogger<PluginLoader>.Instance;
            var options = Options.Create(new PluginLoaderOptions
            {
                PluginDirectory = samplePath,
                RequireSignature = false
            });
            var signingValidator = new PluginSigningValidator(NullLogger<PluginSigningValidator>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            var serviceProvider = services.BuildServiceProvider();

            var loader = new PluginLoader(logger, options, signingValidator, serviceProvider);

            // Act
            var modules = await loader.LoadAllPluginsAsync(CancellationToken.None);

            // Assert
            Assert.NotEmpty(modules);
            var sampleModule = modules.FirstOrDefault(m => m.Id == "sample-game");
            Assert.NotNull(sampleModule);
            var manifest = sampleModule!.GetManifest();
            Assert.Equal("Sample Game Module", manifest.Name);
        }
    }
}
