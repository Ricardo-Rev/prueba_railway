// Contracts/IConfiguracionAnalisisRepository.cs
namespace Lexico.Application.Contracts;

public interface IConfiguracionAnalisisRepository
{
    /// Stopwords como set (se espera JSON array en BD)
    Task<HashSet<string>> GetStopwordsAsync(int idiomaId);

    /// Config de REGEX por idioma (se espera JSON objeto en BD, p.ej. { "WordRegex": "...", "EmailRegex": "..." })
    Task<Dictionary<string, string>> GetRegexConfigAsync(int idiomaId);

    /// Config de reglas gramaticales por idioma (JSON objeto, p.ej. { "PronounsRegex": "...", "VerbRegex": "...", "NounRegex": "..." })
    Task<Dictionary<string, string>> GetGrammarRulesConfigAsync(int idiomaId);
}
