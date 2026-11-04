// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Fonts;

/// <summary>
/// Extra free fonts for <see cref="FontLibrary"/>, in an assembly of their own so a host (a Blazor WebAssembly app,
/// for instance) can load it only when it converts documents. <see cref="Register"/> adds Selawik (drawing Segoe
/// UI), Gelasio (drawing Georgia), Roboto, Open Sans and Lato under their own names, and Noto Sans and Noto Serif,
/// which then draw unknown sans-serif and serif families and the characters a resolved font lacks, for a wider
/// European coverage (Greek, Cyrillic, extended Latin) than the bundled Liberation faces. Aptos, which has no free
/// equivalent, is drawn with Roboto. Every substitution is reported by the conversions
/// (<c>WordPdfResult.Fonts</c> and <c>Gaps</c>). The fonts are SIL OFL 1.1, shipped unmodified with
/// their licences (<see cref="Licence"/>, <see cref="Notice"/>). Thread-safe; each font is read once per process.
/// </summary>
public static class DocumentFonts
{
    private const string Prefix = "OmniEurope.Documents.Fonts.";

    // Registration order matters: for one family, a regular face comes before a light or semibold one.
    private static readonly (string File, string? Alias)[] Faces =
    [
        ("Selawik-Regular", null), ("Selawik-Bold", null),
        ("Selawik-Light", "Selawik Light"), ("Selawik-Semilight", "Selawik Semilight"), ("Selawik-Semibold", "Selawik Semibold"),
        ("Gelasio-Regular", null), ("Gelasio-Bold", null), ("Gelasio-Italic", null), ("Gelasio-BoldItalic", null),
        ("NotoSans-Regular", null), ("NotoSans-Bold", null), ("NotoSans-Italic", null), ("NotoSans-BoldItalic", null),
        ("NotoSerif-Regular", null), ("NotoSerif-Bold", null), ("NotoSerif-Italic", null), ("NotoSerif-BoldItalic", null),
        ("Roboto-Regular", null), ("Roboto-Bold", null), ("Roboto-Italic", null), ("Roboto-BoldItalic", null),
        ("OpenSans-Regular", null), ("OpenSans-Bold", null), ("OpenSans-Italic", null), ("OpenSans-BoldItalic", null),
        ("Lato-Regular", null), ("Lato-Bold", null), ("Lato-Italic", null), ("Lato-BoldItalic", null),
    ];

    private const string SelawikNote = "Selawik: free look-alike of Segoe UI (close widths, no italic face)";
    private const string AptosNote = "Aptos has no free equivalent: drawn with Roboto, the closest grotesque sans-serif (widths differ)";

    private static readonly (string Family, string Substitute, string Note)[] Substitutes =
    [
        ("Segoe UI", "Selawik", SelawikNote),
        ("Segoe UI Light", "Selawik Light", SelawikNote),
        ("Segoe UI Semilight", "Selawik Semilight", SelawikNote),
        ("Segoe UI Semibold", "Selawik Semibold", SelawikNote),
        ("Georgia", "Gelasio", "Gelasio: metric-compatible look-alike of Georgia (same widths, other shapes)"),
        ("Aptos", "Roboto", AptosNote),
        ("Aptos Display", "Roboto", AptosNote),
        ("Aptos Narrow", "Roboto", AptosNote),
        ("Aptos Light", "Roboto", AptosNote),
        ("Aptos SemiBold", "Roboto", AptosNote),
        ("Aptos Serif", "Noto Serif", "Aptos Serif has no free equivalent: drawn with Noto Serif (widths differ)"),
    ];

    private static readonly Lazy<TrueTypeFont[]> Loaded = new(() => Faces.Select(f => TrueTypeFont.Load(Read(f.File + ".ttf"))).ToArray());

    /// <summary>The families this assembly adds, each under its own name.</summary>
    public static IReadOnlyList<string> Families { get; } = ["Selawik", "Gelasio", "Noto Sans", "Noto Serif", "Roboto", "Open Sans", "Lato"];

    /// <summary>
    /// Registers the extra fonts in <paramref name="library"/>, with the substitutions (Segoe UI, Georgia, Aptos)
    /// and Noto Sans and Noto Serif as class fallbacks. A font registered beforehand under a requested name (Segoe
    /// UI, for instance) still wins. Registering into <see cref="FontLibrary.Default"/> changes every conversion of
    /// the process that does not pass its own library.
    /// </summary>
    public static void Register(FontLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var fonts = Loaded.Value;
        for (var i = 0; i < fonts.Length; i++)
        {
            library.Register(fonts[i], Faces[i].Alias);
        }

        foreach (var (family, substitute, note) in Substitutes)
        {
            library.AddSubstitute(family, substitute, note);
        }

        library.SetClassFallbacks("Noto Sans", "Noto Serif");
    }

    /// <summary>A new library holding the bundled fonts and the extra fonts (<see cref="Register"/>).</summary>
    public static FontLibrary CreateLibrary()
    {
        var library = new FontLibrary();
        Register(library);
        return library;
    }

    /// <summary>The licence text of an extra family (Selawik, Noto Sans, Open Sans...).</summary>
    public static string Licence(string family)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (!Families.Contains(family, StringComparer.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException($"No extra font family '{family}'.");
        }

        return ReadText($"LICENSE-{family.Replace(" ", string.Empty, StringComparison.Ordinal)}.txt");
    }

    /// <summary>The notice listing each font file with its version, licence, source URL and SHA-256.</summary>
    public static string Notice() => ReadText("NOTICE.txt");

    private static string ReadText(string name)
    {
        using var reader = new StreamReader(new MemoryStream(Read(name)));
        return reader.ReadToEnd();
    }

    private static byte[] Read(string name)
    {
        using var stream = typeof(DocumentFonts).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"Extra font resource '{name}' is missing from the assembly.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
