// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Html;

/// <summary>
/// Parses HTML the way browsers recover from it, for the common cases: implied end tags (<c>p</c>,
/// <c>li</c>, <c>dt</c>/<c>dd</c>, table rows and cells, <c>option</c>), implied table sections, stray end
/// tags ignored, unclosed elements closed at the end. Nesting deeper than 512 elements is flattened, as
/// browsers do, so hostile input cannot exhaust the stack of the code that walks the tree.
/// </summary>
public static class HtmlParser
{
    /// <summary>Parses a whole document; <c>head</c> and <c>body</c> always exist.</summary>
    public static HtmlDocument ParseDocument(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var document = new HtmlDocument();
        new HtmlTreeBuilder(document.Body, document).Build(html);
        return document;
    }

    /// <summary>Parses a fragment, as the content of a <c>body</c> element.</summary>
    public static HtmlFragment ParseFragment(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var fragment = new HtmlFragment();
        new HtmlTreeBuilder(fragment, document: null).Build(html);
        return fragment;
    }
}

/// <summary>Builds the tree from tokens with a stack of open elements.</summary>
internal sealed class HtmlTreeBuilder(HtmlNode root, HtmlDocument? document)
{
    private const int MaxDepth = 512;
    private readonly List<HtmlElement> _open = [];
    private bool _bodyStarted;

    private HtmlNode Current => _open.Count > 0 ? _open[^1] : root;

    public void Build(string html)
    {
        foreach (var token in new HtmlTokenizer(html).Tokens())
        {
            switch (token.Kind)
            {
                case HtmlTokenKind.Text:
                    AddText(token.Data);
                    break;
                case HtmlTokenKind.Comment:
                    Current.AppendChild(new HtmlComment(token.Data));
                    break;
                case HtmlTokenKind.StartTag:
                    StartTag(token);
                    break;
                case HtmlTokenKind.EndTag:
                    EndTag(token.Data);
                    break;
            }
        }
    }

    private void AddText(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (document is not null && _open.Count == 0 && !_bodyStarted && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Text inside a head element (title, style...) does not start the body.
        _bodyStarted |= document is null || _open.Count == 0 || _open[0].Parent != document.Head;
        if (Current.LastChild is HtmlText previous)
        {
            previous.Data += text;
        }
        else
        {
            Current.AppendChild(new HtmlText(text));
        }
    }

    private void StartTag(HtmlToken token)
    {
        var tag = token.Data;
        if (MergedOrIgnored(token))
        {
            return;
        }

        CloseImplied(tag);
        var element = new HtmlElement(tag);
        foreach (var (name, value) in token.Attributes)
        {
            element.AddParsedAttribute(name, value);
        }

        if (document is not null && HtmlTags.IsHeadOnly(tag) && !_bodyStarted && _open.Count == 0)
        {
            // Opened like any element so its text (title, style, script) lands inside it.
            document.Head.AppendChild(element);
            if (!element.IsVoid && !token.SelfClosing)
            {
                _open.Add(element);
            }

            return;
        }

        _bodyStarted = true;
        InsertImpliedTableParents(tag);
        Current.AppendChild(element);
        if (!element.IsVoid && !(token.SelfClosing && tag is "svg" or "math") && _open.Count < MaxDepth)
        {
            _open.Add(element);
        }
    }

    // html, head and body merge into the document; a fragment is parsed as the content of a body, where
    // browsers ignore them.
    private bool MergedOrIgnored(HtmlToken token) =>
        document is not null ? MergeIntoDocument(token) : token.Data is "html" or "head" or "body";

    private bool MergeIntoDocument(HtmlToken token)
    {
        var target = token.Data switch
        {
            "html" => document!.DocumentElement,
            "body" => document!.Body,
            "head" => document!.Head,
            _ => null,
        };
        if (target is null)
        {
            return false;
        }

        foreach (var (name, value) in token.Attributes)
        {
            target.AddParsedAttribute(name, value);
        }

        return true;
    }

    // A start tag closes an open element it cannot sit in (Closes), looking no further up than StopAt.
    private static readonly Dictionary<string, (Func<string, bool> Closes, Func<string, bool> StopAt)> ImpliedEnds = new(StringComparer.Ordinal)
    {
        ["li"] = (t => t == "li", t => t is "ul" or "ol" || HtmlTags.IsScopeBoundary(t)),
        ["dt"] = (t => t is "dt" or "dd", t => t == "dl" || HtmlTags.IsScopeBoundary(t)),
        ["dd"] = (t => t is "dt" or "dd", t => t == "dl" || HtmlTags.IsScopeBoundary(t)),
        ["option"] = (t => t == "option", t => t is "select" or "optgroup"),
        ["optgroup"] = (t => t is "option" or "optgroup", t => t == "select"),
        ["a"] = (t => t == "a", HtmlTags.IsScopeBoundary),
        ["tr"] = (t => t == "tr", t => t == "table" || HtmlTags.IsTableSection(t)),
        ["td"] = (HtmlTags.IsCell, t => t is "tr" or "table"),
        ["th"] = (HtmlTags.IsCell, t => t is "tr" or "table"),
        ["thead"] = (HtmlTags.IsTableSection, t => t == "table"),
        ["tbody"] = (HtmlTags.IsTableSection, t => t == "table"),
        ["tfoot"] = (HtmlTags.IsTableSection, t => t == "table"),
    };

    private void CloseImplied(string tag)
    {
        if (HtmlTags.ClosesP(tag))
        {
            CloseIfOpen("p", stopAt: IsScopeOrList);
        }

        if (ImpliedEnds.TryGetValue(tag, out var rule))
        {
            CloseIfOpen(rule.Closes, rule.StopAt);
        }
        else if (HtmlTags.IsHeading(tag) && _open.Count > 0 && HtmlTags.IsHeading(_open[^1].TagName))
        {
            _open.RemoveAt(_open.Count - 1);
        }
    }

    private static bool IsScopeOrList(string tag) => HtmlTags.IsScopeBoundary(tag) || tag == "button";

    private void CloseIfOpen(string tag, Func<string, bool> stopAt) => CloseIfOpen(t => t == tag, stopAt);

    private void CloseIfOpen(Func<string, bool> match, Func<string, bool> stopAt)
    {
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            var name = _open[i].TagName;
            if (match(name))
            {
                _open.RemoveRange(i, _open.Count - i);
                return;
            }

            if (stopAt(name))
            {
                return;
            }
        }
    }

    // Browsers wrap rows in tbody and cells in tr when the markup leaves them out.
    private void InsertImpliedTableParents(string tag)
    {
        var current = Current is HtmlElement element ? element.TagName : string.Empty;
        if (tag == "tr" && current == "table")
        {
            OpenImplied("tbody");
        }
        else if (HtmlTags.IsCell(tag))
        {
            if (current == "table")
            {
                OpenImplied("tbody");
                current = "tbody";
            }

            if (HtmlTags.IsTableSection(current))
            {
                OpenImplied("tr");
            }
        }
    }

    private void OpenImplied(string tag)
    {
        var element = Current.AppendChild(new HtmlElement(tag));
        _open.Add(element);
    }

    private void EndTag(string tag)
    {
        switch (tag)
        {
            case "br":
                Current.AppendChild(new HtmlElement("br"));
                return;
            case "html" or "body" or "head":
                return;
            case "p" when !IsOpen("p"):
                Current.AppendChild(new HtmlElement("p"));
                return;
        }

        for (var i = _open.Count - 1; i >= 0; i--)
        {
            var name = _open[i].TagName;
            if (name == tag)
            {
                _open.RemoveRange(i, _open.Count - i);
                return;
            }

            if (HtmlTags.IsScopeBoundary(name))
            {
                return;
            }
        }
    }

    private bool IsOpen(string tag)
    {
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            if (_open[i].TagName == tag)
            {
                return true;
            }

            if (IsScopeOrList(_open[i].TagName))
            {
                return false;
            }
        }

        return false;
    }
}
