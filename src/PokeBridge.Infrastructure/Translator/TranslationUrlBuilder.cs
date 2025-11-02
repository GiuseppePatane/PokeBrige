

using Microsoft.AspNetCore.WebUtilities;

namespace PokeBridge.Infrastructure.Translator;

public static class TranslationUrlBuilder
{
    private const string ShakespearePath = "translate/shakespeare.json";
    private const string YodaPath = "translate/yoda.json";

    public static Uri BuildShakespeareTranslationUri(string? text) =>
        BuildTranslationUriInternal(ShakespearePath, text);

    public static Uri BuildYodaTranslationUri(string? text) =>
        BuildTranslationUriInternal(YodaPath, text);

    private static Uri BuildTranslationUriInternal(string path, string? text)
    {
        text ??= string.Empty;

        var query = new Dictionary<string, string?>
        {
            { "text", text }
        };

        var url = QueryHelpers.AddQueryString(path, query);

        return new Uri(url, UriKind.Relative);
    }
}