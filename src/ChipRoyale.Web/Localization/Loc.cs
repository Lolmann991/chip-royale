using System.Globalization;
using System.Resources;

namespace ChipRoyale.Web.Localization;

/// <summary>Marker type for the shared texts in <c>Resources/SharedResource(.en).resx</c>.</summary>
public sealed class SharedResource;

/// <summary>
/// Access to the UI texts. German is the neutral language (<c>SharedResource.resx</c>),
/// English lives in <c>SharedResource.en.resx</c>. The culture comes from the request
/// (cookie or browser language) and flows into the Blazor circuit.
/// Use <c>@L("Key")</c> in Razor (imported statically via _Imports).
/// </summary>
public static class Loc
{
    public static readonly string[] SupportedCultures = ["de", "en"];
    public const string DefaultCulture = "de";

    private static readonly ResourceManager Resources =
        new("ChipRoyale.Web.Resources.SharedResource", typeof(SharedResource).Assembly);

    /// <summary>Text for the current UI culture; the key itself if it is missing.</summary>
    public static string L(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Formatted text, e.g. <c>L("Table.Call", 40)</c> for "Mitgehen {0:N0}".</summary>
    public static string L(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, L(key), args);

    /// <summary>Text for an explicit culture (used by tests).</summary>
    public static string? Lookup(string key, CultureInfo culture) => Resources.GetString(key, culture);

    public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "de";
}
