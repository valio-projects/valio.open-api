using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Строитель AST для типизированного Result struct из IrOperation.
/// Генерирует CppHeaderFileNode, содержащий UStructDeclarationNode для результата API-метода.
/// Паттерн: StatusCode + SuccessBody + ErrorBody + IsSuccess()
/// </summary>
public sealed class UEResultAstBuilder
{
    private readonly UETypeMapper _typeMapper;
    private readonly UENamingConvention _naming;
    private readonly CppGenerationOptions _options;

    public UEResultAstBuilder(UETypeMapper typeMapper, UENamingConvention naming, CppGenerationOptions options)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Строит CppHeaderFileNode для Result-структуры операции
    /// </summary>
    public CppHeaderFileNode Build(IrOperation operation)
    {
        var resultName = _naming.ResultStructName(operation.MethodName);
        var fileName = _naming.ResultFileName(operation.MethodName);
        var category = _options.BlueprintCategory;

        var fields = new List<UPropertyFieldNode>();

        // StatusCode — всегда присутствует
        fields.Add(new UPropertyFieldNode(
            name: new IdentifierNode("StatusCode"),
            type: new SimpleTypeNode(new IdentifierNode("int32"), isPrimitive: true),
            specifiers: UPropertySpecifiers.ResultField(category),
            defaultValue: new LiteralExpressionNode(0, LiteralKind.Number)));

        // SuccessBody — присутствует если есть 2xx ответ с телом
        var successResponse = FindSuccessResponse(operation);
        if (successResponse?.Type != null)
        {
            fields.Add(new UPropertyFieldNode(
                name: new IdentifierNode("SuccessBody"),
                type: _typeMapper.MapType(successResponse.Type),
                specifiers: UPropertySpecifiers.ResultField(category),
                documentation: new CommentNode(
                    $"Valid when StatusCode is 2xx ({successResponse.StatusCode}).",
                    CommentKind.MultiLine)));
        }

        // ErrorBody — для обработки ошибок 4xx/5xx
        fields.Add(new UPropertyFieldNode(
            name: new IdentifierNode("ErrorBody"),
            type: new SimpleTypeNode(new IdentifierNode("FRestApiResponse")),
            specifiers: UPropertySpecifiers.ResultField(category),
            documentation: new CommentNode(
                "Valid when StatusCode 400 or 500. For 400 use Error.message, for 500 use Exception.message.",
                CommentKind.MultiLine)));

        // IsSuccess() inline метод
        var isSuccessMethod = new CppFreeFunctionNode(
            name: new IdentifierNode("IsSuccess"),
            returnType: new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
            isDeclarationOnly: false); // будет отрендерен inline

        var structNode = new UStructDeclarationNode(
            name: new IdentifierNode(resultName),
            specifiers: new UStructSpecifiers { BlueprintType = true },
            fields: fields,
            inlineMethods: new[] { isSuccessMethod },
            documentation: new CommentNode(
                $"Typed result for {operation.MethodName}.",
                CommentKind.MultiLine));

        var includes = new List<CppIncludeDirectiveNode>
        {
            new("CoreMinimal.h"),
            new(fileName.Replace(".h", _options.GeneratedHeaderSuffix)),
            new(_naming.ModelIncludePath("RestApiResponse")) // ErrorBody type
        };

        // Include для SuccessBody type если это ссылка на модель
        if (successResponse?.Type is IrModelReference modelRef)
        {
            includes.Add(new CppIncludeDirectiveNode(_naming.ModelIncludePath(modelRef.ModelName)));
        }

        return new CppHeaderFileNode(
            fileName: fileName,
            headerComment: _options.Copyright != null ? new CommentNode(_options.Copyright, CommentKind.MultiLine) : null,
            includes: includes,
            declarations: new AstNode[] { structNode });
    }

    /// <summary>
    /// Определяет есть ли успешный тип ответа (для SuccessBody)
    /// </summary>
    public bool HasSuccessBody(IrOperation operation)
    {
        return FindSuccessResponse(operation)?.Type != null;
    }

    /// <summary>
    /// Находит первый 2xx ответ с телом
    /// </summary>
    private static IrResponse? FindSuccessResponse(IrOperation operation)
    {
        return operation.Responses.FirstOrDefault(r =>
            r.StatusCode.StartsWith('2') && r.Type != null);
    }
}
