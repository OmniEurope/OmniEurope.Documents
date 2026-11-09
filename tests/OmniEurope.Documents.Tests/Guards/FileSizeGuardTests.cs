// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Tests.Guards;

/// <summary>
/// Every production source file under <c>src/</c> stays within 600 effective lines, so large types are split into
/// real collaborators before they grow into god files. Blank lines and comment-only lines are not counted.
/// When this fails, extract a collaborator type with its own responsibility; never split a class across partial files.
/// </summary>
public sealed class FileSizeGuardTests
{
    private const int MaxLines = 600;

    [Fact]
    public void No_production_file_exceeds_the_line_budget()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsOutOfScope(file))
            .ToList();
        Assert.True(files.Count > 0, "No production file found under src: the guard would pass vacuously.");

        var offenders = files
            .Select(file => (Path: Path.GetRelativePath(root, file).Replace('\\', '/'), Lines: EffectiveLines(File.ReadAllLines(file))))
            .Where(file => file.Lines > MaxLines)
            .OrderByDescending(file => file.Lines)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These production files exceed {MaxLines} effective lines:{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders.Select(file => $"  - {file.Path} ({file.Lines})")));
    }

    [Fact]
    public void Counting_ignores_blank_and_comment_only_lines()
    {
        string[] lines =
        [
            "// header",
            "",
            "/* block",
            "   still comment */",
            "/// <summary>doc</summary>",
            "int a = 1; // trailing comment counts",
            "/* inline */ int b = 2;",
            "string c = \"/* not a comment */\";",
        ];

        Assert.Equal(3, EffectiveLines(lines));
    }

    /// <summary>Counts lines holding something other than a comment. A block comment is recognised only where it
    /// opens a line, so a <c>/*</c> inside a string literal never starts one.</summary>
    internal static int EffectiveLines(IEnumerable<string> lines)
    {
        var count = 0;
        var inBlock = false;
        foreach (var line in lines)
        {
            var rest = line.Trim();
            while (true)
            {
                if (inBlock)
                {
                    var end = rest.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0)
                    {
                        rest = string.Empty;
                        break;
                    }

                    rest = rest[(end + 2)..].TrimStart();
                    inBlock = false;
                }

                if (!rest.StartsWith("/*", StringComparison.Ordinal))
                {
                    break;
                }

                inBlock = true;
                rest = rest[2..];
            }

            if (rest.Length > 0 && !rest.StartsWith("//", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsOutOfScope(string file)
    {
        var separator = Path.DirectorySeparatorChar;
        var name = Path.GetFileName(file);
        return file.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
            || file.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmniEurope.Documents.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (OmniEurope.Documents.slnx) not found above the test output folder.");
    }
}
