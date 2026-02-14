using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Строитель AST для UE модели (USTRUCT) из IR представления.
/// Генерирует CppHeaderFileNode, содержащий один UStructDeclarationNode.
/// </summary>
public sealed class UEModelAstBuilder
{
    private readonly UETypeMapper _typeMapper;
    private readonly UENamingConvention _naming;
    private readonly CppGenerationOptions _options;

    public UEModelAstBuilder(UETypeMapper typeMapper, UENamingConvention naming, CppGenerationOptions options)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Строит CppHeaderFileNode из IrModel
    /// </summary>
    public CppHeaderFileNode Build(IrModel model)
    {
        var structName = _naming.StructName(model.Name);
        var fileName = _naming.ModelFileName(model.Name);

        var fields = model.Properties
            .Select(BuildField)
            .ToList();

        var structNode = new UStructDeclarationNode(
            name: new IdentifierNode(structName),
            specifiers: new UStructSpecifiers { BlueprintType = true },
            fields: fields,
            documentation: BuildDocumentation(model.Description));

        var includes = CollectIncludes(model, fileName);

        return new CppHeaderFileNode(
            fileName: fileName,
            headerComment: BuildCopyrightComment(),
            includes: includes,
            declarations: new AstNode[] { structNode });
    }

    /// <summary>
    /// Строит UPropertyFieldNode из IrProperty
    /// </summary>
    private UPropertyFieldNode BuildField(IrProperty property)
    {
        var type = _typeMapper.MapType(property.Type);
        var defaultValueStr = _typeMapper.GetDefaultValue(property.Type);
        ExpressionNode? defaultValue = defaultValueStr != null
            ? new LiteralExpressionNode(defaultValueStr, LiteralKind.String)
            : null;

        return new UPropertyFieldNode(
            name: new IdentifierNode(_naming.PropertyName(property.Name)),
            type: type,
            specifiers: UPropertySpecifiers.ModelField(_options.BlueprintCategory),
            defaultValue: defaultValue,
            documentation: BuildDocumentation(property.Description));
    }

    /// <summary>
    /// Собирает необходимые #include директивы для модели
    /// </summary>
    private List<CppIncludeDirectiveNode> CollectIncludes(IrModel model, string fileName)
    {
        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new(fileName.Replace(".h", _options.GeneratedHeaderSuffix))
        };

        // Добавляем includes для зависимых моделей
        var referencedModels = new HashSet<string>();
        foreach (var property in model.Properties)
        {
            CollectModelReferences(property.Type, referencedModels);
        }

        foreach (var refModel in referencedModels.OrderBy(x => x))
        {
            var refFileName = _naming.ModelFileName(refModel);
            includes.Add(new CppIncludeDirectiveNode(refFileName));
        }

        return includes;
    }

    /// <summary>
    /// Рекурсивно собирает ссылки на модели из типа
    /// </summary>
    private static void CollectModelReferences(IrType type, HashSet<string> models)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                models.Add(modelRef.ModelName);
                break;
            case IrArrayType array:
                CollectModelReferences(array.ElementType, models);
                break;
            case IrDictionaryType dict:
                CollectModelReferences(dict.KeyType, models);
                CollectModelReferences(dict.ValueType, models);
                break;
            case IrNullableType nullable:
                CollectModelReferences(nullable.InnerType, models);
                break;
            case IrOptionalType optional:
                CollectModelReferences(optional.InnerType, models);
                break;
            case IrUnionType union:
                foreach (var t in union.Types) CollectModelReferences(t, models);
                break;
            case IrIntersectionType intersection:
                foreach (var t in intersection.Types) CollectModelReferences(t, models);
                break;
        }
    }

    private CommentNode? BuildDocumentation(string? description)
    {
        return !string.IsNullOrWhiteSpace(description)
            ? new CommentNode(description, CommentKind.MultiLine)
            : null;
    }

    private CommentNode? BuildCopyrightComment()
    {
        return _options.Copyright != null
            ? new CommentNode(_options.Copyright, CommentKind.MultiLine)
            : null;
    }
}
