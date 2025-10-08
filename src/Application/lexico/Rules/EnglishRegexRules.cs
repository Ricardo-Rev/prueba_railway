namespace Lexico.Application.Rules;

public class EnglishRegexRules : IRegexRules
{
    public string LanguageCode => "en";

    public string WordRegex =>
        @"\b[\p{L}\p{M}A-Za-z]{2,}\b";

    public string PronounsRegex =>
        @"\b(i|you|he|she|it|we|they|me|him|her|us|them|mine|yours|his|hers|ours|theirs)\b";

    public string VerbRegex =>
        @"\b[\p{L}]+(ing|ed|s)\b";

    public string NounRegex =>
        @"\b[\p{L}]+(tion|ness|ment|ity|ship|er|or|ist|ism|ance|ence|al|ure|age|ry|dom|hood|ism|ity|ty|ling)\b";

    public string PersonNameRegex =>
        @"\b((Mr\.|Mrs\.|Ms\.|Dr\.|Prof\.)\s+)?[A-Z][a-z]+(?:\s+[A-Z][a-z]+){0,2}\b";

    public string EmailRegex   => @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[A-Za-z]{2,}";
    public string UrlRegex     => @"https?://[^\s]+";
    public string PhoneRegex   => @"\+?\d[\d\s\-]{7,}\d";
    public string DateRegex    => @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}|\d{4}[-/]\d{2}[-/]\d{2})\b";
    public string MoneyRegex   => @"\b(USD|\$|£|€)\s?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?\b";
    public string HashtagRegex => @"#\w+";
    public string MentionRegex => @"@\w+";
    public string CodeRegex    => @"\b([A-Z]{2,4}-\d{3,6})\b";
}
