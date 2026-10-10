// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;

namespace OmniEurope.Documents.Word.Validation;

/// <summary>
/// The markup compatibility preprocessing of ECMA-376 Part 3, applied to a part before schema validation, as a
/// consumer that understands exactly the namespaces of the shipped schemas:
/// <list type="bullet">
/// <item>an element or attribute of a namespace that <c>mc:Ignorable</c> declares ignorable and that is not
/// understood is removed; an ignorable element <c>mc:ProcessContent</c> names is replaced by its content;</item>
/// <item><c>mc:AlternateContent</c> is replaced by the content of its first <c>mc:Choice</c> whose
/// <c>Requires</c> namespaces are all understood, else by that of its <c>mc:Fallback</c>, else by nothing;</item>
/// <item><c>mc:MustUnderstand</c> naming a namespace not understood, a prefix that is not declared, and
/// <c>mc:AlternateContent</c> that breaks its content rules are errors;</item>
/// <item>the markup compatibility attributes themselves are removed.</item>
/// </list>
/// Elements and attributes of namespaces neither understood nor ignorable are left in place, for the schema
/// validation to report.
/// </summary>
internal sealed class MarkupCompatibility
{
    public static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private readonly Func<string, bool> _understood;
    private readonly Action<XObject, string> _error;

    private MarkupCompatibility(Func<string, bool> understood, Action<XObject, string> error)
    {
        _understood = understood;
        _error = error;
    }

    /// <summary>
    /// Preprocesses a part in place, its root element being of an understood namespace (so the root itself stays);
    /// each error is reported with the node it concerns.
    /// </summary>
    public static void Process(XElement root, Func<string, bool> understood, Action<XObject, string> error) =>
        new MarkupCompatibility(understood, error).Rewrite(root, Scope.Empty);

    // The nodes that replace an element: itself (cleaned), its processed content, or nothing.
    private IEnumerable<XNode> Rewrite(XElement element, Scope inherited)
    {
        var scope = inherited.Enter(element, _understood, _error);
        if (element.Name.Namespace == Mc)
        {
            return element.Name.LocalName == "AlternateContent" ? Alternate(element, scope) : Misplaced(element);
        }

        var ns = element.Name.NamespaceName;
        if (!_understood(ns) && scope.Ignorable.Contains(ns))
        {
            return scope.ProcessesContent(element.Name) ? Content(element, scope) : [];
        }

        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && Removed(a, scope)).ToList())
        {
            attribute.Remove();
        }

        var children = Content(element, scope);
        element.RemoveNodes();
        element.Add(children);
        return [element];
    }

    // An attribute of the markup compatibility namespace, or of an ignorable namespace not understood.
    private bool Removed(XAttribute attribute, Scope scope)
    {
        var ns = attribute.Name.NamespaceName;
        return attribute.Name.Namespace == Mc || (ns.Length > 0 && !_understood(ns) && scope.Ignorable.Contains(ns));
    }

    // The processed child nodes of an element, detached from it. Every child is processed while all of them are still
    // attached, so the errors found inside give the path of the original tree.
    private List<XNode> Content(XElement element, Scope scope)
    {
        var nodes = element.Nodes().ToList();
        var result = new List<XNode>();
        foreach (var node in nodes)
        {
            if (node is XElement child)
            {
                result.AddRange(Rewrite(child, scope));
            }
            else
            {
                result.Add(node);
            }
        }

        element.RemoveNodes();
        return result;
    }

    private IEnumerable<XNode> Alternate(XElement alternate, Scope scope)
    {
        var choices = alternate.Elements().ToList();
        foreach (var other in choices.Where(c => c.Name != Mc + "Choice" && c.Name != Mc + "Fallback"))
        {
            _error(other, $"mc:AlternateContent holds {other.Name.LocalName} in namespace '{other.Name.NamespaceName}': only mc:Choice and mc:Fallback are allowed");
        }

        var fallbacks = choices.Where(c => c.Name == Mc + "Fallback").ToList();
        if (fallbacks.Count > 1 || (fallbacks.Count == 1 && choices.SkipWhile(c => c.Name != Mc + "Fallback").Skip(1).Any(c => c.Name == Mc + "Choice")))
        {
            _error(alternate, "mc:AlternateContent must hold its mc:Choice elements then at most one mc:Fallback");
        }

        if (!choices.Any(c => c.Name == Mc + "Choice"))
        {
            _error(alternate, "mc:AlternateContent holds no mc:Choice");
        }

        var selected = choices.FirstOrDefault(c => c.Name == Mc + "Choice" && Satisfied(c)) ?? fallbacks.FirstOrDefault();
        return selected is null ? [] : Content(selected, scope.Enter(selected, _understood, _error));
    }

    // A choice whose Requires prefixes all name understood namespaces; a missing or undeclared prefix is an error.
    private bool Satisfied(XElement choice)
    {
        var requires = (string?)choice.Attribute("Requires");
        if (string.IsNullOrWhiteSpace(requires))
        {
            _error(choice, "mc:Choice has no Requires attribute");
            return false;
        }

        var satisfied = true;
        foreach (var prefix in requires.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var ns = choice.GetNamespaceOfPrefix(prefix);
            if (ns is null)
            {
                _error(choice, $"mc:Choice Requires prefix '{prefix}', which is not declared");
            }

            satisfied &= ns is not null && _understood(ns.NamespaceName);
        }

        return satisfied;
    }

    private IEnumerable<XNode> Misplaced(XElement element)
    {
        _error(element, $"mc:{element.Name.LocalName} is not allowed here: only mc:AlternateContent may appear in the content of an element");
        return [];
    }

    // The markup compatibility declarations in force at an element.
    private sealed class Scope
    {
        public static readonly Scope Empty = new([], []);

        private readonly HashSet<string> _processContent;

        private Scope(HashSet<string> ignorable, HashSet<string> processContent)
        {
            Ignorable = ignorable;
            _processContent = processContent;
        }

        public HashSet<string> Ignorable { get; }

        // The scope of an element: the inherited declarations and those its own mc attributes add.
        public Scope Enter(XElement element, Func<string, bool> understood, Action<XObject, string> error)
        {
            var ignorable = element.Attribute(Mc + "Ignorable");
            var process = element.Attribute(Mc + "ProcessContent");
            var must = element.Attribute(Mc + "MustUnderstand");
            if (must is not null)
            {
                foreach (var ns in Namespaces(element, must, error).Where(n => !understood(n)))
                {
                    error(must, $"mc:MustUnderstand names namespace '{ns}', which is not understood");
                }
            }

            if (ignorable is null && process is null)
            {
                return this;
            }

            var next = new Scope([.. Ignorable], [.. _processContent]);
            if (ignorable is not null)
            {
                next.Ignorable.UnionWith(Namespaces(element, ignorable, error));
            }

            if (process is not null)
            {
                foreach (var name in process.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var colon = name.IndexOf(':', StringComparison.Ordinal);
                    var ns = colon > 0 ? element.GetNamespaceOfPrefix(name[..colon]) : null;
                    if (ns is null)
                    {
                        error(process, $"mc:ProcessContent names '{name}', whose prefix is not declared");
                        continue;
                    }

                    next._processContent.Add("{" + ns.NamespaceName + "}" + name[(colon + 1)..]);
                }
            }

            return next;
        }

        public bool ProcessesContent(XName name) =>
            _processContent.Contains(name.ToString()) || _processContent.Contains("{" + name.NamespaceName + "}*");

        private static IEnumerable<string> Namespaces(XElement element, XAttribute attribute, Action<XObject, string> error)
        {
            foreach (var prefix in attribute.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var ns = element.GetNamespaceOfPrefix(prefix);
                if (ns is null)
                {
                    error(attribute, $"mc:{attribute.Name.LocalName} names prefix '{prefix}', which is not declared");
                    continue;
                }

                yield return ns.NamespaceName;
            }
        }
    }
}
