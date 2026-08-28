namespace KKBOT.AI.Ollama;

/// <summary>
/// Configuration options for the Ollama client.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// Base URL of the Ollama server (e.g., http://localhost:11434).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default model name to use.
    /// </summary>
    public string Model { get; set; } = "llama2";

    /// <summary>
    /// Default vision model name to use.
    /// </summary>
    public string VisionModel { get; set; } = "llava";

    /// <summary>
    /// Default request parameters (e.g., temperature, max tokens).
    /// </summary>
    public OllamaRequestParameters DefaultParams { get; set; } = new();
}

public sealed class OllamaRequestParameters
{
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 512;
    public bool Stream { get; set; } = false;
}