using System.Windows.Data;
using System.Windows.Markup;

namespace Magnifier.App.Localization;

// {loc:Tr SomeKey} in XAML — returns a live OneWay binding to Loc.Instance so language
// switches apply without recreating the window.
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
