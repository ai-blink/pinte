using System.ComponentModel;

namespace Magnifier.App.Localization;

// Backs {loc:Tr Key} bindings. SetLanguage raises the WPF indexer-change signal so every
// bound TextBlock/Content re-pulls its string immediately, the same way MagnifierTheme.Apply
// swaps brushes live without recreating windows.
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Loc() { }

    // Ko until App.OnStartup resolves the real preference — matches the product's Korean-only
    // baseline, so window/tests constructed without a startup pass (unit tests) see the same
    // strings they did before localization existed.
    public AppLanguage Current { get; private set; } = AppLanguage.Ko;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(AppLanguage preference)
    {
        Current = AppLanguageResolver.Resolve(preference);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] => Strings.Get(Current, key);
}
