using System.Globalization;

namespace Lexico.Application.Rules;

public class SpanishRegexRules : IRegexRules
{
    public string LanguageCode => "es";

    // Palabra con letras latinas + acentos
    public string WordRegex =>
        @"\b[\p{L}\p{M}áéíóúüñÁÉÍÓÚÜÑ]{2,}\b";

    public string PronounsRegex =>
        @"\b(yo|tú|vos|usted|él|ella|nosotros|nosotras|vosotros|ustedes|ellos|ellas|mí|conmigo|ti|contigo|sí|consigo)\b";

    // Verbos infinitivo + participios/gerundios comunes
    public string VerbRegex =>
        @"\b[\p{L}\p{M}áéíóúüñÁÉÍÓÚÜÑ]+(ar|er|ir|ando|iendo|ado|ido)\b";

    // Sustantivos comunes por terminación (muy heurístico)
    public string NounRegex =>
        @"\b[\p{L}\p{M}áéíóúüñÁÉÍÓÚÜÑ]+(ción|sión|dad|tud|aje|ura|ista|ismo|ez|eza|or|ora|o|a|e|s)\b";

    // Títulos + Capitalizados (1–3 palabras)
    public string PersonNameRegex =>
        @"\b((Sr\.|Sra\.|Srta\.|Dr\.|Dra\.|Ing\.|Lic\.)\s+)?[A-ZÁÉÍÓÚÜÑ][a-záéíóúüñ]+(?:\s+[A-ZÁÉÍÓÚÜÑ][a-záéíóúüñ]+){0,2}\b";

    public string EmailRegex   => @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[A-Za-z]{2,}";
    public string UrlRegex     => @"https?://[^\s]+";
    public string PhoneRegex   => @"\+?\d[\d\s\-]{7,}\d";
    public string DateRegex    => @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}|\d{4}[-/]\d{2}[-/]\d{2})\b";
    public string MoneyRegex   => @"\b(Q|L|C|USD|\$)\s?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?\b";
    public string HashtagRegex => @"#\w+";
    public string MentionRegex => @"@\w+";
    public string CodeRegex    => @"\b([A-Z]{2,4}-\d{3,6})\b";
}
