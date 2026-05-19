using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.TypeScript.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Интерфейс построителя AST для TypeScript
/// </summary>
public interface ITypeScriptAstBuilder
{
    /// <summary>
    /// Строит TypeAlias
    /// </summary>
    TypeAliasDeclarationNode BuildTypeAlias(string name, TypeNode type);

    // IR → AST методы

    /// <summary>
    /// Строит TypeNode из IrType
    /// </summary>
    TypeNode BuildType(IrType irType);

    /// <summary>
    /// Строит SourceFile из IrModel
    /// </summary>
    SourceFileNode BuildSourceFile(IrModel model, string? importPath = null, FileOrganization? fileOrganization = null);

    /// <summary>
    /// Строит InterfaceDeclarationNode из IrModel
    /// </summary>
    InterfaceDeclarationNode BuildInterface(IrModel model);

    /// <summary>
    /// Строит PropertyDeclarationNode из IrProperty
    /// </summary>
    PropertyDeclarationNode BuildProperty(IrProperty property);

    /// <summary>
    /// Строит SourceFile из IrEnum
    /// </summary>
    SourceFileNode BuildSourceFile(IrEnum enumModel, string? importPath = null, FileOrganization? fileOrganization = null);

    /// <summary>
    /// Строит FunctionDeclarationNode из IrOperation
    /// </summary>
    FunctionDeclarationNode BuildFunction(IrOperation operation, bool isExported = false);

    /// <summary>
    /// Строит SourceFile из IrClient
    /// </summary>
    SourceFileNode BuildSourceFile(IrClient client, string? importPath = null, FileOrganization? fileOrganization = null);

    /// <summary>
    /// Строит список SourceFile из IrDocument (модели, enum'ы и опционально endpoint-клиенты).
    /// </summary>
    IReadOnlyList<SourceFileNode> BuildSourceFiles(
        IrDocument document,
        string? importPath = null,
        FileOrganization? fileOrganization = null,
        bool includeOperationEndpointFiles = false);

    /// <summary>
    /// Endpoint-файл для одной операции (axios или fetch).
    /// </summary>
    SourceFileNode BuildSourceFileForOperation(
        IrOperation operation,
        string? importPath = null,
        FileOrganization? fileOrganization = null,
        string transport = "axios");
}
