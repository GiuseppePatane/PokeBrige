using PokeBridge.Core.Shared;

namespace PokeBridge.Core.Translator;

public interface ITranslatorClient
{
    public Task<Result<string>> GetTranslationAsync(string text,TranslationType translationType);
}