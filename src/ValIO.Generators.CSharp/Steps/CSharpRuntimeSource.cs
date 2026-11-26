using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ValIO.Core.Plugins;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>Places the checked-in C# runtime declarations in the generated client's namespace.</summary>
internal static class CSharpRuntimeSource
{
    /// <summary>Reads a complete repository-owned source file and applies the profile to its syntax tree.</summary>
    /// <param name="fileName">The embedded C# file with the fixed runtime implementation.</param>
    /// <param name="ns">The namespace assigned to its declarations, if specified.</param>
    /// <returns>The rendered compilation unit.</returns>
    internal static string Render(string fileName, string ns)
    {
        var source = SourceResource.Read(typeof(CSharpRuntimeSource).Assembly, $"ValIO.Runtime.{fileName}");
        var unit = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        if (!string.IsNullOrWhiteSpace(ns))
        {
            var declaration = SyntaxFactory.FileScopedNamespaceDeclaration(BuildNamespace(ns))
                .WithMembers(unit.Members);
            unit = unit.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(declaration));
        }

        return unit.NormalizeWhitespace(eol: Environment.NewLine).ToFullString();
    }

    /// <summary>Builds a qualified name from the namespace identifiers in the profile.</summary>
    /// <param name="ns">The dot-delimited namespace.</param>
    /// <returns>The qualified name syntax.</returns>
    private static NameSyntax BuildNamespace(string ns)
    {
        var parts = ns.Split('.');
        NameSyntax name = SyntaxFactory.IdentifierName(parts[0]);
        foreach (var part in parts.Skip(1))
            name = SyntaxFactory.QualifiedName(name, SyntaxFactory.IdentifierName(part));
        return name;
    }
}
