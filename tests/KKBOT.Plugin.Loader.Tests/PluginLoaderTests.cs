using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugin.Sample;
using KKBOT.Plugins.Loader;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KKBOT.Plugin.Loader.Tests;
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
            var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);

            // Act
            var modules = await loader.LoadPluginsAsync(samplePath, CancellationToken.None);

            // Assert
            Assert.NotEmpty(modules);
            var sampleModule = modules.FirstOrDefault(m => m.Id == "sample-game");
            Assert.NotNull(sampleModule);
            var manifest = sampleModule!.GetManifest();
            Assert.Equal("Sample Game Module", manifest.Name);
        }
    }
}
