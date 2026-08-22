using KKBOT.Perception.Abstractions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Perception.Ocr;

/// <summary>
/// Parses raw OCR output into structured information.
/// </summary>
public static class OcrResultParser
{
    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);

    public static Task<OcrParseResult> ParseAsync(string rawText, CancellationToken cancellationToken)
    {
        var numbers = NumberRegex.Matches(rawText);
        var result = new OcrParseResult
        {
            RawText = rawText,
            ExtractedNumbers = numbers.Select(m => m.Value).ToArray()
        };
        return Task.FromResult(result);
    }
}

public sealed class OcrParseResult
{
    public string RawText { get; init; } = string.Empty;
    public string[] ExtractedNumbers { get; init; } = Array.Empty<string>();
}