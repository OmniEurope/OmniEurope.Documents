// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Html;

/// <summary>A node of an HTML tree.</summary>
public abstract class HtmlNode
{
    private readonly List<HtmlNode> _children = [];

    internal HtmlNode()
    {
    }

    /// <summary>The parent node, null for a root or a detached node.</summary>
    public HtmlNode? Parent { get; private set; }

    /// <summary>The parent when it is an element.</summary>
    public HtmlElement? ParentElement => Parent as HtmlElement;

    /// <summary>The child nodes, in order.</summary>
    public IReadOnlyList<HtmlNode> ChildNodes => _children;

    /// <summary>The child elements, in order.</summary>
    public IEnumerable<HtmlElement> Children => _children.OfType<HtmlElement>();

    /// <summary>The first child node.</summary>
    public HtmlNode? FirstChild => _children.Count > 0 ? _children[0] : null;

    /// <summary>The last child node.</summary>
    public HtmlNode? LastChild => _children.Count > 0 ? _children[^1] : null;

    /// <summary>The next sibling node.</summary>
    public HtmlNode? NextSibling => SiblingAt(1);

    /// <summary>The previous sibling node.</summary>
    public HtmlNode? PreviousSibling => SiblingAt(-1);

    /// <summary>The text of every descendant text node, concatenated.</summary>
    public virtual string TextContent
    {
        get
        {
            var builder = new StringBuilder();
            AppendText(builder);
            return builder.ToString();
        }
    }

    /// <summary>Every descendant node in document order.</summary>
    public IEnumerable<HtmlNode> Descendants()
    {
        var pending = new Stack<HtmlNode>();
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            pending.Push(_children[i]);
        }

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            for (var i = node._children.Count - 1; i >= 0; i--)
            {
                pending.Push(node._children[i]);
            }
        }
    }

    /// <summary>Every descendant element in document order.</summary>
    public IEnumerable<HtmlElement> DescendantElements() => Descendants().OfType<HtmlElement>();

    /// <summary>Appends <paramref name="child"/> (detaching it from its current parent first).</summary>
    public T AppendChild<T>(T child)
        where T : HtmlNode
    {
        return InsertAt(_children.Count, child);
    }

    /// <summary>Inserts <paramref name="child"/> before <paramref name="reference"/> (appends when null).</summary>
    public T InsertBefore<T>(T child, HtmlNode? reference)
        where T : HtmlNode
    {
        if (reference is null)
        {
            return AppendChild(child);
        }

        var index = _children.IndexOf(reference);
        if (index < 0)
        {
            throw new ArgumentException("The reference node is not a child of this node.", nameof(reference));
        }

        return InsertAt(index, child);
    }

    /// <summary>Removes <paramref name="child"/>.</summary>
    public void RemoveChild(HtmlNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child))
        {
            throw new ArgumentException("The node is not a child of this node.", nameof(child));
        }

        child.Parent = null;
    }

    /// <summary>Removes this node from its parent.</summary>
    public void Remove() => Parent?.RemoveChild(this);

    /// <summary>Replaces this node with <paramref name="nodes"/>.</summary>
    public void ReplaceWith(params IEnumerable<HtmlNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var parent = Parent ?? throw new InvalidOperationException("A detached node cannot be replaced.");
        var index = parent._children.IndexOf(this);
        parent.RemoveChild(this);
        foreach (var node in nodes.ToList())
        {
            parent.InsertAt(index++, node);
        }
    }

    /// <summary>The HTML of this node's children.</summary>
    public string InnerHtml
    {
        get
        {
            var builder = new StringBuilder();
            foreach (var child in _children)
            {
                HtmlSerializer.Write(child, builder);
            }

            return builder.ToString();
        }
    }

    /// <summary>The HTML of this node and its descendants.</summary>
    public string OuterHtml
    {
        get
        {
            var builder = new StringBuilder();
            HtmlSerializer.Write(this, builder);
            return builder.ToString();
        }
    }

    /// <summary>The first descendant element matching a CSS selector, or null.</summary>
    public HtmlElement? QuerySelector(string selector) => QuerySelectorAll(selector).FirstOrDefault();

    /// <summary>The descendant elements matching a CSS selector, in document order.</summary>
    public IReadOnlyList<HtmlElement> QuerySelectorAll(string selector)
    {
        var parsed = HtmlSelector.Parse(selector);
        return DescendantElements().Where(parsed.Matches).ToList();
    }

    internal virtual void AppendText(StringBuilder builder)
    {
        foreach (var child in _children)
        {
            child.AppendText(builder);
        }
    }

    private T InsertAt<T>(int index, T child)
        where T : HtmlNode
    {
        ArgumentNullException.ThrowIfNull(child);
        for (var ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
            {
                throw new InvalidOperationException("A node cannot contain itself.");
            }
        }

        if (child.Parent == this && _children.IndexOf(child) < index)
        {
            index--;
        }

        child.Remove();
        _children.Insert(index, child);
        child.Parent = this;
        return child;
    }

    private HtmlNode? SiblingAt(int delta)
    {
        if (Parent is null)
        {
            return null;
        }

        var index = Parent._children.IndexOf(this) + delta;
        return index >= 0 && index < Parent._children.Count ? Parent._children[index] : null;
    }
}

/// <summary>A parsed HTML document: <c>html</c> with <c>head</c> and <c>body</c>.</summary>
public sealed class HtmlDocument : HtmlNode
{
    internal HtmlDocument()
    {
        DocumentElement = AppendChild(new HtmlElement("html"));
        Head = DocumentElement.AppendChild(new HtmlElement("head"));
        Body = DocumentElement.AppendChild(new HtmlElement("body"));
    }

    /// <summary>The <c>html</c> element.</summary>
    public HtmlElement DocumentElement { get; }

    /// <summary>The <c>head</c> element.</summary>
    public HtmlElement Head { get; }

    /// <summary>The <c>body</c> element.</summary>
    public HtmlElement Body { get; }

    /// <summary>The text of the <c>title</c> element, empty when there is none.</summary>
    public string Title => Head.QuerySelector("title")?.TextContent.Trim() ?? string.Empty;
}

/// <summary>A list of nodes with no element around them (the result of fragment parsing).</summary>
public sealed class HtmlFragment : HtmlNode
{
    /// <summary>Creates an empty fragment.</summary>
    public HtmlFragment()
    {
    }
}

/// <summary>Text.</summary>
public sealed class HtmlText(string data) : HtmlNode
{
    /// <summary>The decoded text.</summary>
    public string Data { get; set; } = data;

    /// <inheritdoc />
    public override string TextContent => Data;

    internal override void AppendText(StringBuilder builder) => builder.Append(Data);
}

/// <summary>A comment.</summary>
public sealed class HtmlComment(string data) : HtmlNode
{
    /// <summary>The comment text.</summary>
    public string Data { get; set; } = data;

    /// <inheritdoc />
    public override string TextContent => string.Empty;

    internal override void AppendText(StringBuilder builder)
    {
    }
}
