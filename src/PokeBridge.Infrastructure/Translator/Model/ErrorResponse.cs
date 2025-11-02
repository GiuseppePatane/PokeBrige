using System.Text.Json.Serialization;

namespace PokeBridge.Infrastructure.Translator.Model;

public class Error
{
    [JsonPropertyName("code")] public int Code { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; }
}

public class ErrorResponse
{
    [JsonPropertyName("error")] public Error Error { get; set; }
}