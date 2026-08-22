using OpenCvSharp;
using System;

namespace KKBOT.Perception.OpenCv;

/// <summary>
/// Applies common preprocessing steps to images before detection.
/// </summary>
public static class ImagePreprocessor
{
    public static Mat Preprocess(Mat source, double scaleFactor = 1.0, int blurKernel = 3)
    {
        var mat = source.Clone();
        if (scaleFactor != 1.0)
        {
            var newSize = new Size((int)(mat.Width * scaleFactor), (int)(mat.Height * scaleFactor));
            Cv2.Resize(mat, mat, newSize);
        }
        if (blurKernel > 0)
        {
            Cv2.GaussianBlur(mat, mat, new Size(blurKernel, blurKernel), 0);
        }
        return mat;
    }
}