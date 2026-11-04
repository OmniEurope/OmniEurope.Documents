// SPDX-License-Identifier: EUPL-1.2
using System.Collections.Concurrent;
using System.Reflection;

namespace OmniEurope.Documents.Fonts;

/// <summary>
/// Resolves a family name and style to a font. Registered fonts come first; then the fonts bundled in this
/// package (Liberation Sans, Serif and Mono, Carlito, Caladea and the symbol face Noto Sans Symbols 2, SIL OFL
/// 1.1), reached directly or through the families they are metric-compatible with (Arial, Helvetica, Times New
/// Roman, Courier New, Calibri, Cambria); any other family falls back by class (monospaced, sans-serif, else
/// serif). Substitutes (<see cref="AddSubstitute"/>) and class fallbacks (<see cref="SetClassFallbacks"/>) let
/// registered fonts draw other families, as the extra fonts assembly does (<c>DocumentFonts.Register</c>);
/// <see cref="Describe"/> tells which font draws a family. Thread-safe.
/// </summary>
public sealed class FontLibrary
{
    // The symbol face has a regular style only: every style resolves to it.
    private const string SymbolStem = "NotoSansSymbols2";

    private static readonly Lazy<FontLibrary> Shared = new(() => new FontLibrary());
    private static readonly ConcurrentDictionary<string, TrueTypeFont> BundledCache = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Liberation Sans"] = "LiberationSans", ["Arial"] = "LiberationSans", ["Helvetica"] = "LiberationSans",
        ["Arimo"] = "LiberationSans", ["Arial MT"] = "LiberationSans", ["Helvetica Neue"] = "LiberationSans",
        ["Liberation Serif"] = "LiberationSerif", ["Times New Roman"] = "LiberationSerif", ["Times"] = "LiberationSerif",
        ["Tinos"] = "LiberationSerif", ["Times New Roman PS"] = "LiberationSerif",
        ["Liberation Mono"] = "LiberationMono", ["Courier New"] = "LiberationMono", ["Courier"] = "LiberationMono",
        ["Cousine"] = "LiberationMono",
        ["Carlito"] = "Carlito", ["Calibri"] = "Carlito", ["Calibri Light"] = "Carlito",
        ["Caladea"] = "Caladea", ["Cambria"] = "Caladea",
        ["Noto Sans Symbols 2"] = SymbolStem,
    };

    private static readonly string[] MonospacedHints = ["mono", "courier", "consol", "code", "typewriter", "fixed"];
    private static readonly string[] SansHints = ["sans", "arial", "helvetica", "verdana", "tahoma", "segoe", "selawik", "gothic", "grotesk", "trebuchet", "calibri", "aptos", "roboto", "open", "lato", "inter"];

    private readonly ConcurrentDictionary<string, List<TrueTypeFont>> _registered = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (string Family, string? Note)> _substitutes = new(StringComparer.OrdinalIgnoreCase);
    private volatile string? _sansFallback;
    private volatile string? _serifFallback;

    /// <summary>A library holding only the bundled fonts.</summary>
    public static FontLibrary Default => Shared.Value;

    /// <summary>The bundled text family names (the symbol face Noto Sans Symbols 2, regular only, is left out).</summary>
    public static IReadOnlyList<string> BundledFamilies { get; } = ["Liberation Sans", "Liberation Serif", "Liberation Mono", "Carlito", "Caladea"];

    /// <summary>Registers a font file under its own family name and, optionally, an extra alias.</summary>
    public TrueTypeFont Register(byte[] data, string? alias = null)
    {
        var font = TrueTypeFont.Load(data);
        Register(font, alias);
        return font;
    }

    /// <summary>Registers a loaded font under its own family name and, optionally, an extra alias (a font
    /// loaded once may serve several libraries). For one family, the first face registered wins between
    /// faces of the same bold and italic flags (a regular face before a light one).</summary>
    public void Register(TrueTypeFont font, string? alias = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        foreach (var name in new[] { font.Names.Family, alias }.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            _registered.AddOrUpdate(name!, _ => [font], (_, list) =>
            {
                lock (list)
                {
                    list.Add(font);
                }

                return list;
            });
        }
    }

    /// <summary>
    /// Draws <paramref name="family"/> with <paramref name="substitute"/>, a registered or bundled family (Segoe UI
    /// with Selawik, for instance), unless a font is registered under <paramref name="family"/> itself. The use is
    /// reported as a substitution, with <paramref name="note"/> saying what differs.
    /// </summary>
    public void AddSubstitute(string family, string substitute, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(substitute);
        _substitutes[family.Trim()] = (substitute.Trim(), note);
    }

    /// <summary>
    /// The registered families that draw unknown sans-serif and serif families, and the characters a resolved
    /// font lacks, before the bundled Liberation faces (Noto Sans and Noto Serif, for instance, for a wider
    /// European coverage). Null, or a family not registered, keeps Liberation Sans or Liberation Serif.
    /// </summary>
    public void SetClassFallbacks(string? sansSerif, string? serif)
    {
        _sansFallback = string.IsNullOrWhiteSpace(sansSerif) ? null : sansSerif.Trim();
        _serifFallback = string.IsNullOrWhiteSpace(serif) ? null : serif.Trim();
    }

    /// <summary>The font to use for a family and style; never null.</summary>
    public TrueTypeFont Resolve(string? family, bool bold = false, bool italic = false)
    {
        var name = Clean(family);
        if (TryRegistered(name, bold, italic, out var registered))
        {
            return registered;
        }

        if (_substitutes.TryGetValue(name, out var substitute))
        {
            return TryRegistered(substitute.Family, bold, italic, out var face) ? face : Bundled(BundledStem(substitute.Family), bold, italic);
        }

        if (Aliases.TryGetValue(name, out var stem))
        {
            return Bundled(stem, bold, italic);
        }

        stem = BundledStem(name);
        return TryRegistered(ClassFallback(stem), bold, italic, out var fallback) ? fallback : Bundled(stem, bold, italic);
    }

    /// <summary>What draws <paramref name="family"/>: the family used, whether it is the requested one, and why
    /// it differs (metric-compatible look-alike, substitute, class fallback).</summary>
    public FontUse Describe(string? family)
    {
        var name = Clean(family);
        var used = Resolve(name).Names.Family;
        if (_registered.ContainsKey(name))
        {
            return new FontUse(name, used, true, null);
        }

        if (_substitutes.TryGetValue(name, out var substitute))
        {
            return new FontUse(name, used, false, substitute.Note ?? $"substitute: {substitute.Family}");
        }

        if (Aliases.ContainsKey(name))
        {
            var exact = used.Equals(name, StringComparison.OrdinalIgnoreCase);
            return new FontUse(name, used, exact, exact ? null : LightSubstitution(name) ?? "metric-compatible look-alike (same widths, other shapes)");
        }

        var kind = BundledStem(name) switch
        {
            "LiberationMono" => "monospaced",
            "LiberationSans" => "sans-serif",
            _ => "serif",
        };
        return new FontUse(name, used, false, $"no free equivalent: drawn with the {kind} fallback");
    }

    /// <summary>True when <paramref name="family"/> is neither registered nor a bundled family or one of its
    /// metric-compatible aliases, so a look-alike chosen by name is drawn instead.</summary>
    internal bool IsSubstituted(string? family)
    {
        var name = (family ?? string.Empty).Trim().Trim('"', '\'');
        return !_registered.ContainsKey(name) && !Aliases.ContainsKey(name);
    }

    /// <summary>
    /// What a reader of the output should know when <paramref name="family"/> is Calibri Light and no font is registered
    /// under that name: Carlito Regular is drawn, which has Calibri's widths but no light weight, so the text looks
    /// heavier. Null otherwise (plain Calibri is drawn with Carlito, whose shapes differ but whose widths do not).
    /// </summary>
    internal string? LightSubstitution(string? family)
    {
        var name = (family ?? string.Empty).Trim().Trim('"', '\'');
        return !_registered.ContainsKey(name) && name.Equals("Calibri Light", StringComparison.OrdinalIgnoreCase)
            ? "font \"Calibri Light\" replaced by Carlito Regular (same widths, but no light weight: drawn heavier)"
            : null;
    }

    /// <summary>
    /// The font to draw <paramref name="codePoint"/> with: the resolved font when it has the glyph, else the
    /// bundled Liberation face of the same class (Caladea, for instance, has no Greek or Cyrillic), else the
    /// resolved font (its .notdef glyph).
    /// </summary>
    public TrueTypeFont ResolveForCharacter(string? family, bool bold, bool italic, int codePoint)
    {
        var primary = Resolve(family, bold, italic);
        if (primary.HasGlyph(codePoint))
        {
            return primary;
        }

        var stem = BundledStem((family ?? string.Empty).Trim());
        var fallbackStem = stem.StartsWith("Liberation", StringComparison.Ordinal) ? stem : stem is "Carlito" or SymbolStem ? "LiberationSans" : "LiberationSerif";
        if (TryRegistered(ClassFallback(fallbackStem), bold, italic, out var registered) && registered.HasGlyph(codePoint))
        {
            return registered;
        }

        var fallback = Bundled(fallbackStem, bold, italic);
        return fallback.HasGlyph(codePoint) ? fallback : primary;
    }

    /// <summary>The name a substitution gap gives the font drawn for <paramref name="family"/>: the bundled stem
    /// (LiberationSerif...) when a bundled face draws it, else the registered family (Selawik, Noto Sans...).</summary>
    internal string SubstituteName(string? family)
    {
        var name = Clean(family);
        if (_substitutes.TryGetValue(name, out var substitute))
        {
            return _registered.ContainsKey(substitute.Family) ? substitute.Family : BundledStem(substitute.Family);
        }

        var stem = BundledStem(name);
        return ClassFallback(stem) is { } fallback && _registered.ContainsKey(fallback) ? fallback : stem;
    }

    private static string Clean(string? family) => (family ?? string.Empty).Trim().Trim('"', '\'');

    // The registered family drawing an unknown family of a bundled class (none for monospaced).
    private string? ClassFallback(string stem) => stem switch
    {
        "LiberationSans" => _sansFallback,
        "LiberationSerif" => _serifFallback,
        _ => null,
    };

    private bool TryRegistered(string? family, bool bold, bool italic, out TrueTypeFont font)
    {
        if (family is not null && _registered.TryGetValue(family, out var faces))
        {
            lock (faces)
            {
                font = faces.OrderBy(f => (f.IsBold == bold ? 0 : 2) + (f.IsItalic == italic ? 0 : 1)).First();
                return true;
            }
        }

        font = null!;
        return false;
    }

    /// <summary>The bundled face of a stem (LiberationSans, Carlito...).</summary>
    public static TrueTypeFont Bundled(string stem, bool bold, bool italic)
    {
        var style = stem == SymbolStem ? "Regular" : (bold, italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        };
        return BundledCache.GetOrAdd($"{stem}-{style}", LoadResource);
    }

    /// <summary>The licence text that applies to a bundled family stem.</summary>
    public static string BundledLicence(string stem)
    {
        var file = stem.StartsWith("Liberation", StringComparison.Ordinal) ? "Liberation" : stem;
        using var stream = typeof(FontLibrary).Assembly.GetManifestResourceStream($"OmniEurope.Documents.Fonts.LICENSE-{file}.txt")
            ?? throw new KeyNotFoundException($"No licence for '{stem}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The bundled stem used for a family name (alias, else class heuristics).</summary>
    public static string BundledStem(string family)
    {
        if (Aliases.TryGetValue(family, out var stem))
        {
            return stem;
        }

        var lower = family.ToLowerInvariant();
        if (Array.Exists(MonospacedHints, lower.Contains))
        {
            return "LiberationMono";
        }

        return Array.Exists(SansHints, lower.Contains) ? "LiberationSans" : "LiberationSerif";
    }

    private static TrueTypeFont LoadResource(string name)
    {
        using var stream = typeof(FontLibrary).GetTypeInfo().Assembly.GetManifestResourceStream($"OmniEurope.Documents.Fonts.{name}.ttf")
            ?? throw new InvalidOperationException($"Bundled font '{name}' is missing from the assembly.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return TrueTypeFont.Load(copy.ToArray());
    }
}
