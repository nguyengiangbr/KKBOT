using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.OpenCv;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KKBOT.Perception.Tests
{
    public class OpenCvDetectorTests
    {
        [Fact]
        public async Task DetectAsync_FindsTemplate_InFrame()
        {
            // Arrange: create temporary folder for templates
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Create a simple white square template (20x20)
            using var tmplMat = new OpenCvSharp.Mat(new OpenCvSharp.Size(20, 20), OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(255));
            var tmplPath = Path.Combine(tempDir, "square.png");
            Cv2.ImWrite(tmplPath, tmplMat);

            // Create a larger black frame (100x100) and embed the template at (30,40)
            using var frameMat = new OpenCvSharp.Mat(new OpenCvSharp.Size(100, 100), OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));
            var roi = new OpenCvSharp.Rect(30, 40, tmplMat.Width, tmplMat.Height);
            tmplMat.CopyTo(new OpenCvSharp.Mat(frameMat, roi));
            var frameBytes = frameMat.ImEncode(".png");

            var frame = new Frame
            {
                Data = frameBytes,
                Width = frameMat.Width,
                Height = frameMat.Height,
                Format = "png"
            };

            var logger = NullLogger<OpenCvGameDetector>.Instance;
            var detector = new OpenCvGameDetector(tempDir, requiredConfidence: 0.8, logger);

            // Act
            var result = await detector.DetectAsync(frame, CancellationToken.None);

            // Assert
            Assert.True(result.IsMatch, "The detector should recognise the template inside the frame.");
            Assert.InRange(result.Confidence, 0.8, 1.0);
            Assert.Equal("square", result.DetectedValue);
        }
    }
}
