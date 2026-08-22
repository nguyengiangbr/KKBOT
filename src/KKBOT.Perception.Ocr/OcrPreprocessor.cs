using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace KKBOT.Perception.Ocr;

/// <summary>
/// Preprocesses images for better OCR accuracy.
/// </summary>
public static class OcrPreprocessor
{
    public static byte[] ConvertToGrayscale(byte[] pngData)
    {
        using var ms = new MemoryStream(pngData);
        using var bitmap = new Bitmap(ms);
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var grayBmp = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format8bppIndexed);
        using var g = Graphics.FromImage(grayBmp);
        var attr = new ImageAttributes();
        attr.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(
            new float[][]
            {
                new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                new float[] {0,0,0,1,0},
                new float[] {0,0,0,0,1}
            }));
        g.DrawImage(bitmap, rect, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attr);
        using var outMs = new MemoryStream();
        grayBmp.Save(outMs, ImageFormat.Png);
        return outMs.ToArray();
    }
}