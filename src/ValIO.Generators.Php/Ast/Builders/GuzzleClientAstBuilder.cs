using System.Collections.Immutable;
using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Php.Ast.Nodes;

namespace ValIO.Generators.Php.Ast.Builders;

/// <summary>
/// Строит <see cref="PhpSourceFileNode"/> с Guzzle-клиентом для одного IR-клиента (tag/group).
/// </summary>
public sealed class GuzzleClientAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly PhpIrTypeMapper _typeMapper;

    public GuzzleClientAstBuilder(INamingConverter namingConverter, PhpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }

    public PhpSourceFileNode BuildSourceFile(IrClient client, string @namespace)
    {
        var ns = NormalizeNamespace($"{@namespace}\\Api");
        var className = BuildClassName(client.Name);

        var uses = new List<PhpUseStatementNode>
        {
            new("GuzzleHttp\\ClientInterface"),
            new("Psr\\Http\\Message\\ResponseInterface"),
        };

        // Constructor promotion: private readonly ClientInterface $client.
        var ctorParam = new ParameterDeclarationNode(
            name: new IdentifierNode("client"),
            type: new SimpleTypeNode(new IdentifierNode("ClientInterface")),
            isRequired: true);
        var ctor = new PhpConstructorDeclarationNode(
            promotedParameters: new[]
            {
                new PhpPromotedParameter("private", true, ctorParam),
            });

        var methods = new List<DeclarationNode>();
        foreach (var op in client.Operations)
        {
            methods.Add(BuildOperationMethod(op));
        }

        var cls = new PhpClassDeclarationNode(
            name: new IdentifierNode(className),
            isFinal: true,
            constructor: ctor,
            members: methods,
            documentation: null);

        return new PhpSourceFileNode(
            @namespace: ns,
            uses: uses,
            declarations: new DeclarationNode[] { cls }.ToImmutableList());
    }

    private MethodDeclarationNode BuildOperationMethod(IrOperation op)
    {
        var methodName = _namingConverter.ToCamelCase(op.MethodName);
        var parameters = new List<ParameterDeclarationNode>();

        foreach (var p in op.PathParameters)
            parameters.Add(BuildParam(p));
        foreach (var p in op.QueryParameters)
            parameters.Add(BuildParam(p));
        foreach (var p in op.HeaderParameters)
            parameters.Add(BuildParam(p));
        if (op.RequestBody != null)
        {
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("body"),
                type: _typeMapper.MapType(op.RequestBody.Type),
                isRequired: op.RequestBody.IsRequired));
        }

        // Body: return $this->client->request('METHOD', "/path");
        var pathExpr = BuildPathExpression(op.Path, op.PathParameters);
        var requestCall = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(new IdentifierNode("$this")),
                    member: new IdentifierNode("client")),
                member: new IdentifierNode("request")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode(op.HttpMethod.ToUpperInvariant(), LiteralKind.String),
                pathExpr,
            }.ToImmutableList());

        var returnStmt = new ReturnStatementNode(requestCall);
        var body = new BlockNode(new StatementNode[] { returnStmt }.ToImmutableList());

        var doc = BuildOperationDoc(op);

        return new MethodDeclarationNode(
            name: new IdentifierNode(methodName),
            returnType: new SimpleTypeNode(new IdentifierNode("ResponseInterface")),
            parameters: parameters,
            body: body,
            accessModifier: AccessModifier.Public,
            documentation: doc);
    }

    private ParameterDeclarationNode BuildParam(IrParameter p)
    {
        var typeNode = _typeMapper.MapType(p.Type);
        if (!p.IsRequired && typeNode is not NullableTypeNode)
            typeNode = new NullableTypeNode(typeNode);
        ExpressionNode? defaultValue = null;
        if (!p.IsRequired)
            defaultValue = new LiteralExpressionNode(null, LiteralKind.Null);
        return new ParameterDeclarationNode(
            name: new IdentifierNode(p.Name),
            type: typeNode,
            isRequired: p.IsRequired,
            defaultValue: defaultValue);
    }

    private ExpressionNode BuildPathExpression(string path, IReadOnlyList<IrParameter> pathParameters)
    {
        if (pathParameters.Count == 0 || !path.Contains('{'))
        {
            return new LiteralExpressionNode(path, LiteralKind.String);
        }

        // Получаем PHP-интерполированную строку с {$var} и сохраняем её как «raw» строку.
        // PhpAstRenderer обрабатывает строковые литералы как single-quoted; для интерполяции
        // нам нужно поставить double-quoted строку. Используем RawTypeNode-нет — нужно
        // получать выражение нативно. Используем CallExpression к sprintf, как стабильную опцию.
        var format = new StringBuilder();
        var args = new List<ExpressionNode>();
        int i = 0;
        while (i < path.Length)
        {
            if (path[i] == '{')
            {
                var end = path.IndexOf('}', i);
                if (end < 0) { format.Append(path[i++]); continue; }
                var name = path.Substring(i + 1, end - i - 1);
                format.Append("%s");
                args.Add(new IdentifierExpressionNode(new IdentifierNode($"this->encode($" + name + ")")));
                // Простоты ради: используем имя как $name — реальный код:
                args[^1] = new IdentifierExpressionNode(new IdentifierNode("$" + _namingConverter.ToCamelCase(name)));
                i = end + 1;
            }
            else
            {
                format.Append(path[i++]);
            }
        }

        var sprintfArgs = new List<ExpressionNode>
        {
            new LiteralExpressionNode(format.ToString(), LiteralKind.String),
        };
        sprintfArgs.AddRange(args);
        return new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("sprintf")),
            arguments: sprintfArgs.ToImmutableList());
    }

    private static CommentNode? BuildOperationDoc(IrOperation op)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(op.Summary))
            lines.Add(op.Summary!);
        if (!string.IsNullOrWhiteSpace(op.Description))
        {
            if (lines.Count > 0) lines.Add("");
            lines.Add(op.Description!);
        }
        if (lines.Count == 0 && !string.IsNullOrWhiteSpace(op.MethodName))
            lines.Add($"@description {op.MethodName}");
        return lines.Count > 0
            ? new CommentNode(string.Join("\n", lines), CommentKind.MultiLine)
            : null;
    }

    private static string BuildClassName(string clientName)
    {
        if (string.IsNullOrWhiteSpace(clientName)) return "ApiClient";
        return clientName.EndsWith("Api", StringComparison.OrdinalIgnoreCase)
            ? clientName
            : clientName + "Api";
    }

    private static string NormalizeNamespace(string ns) => ns.Trim('\\').Replace('/', '\\');
}
