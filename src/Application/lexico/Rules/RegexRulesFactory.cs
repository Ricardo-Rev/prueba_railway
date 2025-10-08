namespace Lexico.Application.Rules;

public static class RegexRulesFactory
{
    public static IRegexRules For(string code)
        => code switch
        {
            "es" => new SpanishRegexRules(),
            "en" => new EnglishRegexRules(),
            "ru" => new RussianRegexRules(),
            _    => new SpanishRegexRules() // fallback
        };
}
