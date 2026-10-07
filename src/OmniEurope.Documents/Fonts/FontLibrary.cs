// SPDX-License-Identifier: EUPL-1.2
using System.Collections.Concurrent;
using System.Reflection;

namespace OmniEurope.Documents.Fonts;

/// <summary>
/// Resolves a family name and style to a font. Registered fonts come first; then the fonts bundled in this
/// package (Liberation Sans, Serif and Mono, Carlito, Caladea, SIL OFL 1.1), reached directly or through the
/// families they are metric-compatible with (Arial, Helvetica, Times New Roman, Courier New, Calibri,
/// Cambria); any other family falls back by class (monospaced, sans-serif, else serif). Thread-safe.
/// </summary>
public sealed class FontLibrary
{
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
    };

    private static readonly string[] MonospacedHints = ["mono", "courier", "consol", "code", "typewriter", "fixed"];
    private static readonly string[] SansHints = ["sans", "arial", "helvetica", "verdana", "tahoma", "segoe", "gothic", "grotesk", "trebuchet", "calibri", "aptos", "roboto", "open", "lato", "inter"];

    private readonly ConcurrentDictionary<string, List<TrueTypeFont>> _registered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A library holding only the bundled fonts.</summary>
    public static FontLibrary Default => Shared.Value;

    /// <summary>The bundled family names.</summary>
    public static IReadOnlyList<string> BundledFamilies { get; } = ["Liberation Sans", "Liberation Serif", "Liberation Mono", "Carlito", "Caladea"];

    /// <summary>Registers a font file under its own family name and, optionally, an extra alias.</summary>
    public TrueTypeFont Register(byte[] data, string? alias = null)
    {
        var font = TrueTypeFont.Load(data);
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

        return font;
    }

    /// <summary>The font to use for a family and style; never null.</summary>
    public TrueTypeFont Resolve(string? family, bool bold = false, bool italic = false)
    {
        var name = (family ?? string.Empty).Trim().Trim('"', '\'');
        if (_registered.TryGetValue(name, out var faces))
        {
            lock (faces)
            {
                return faces.OrderBy(f => (f.IsBold == bold ? 0 : 2) + (f.IsItalic == italic ? 0 : 1)).First();
            }
        }

        return Bundled(BundledStem(name), bold, italic);
    }

    /// <summary>True when <paramref name="family"/> is neither registered nor a bundled family or one of its
    /// metric-compatible aliases, so a look-alike chosen by name is drawn instead.</summary>
    internal bool IsSubstituted(string? family)
    {
        var name = (family ?? string.Empty).Trim().Trim('"', '\'');
        return !_registered.ContainsKey(name) && !Aliases.ContainsKey(name);
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
        var fallbackStem = stem.StartsWith("Liberation", StringComparison.Ordinal) ? stem : stem == "Carlito" ? "LiberationSans" : "LiberationSerif";
        var fallback = Bundled(fallbackStem, bold, italic);
        return fallback.HasGlyph(codePoint) ? fallback : primary;
    }

    /// <summary>The bundled face of a stem (LiberationSans, Carlito...).</summary>
    public static TrueTypeFont Bundled(string stem, bool bold, bool italic)
    {
        var style = (bold, italic) switch
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
