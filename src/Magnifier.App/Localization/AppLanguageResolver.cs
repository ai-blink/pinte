using System.Globalization;

namespace Magnifier.App.Localization;

internal static class AppLanguageResolver
{
    // AppLanguage.System resolves to the closest supported language for the OS UI culture;
    // an unmapped culture falls back to English, matching README.md's root/default language.
    public static AppLanguage Resolve(AppLanguage preference)
    {
        if (preference != AppLanguage.System) return preference;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "ko" => AppLanguage.Ko,
            "zh" => AppLanguage.ZhHans,
            "ja" => AppLanguage.Ja,
            _ => AppLanguage.En
        };
    }
}
