namespace KKBOT.AI.Ollama.Models;

public sealed class OllamaVisionRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
}