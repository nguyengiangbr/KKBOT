namespace KKBOT.AI.Ollama.Models;

public sealed class OllamaRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public OllamaRequestParameters Options { get; set; } = new();
    public bool Stream { get; set; }
}