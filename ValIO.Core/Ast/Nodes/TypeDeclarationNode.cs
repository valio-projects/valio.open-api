using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Базовый класс для объявлений типов (класс, интерфейс, enum и т.д.)
/// </summary>
public abstract class TypeDeclarationNode : DeclarationNode
{
    /// <summary>
    /// Модификатор доступа
    /// </summary>
    public AccessModifier AccessModifier { get; }
    
    /// <summary>
    /// Базовые типы/интерфейсы
    /// </summary>
    public IReadOnlyList<TypeNode> BaseTypes { get; }
    
    /// <summary>
    /// Члены типа (свойства, методы и т.д.)
    /// </summary>
    public IReadOnlyList<DeclarationNode> Members { get; }
    
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            children.AddRange(BaseTypes);
            children.AddRange(Members);
            return children;
        }
    }
    
    /// <summary>
    /// Конструктор
    /// </summary>
    protected TypeDeclarationNode(
        IdentifierNode name,
        AccessModifier accessModifier = AccessModifier.Public,
        IReadOnlyList<TypeNode>? baseTypes = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        AccessModifier = accessModifier;
        BaseTypes = baseTypes?.ToImmutableList() ?? ImmutableList<TypeNode>.Empty;
        Members = members?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
    }
    
    public override string ToString()
    {
        var baseTypesStr = BaseTypes.Count > 0 
            ? $" : {string.Join(", ", BaseTypes)}" 
            : "";
        var membersCount = Members.Count;
        return $"{Kind}({AccessModifier} {Name.Name}{baseTypesStr}) [{membersCount} members]";
    }
}
