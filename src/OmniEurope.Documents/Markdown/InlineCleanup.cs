// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>Merges adjacent text nodes and drops empty ones, recursively.</summary>
internal static class InlineCleanup
{
    public static List<MarkdownInline> MergeText(List<MarkdownInline> inlines)
    {
        var result = new List<MarkdownInline>(inlines.Count);
        StringBuilder? pending = null;
        foreach (var inline in inlines)
        {
            if (inline is MarkdownText text)
            {
                (pending ??= new StringBuilder()).Append(text.Text);
                continue;
            }

            Flush(ref pending, result);
            if (inline is MarkdownContainerInline container)
            {
                var children = MergeText(container.ChildList);
                container.ChildList.Clear();
                container.ChildList.AddRange(children);
            }

            result.Add(inline);
        }

        Flush(ref pending, result);
        return result;
    }

    private static void Flush(ref StringBuilder? pending, List<MarkdownInline> result)
    {
        if (pending is { Length: > 0 })
        {
            result.Add(new MarkdownText(pending.ToString()));
        }

        pending = null;
    }
}
