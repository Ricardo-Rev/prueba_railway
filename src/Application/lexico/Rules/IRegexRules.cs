namespace Lexico.Application.Rules;

public interface IRegexRules
{
    string LanguageCode { get; } // "es" | "en" | "ru"

    // Regex “palabra” (token) incluyendo acentos y cirílico
    string WordRegex { get; }

    // Pronombres personales (palabras completas)
    string PronounsRegex { get; }

    // Heurísticas simples de “forma raíz”
    string VerbRegex { get; }       // patrones típicos verbales
    string NounRegex { get; }       // patrones típicos nominales

    // Nombres de personas (títulos + secuencias capitalizadas)
    string PersonNameRegex { get; }

    // Otros patrones (emails, urls, fechas, montos, hashtags, @menciones, códigos)
    string EmailRegex { get; }
    string UrlRegex { get; }
    string PhoneRegex { get; }
    string DateRegex { get; }
    string MoneyRegex { get; }
    string HashtagRegex { get; }
    string MentionRegex { get; }
    string CodeRegex { get; }
}
