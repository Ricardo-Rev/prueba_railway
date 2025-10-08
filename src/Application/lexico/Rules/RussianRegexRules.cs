namespace Lexico.Application.Rules;

public class RussianRegexRules : IRegexRules
{
    public string LanguageCode => "ru";

    // Palabra con cirílico
    public string WordRegex =>
        @"\b[А-Яа-яЁё]{2,}\b";

    public string PronounsRegex =>
        @"\b(я|ты|он|она|оно|мы|вы|они|меня|тебя|его|её|нас|вас|их|мне|тебе|ему|ей|нам|вам|им)\b";

    // Verbos: infinitivo/tiempos comunes (heurístico)
    public string VerbRegex =>
        @"\b[А-Яа-яЁё]+(ть|л|ла|ло|ли|ешь|ем|ете|ют|у|ем|ете)\b";

    // Sustantivos por terminación frecuente
    public string NounRegex =>
        @"\b[А-Яа-яЁё]+(ие|ия|ость|тель|ник|ка|ть|ца|ок|ец|ёнок|ушка|ение)\b";

    // Nombres: inicial mayúscula + 1–2 apellidos
    public string PersonNameRegex =>
        @"\b[А-ЯЁ][а-яё]+(?:\s+[А-ЯЁ][а-яё]+){0,2}\b";

    public string EmailRegex   => @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[A-Za-z]{2,}";
    public string UrlRegex     => @"https?://[^\s]+";
    public string PhoneRegex   => @"\+?\d[\d\s\-]{7,}\d";
    public string DateRegex    => @"\b(\d{1,2}[./-]\d{1,2}[./-]\d{2,4}|\d{4}[./-]\d{2}[./-]\d{2})\b";
    public string MoneyRegex   => @"\b(₽|руб\.?)\s?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?\b";
    public string HashtagRegex => @"#\w+";
    public string MentionRegex => @"@\w+";
    public string CodeRegex    => @"\b([A-ZА-Я]{2,4}-\d{3,6})\b";
}
