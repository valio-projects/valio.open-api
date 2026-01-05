using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел свойства объекта Zod схемы
/// </summary>
public sealed class ZodPropertyNode : AstNode
{
    /// <summary>
    /// Имя свойства
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Zod схема свойства
    /// </summary>
    public ZodSchemaNode Schema { get; }
    
    /// <summary>
    /// Является ли свойство обязательным
    /// </summary>
    public bool IsRequired { get; }
    
    /// <summary>
    /// Является ли свойство nullable
    /// </summary>
    public bool IsNullable { get; }
    
    /// <summary>
    /// Является ли свойство optional
    /// </summary>
    public bool IsOptional { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodProperty;
    
    public override IReadOnlyList<AstNode> Children => new[] { Name, Schema };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodPropertyNode(
        IdentifierNode name,
        ZodSchemaNode schema,
        bool isRequired = true,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        IsRequired = isRequired;
        IsNullable = isNullable;
        IsOptional = isOptional;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodProperty(this);
        }
        return visitor.VisitDefault(this);
    }
    
    public override AstNode Clone()
    {
        return new ZodPropertyNode(
            (IdentifierNode)Name.Clone(),
            (ZodSchemaNode)Schema.Clone(),
            IsRequired,
            IsNullable,
            IsOptional,
            Metadata.ToImmutableDictionary());
    }
}
