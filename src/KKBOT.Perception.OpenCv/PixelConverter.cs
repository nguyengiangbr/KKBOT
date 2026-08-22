using OpenCvSharp;

namespace KKBOT.Perception.OpenCv;

/// <summary>
/// Utility for converting between pixel formats.
/// </summary>
public static class PixelConverter
{
    public static Mat ToBgr(Mat source) => source.CvtColor(ColorConversionCodes.RGB2BGR);
    public static Mat ToGray(Mat source) => source.CvtColor(ColorConversionCodes.BGR2GRAY);
    public static Mat ToRgb(Mat source) => source.CvtColor(ColorConversionCodes.BGR2RGB);
}