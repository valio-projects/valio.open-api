using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>
/// Интерфейс построителя AST для C#
/// </summary>
public interface ICSharpAstBuilder
{
    /// <summary>
    /// Строит CompilationUnit из ModelTemplateData
    /// </summary>
    CompilationUnitNode BuildCompilationUnit(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит CompilationUnit из ClientTemplateData
    /// </summary>
    CompilationUnitNode BuildCompilationUnit(ClientTemplateData<CSharpClientTemplateDataOptions, CSharpOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит CompilationUnit из EnumTemplateData
    /// </summary>
    CompilationUnitNode BuildCompilationUnit(EnumTemplateData<CSharpEnumTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Namespace
    /// </summary>
    NamespaceDeclarationNode BuildNamespace(string name, IReadOnlyList<DeclarationNode> declarations);
    
    /// <summary>
    /// Строит Class
    /// </summary>
    ClassDeclarationNode BuildClass(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Record
    /// </summary>
    RecordDeclarationNode BuildRecord(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Struct
    /// </summary>
    StructDeclarationNode BuildStruct(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Property
    /// </summary>
    PropertyDeclarationNode BuildProperty(PropertyTemplateData<CSharpPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Method
    /// </summary>
    MethodDeclarationNode BuildMethod(OperationTemplateData<CSharpOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Type из TypeInfo
    /// </summary>
    TypeNode BuildType(TypeInfo typeInfo);
    
    /// <summary>
    /// Строит Literal Expression
    /// </summary>
    Expressions.LiteralExpressionNode BuildLiteral(object value, TypeInfo typeInfo);
}
