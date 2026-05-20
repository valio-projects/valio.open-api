using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Fluent helper for assembling a <see cref="SourceFileNode"/> with deduplicated
/// imports. Use this from any AST builder that needs to incrementally gather
/// imports (each visitor can call <see cref="EnsureImport"/> idempotently)
/// instead of re-implementing dedup logic per builder.
///
/// Identity for dedup is the tuple <c>(symbol, from, isTypeOnly, kind)</c>.
/// </summary>
public sealed class TypeScriptSourceFileBuilder
{
    private readonly Dictionary<ImportKey, IdentifierNode> _imports = new();
    private readonly List<(string From, ImportKind Kind, bool IsTypeOnly, List<IdentifierNode> Items)> _importOrder = new();
    private readonly List<DeclarationNode> _declarations = new();
    private readonly List<ExportNode> _exports = new();
    private readonly Dictionary<string, object> _metadata = new();

    /// <summary>Idempotent — adds the (symbol, from) pair iff not already present.</summary>
    public TypeScriptSourceFileBuilder EnsureImport(
        string symbol, string from,
        bool isTypeOnly = false,
        ImportKind kind = ImportKind.Named)
    {
        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Import symbol must be non-empty", nameof(symbol));
        if (string.IsNullOrEmpty(from))
            throw new ArgumentException("Import 'from' module must be non-empty", nameof(from));

        var key = new ImportKey(symbol, from, isTypeOnly, kind);
        if (_imports.ContainsKey(key)) return this;

        var ident = new IdentifierNode(symbol);
        _imports[key] = ident;
        var existing = _importOrder.FirstOrDefault(g =>
            g.From == from && g.Kind == kind && g.IsTypeOnly == isTypeOnly);
        if (existing.Items != null)
        {
            existing.Items.Add(ident);
        }
        else
        {
            _importOrder.Add((from, kind, isTypeOnly, new List<IdentifierNode> { ident }));
        }
        return this;
    }

    /// <summary>Add multiple named imports from the same module in one call.</summary>
    public TypeScriptSourceFileBuilder EnsureImports(
        string from, IEnumerable<string> symbols,
        bool isTypeOnly = false)
    {
        foreach (var s in symbols) EnsureImport(s, from, isTypeOnly);
        return this;
    }

    /// <summary>Append a top-level declaration (function, interface, type alias, …).</summary>
    public TypeScriptSourceFileBuilder Add(DeclarationNode declaration)
    {
        _declarations.Add(declaration ?? throw new ArgumentNullException(nameof(declaration)));
        return this;
    }

    /// <summary>Append an <see cref="ExportNode"/>.</summary>
    public TypeScriptSourceFileBuilder AddExport(ExportNode export)
    {
        _exports.Add(export ?? throw new ArgumentNullException(nameof(export)));
        return this;
    }

    /// <summary>Set a metadata flag (e.g. <c>"directive"</c> → <c>"use server"</c>).</summary>
    public TypeScriptSourceFileBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    /// <summary>Produce a fully-formed <see cref="SourceFileNode"/>.</summary>
    public SourceFileNode Build()
    {
        var imports = _importOrder
            .Select(g => new ImportNode(
                items: g.Items.ToImmutableList(),
                from: g.From,
                kind: g.Kind,
                isTypeOnly: g.IsTypeOnly))
            .ToImmutableList();

        return new SourceFileNode(
            imports: imports,
            exports: _exports.ToImmutableList(),
            declarations: _declarations.ToImmutableList(),
            metadata: _metadata.Count == 0 ? null : _metadata.ToImmutableDictionary());
    }

    private readonly record struct ImportKey(string Symbol, string From, bool IsTypeOnly, ImportKind Kind);
}
