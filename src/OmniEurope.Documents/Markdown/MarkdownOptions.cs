// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Markdown;

/// <summary>
/// Options of the Markdown parser and HTML renderer. The defaults are the safe CommonMark core: raw HTML
/// is not recognised (it stays text and is escaped) and links or images whose scheme is not allowed are
/// rendered without their destination.
/// </summary>
public sealed class MarkdownOptions
{
    /// <summary>Plain CommonMark, raw HTML escaped.</summary>
    public static MarkdownOptions CommonMark { get; } = new();

    /// <summary>CommonMark plus the GitHub extensions (tables, task lists, strikethrough, bare URLs), raw
    /// HTML escaped.</summary>
    public static MarkdownOptions GitHub { get; } = new()
    {
        Tables = true,
        TaskLists = true,
        Strikethrough = true,
        ExtendedAutolinks = true,
    };

    /// <summary>True to recognise raw HTML blocks and inline tags and pass them through unchanged. Only for
    /// trusted input. Default false.</summary>
    public bool AllowRawHtml { get; init; }

    /// <summary>GFM pipe tables. Default false.</summary>
    public bool Tables { get; init; }

    /// <summary>GFM task list items (<c>- [x] done</c>). Default false.</summary>
    public bool TaskLists { get; init; }

    /// <summary>GFM strikethrough (<c>~~text~~</c>). Default false.</summary>
    public bool Strikethrough { get; init; }

    /// <summary>GFM bare URLs (<c>https://...</c>, <c>www....</c>) and e-mail addresses become links. Default false.</summary>
    public bool ExtendedAutolinks { get; init; }

    /// <summary>
    /// Decides whether a destination may be rendered. Default <see cref="MarkdownUrl.IsSafe"/>: relative
    /// URLs, <c>http</c>, <c>https</c> and <c>mailto</c>, plus <c>data:image/png|gif|jpeg|webp</c> for images.
    /// The second argument is true for an image.
    /// </summary>
    public Func<string, bool, bool> UrlFilter { get; init; } = MarkdownUrl.IsSafe;
}
