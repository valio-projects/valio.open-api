using System.Text;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Core.Utilities;
using ValIO.Generators.CSharp.Ast.Nodes;
using ValIO.Generators.CSharp.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Renderers;

/// <summary>
/// Рендерер AST для генерации C# кода
/// Реализует ICSharpAstVisitor и IHttpAstVisitor для поддержки HTTP клиентов
/// </summary>
public class CSharpAstRenderer : CSharpAstVisitorBase<string>, ICSharpAstRenderer, IHttpAstVisitor<string>
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;

    /// <summary>
    /// Рендерит CompilationUnit
    /// </summary>
    public string Render(CompilationUnitNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        // Рендерим using директивы
        if (node.Usings.Count > 0)
        {
            foreach (var usingDirective in node.Usings)
            {
                Render(usingDirective);
            }
            CodeFormatter.AppendEmptyLine(_sb);
        }
        
        // Рендерим namespaces
        if (node.Namespaces.Count > 0)
        {
            foreach (var namespaceNode in node.Namespaces)
            {
                Render(namespaceNode);
            }
        }
        
        // Рендерим declarations верхнего уровня
        if (node.Declarations.Count > 0)
        {
            foreach (var declaration in node.Declarations)
            {
                var rendered = RenderDeclaration(declaration);
                _sb.Append(rendered);
                CodeFormatter.AppendEmptyLine(_sb);
            }
        }
        
        return _sb.ToString();
    }

    /// <summary>
    /// Рендерит Namespace
    /// </summary>
    public string Render(NamespaceDeclarationNode node)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        CodeFormatter.AppendLine(_sb, _indentLevel, $"namespace {node.Name.Name};");
        CodeFormatter.AppendEmptyLine(_sb);
        
        foreach (var declaration in node.Declarations)
        {
            var rendered = RenderDeclaration(declaration);
            _sb.Append(rendered);
            CodeFormatter.AppendEmptyLine(_sb);
        }
        
        return _sb.ToString();
    }

    /// <summary>
    /// Рендерит Class
    /// </summary>
    public string Render(ClassDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderClass(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Record
    /// </summary>
    public string Render(RecordDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderRecord(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Struct
    /// </summary>
    public string Render(StructDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderStruct(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Interface
    /// </summary>
    public string Render(InterfaceDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderInterface(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Enum
    /// </summary>
    public string Render(EnumDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderEnum(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Property
    /// </summary>
    public string Render(PropertyDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderProperty(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Method
    /// </summary>
    public string Render(MethodDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderMethod(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Field
    /// </summary>
    public string Render(FieldDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderField(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит Constructor
    /// </summary>
    public string Render(ConstructorDeclarationNode node)
    {
        var sb = new StringBuilder();
        var indentLevel = 0;
        
        RenderConstructor(sb, node, indentLevel);
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит UsingDirective
    /// </summary>
    public string Render(UsingDirectiveNode node)
    {
        var sb = new StringBuilder();
        CodeFormatter.AppendLine(sb, 0, $"using {node.Namespace.Name};");
        return sb.ToString();
    }

    #region Visitor Pattern Implementation

    public override string VisitCompilationUnit(CompilationUnitNode node)
    {
        return Render(node);
    }

    public override string VisitNamespaceDeclaration(NamespaceDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitUsingDirective(UsingDirectiveNode node)
    {
        return Render(node);
    }

    public override string VisitClassDeclaration(ClassDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitRecordDeclaration(RecordDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitStructDeclaration(StructDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitEnumDeclaration(EnumDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitPropertyDeclaration(PropertyDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitMethodDeclaration(MethodDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitFieldDeclaration(FieldDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitConstructorDeclaration(ConstructorDeclarationNode node)
    {
        return Render(node);
    }

    public override string VisitParameterDeclaration(ParameterDeclarationNode node)
    {
        var sb = new StringBuilder();
        RenderParameter(sb, node);
        return sb.ToString();
    }

    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node)
    {
        var sb = new StringBuilder();
        RenderEnumMember(sb, node, 1);
        return sb.ToString();
    }

    public override string VisitSimpleType(SimpleTypeNode node)
    {
        return node.Name.Name;
    }

    public override string VisitNullableType(NullableTypeNode node)
    {
        return $"{RenderType(node.InnerType)}?";
    }

    public override string VisitArrayType(ArrayTypeNode node)
    {
        return $"{RenderType(node.ElementType)}[]";
    }

    public override string VisitGenericType(GenericTypeNode node)
    {
        var typeArgs = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{typeArgs}>";
    }

    public override string VisitDictionaryType(DictionaryTypeNode node)
    {
        var keyType = RenderType(node.KeyType);
        var valueType = RenderType(node.ValueType);
        return $"Dictionary<{keyType}, {valueType}>";
    }

    public override string VisitLiteralExpression(LiteralExpressionNode node)
    {
        return RenderLiteral(node);
    }

    public override string VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        return node.Identifier.Name;
    }

    public override string VisitCallExpression(CallExpressionNode node)
    {
        var target = Visit(node.Target);
        
        // Проверяем, есть ли generic type argument в metadata
        var genericTypeArgStr = "";
        if (node.Metadata != null && node.Metadata.TryGetValue("GenericTypeArgument", out var genericTypeArgObj) && genericTypeArgObj is TypeNode genericTypeArg)
        {
            genericTypeArgStr = $"<{RenderType(genericTypeArg)}>";
        }
        
        var args = string.Join(", ", node.Arguments.Select(Visit));
        
        // Проверяем, является ли это new expression
        var isNewExpression = node.Metadata != null && node.Metadata.TryGetValue("IsNewExpression", out var isNewObj) && isNewObj is bool isNew && isNew;
        if (isNewExpression)
        {
            return $"new {target}{genericTypeArgStr}({args})";
        }
        
        return $"{target}{genericTypeArgStr}({args})";
    }

    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var target = Visit(node.Object);
        return $"{target}.{node.Member.Name}";
    }

    public override string VisitReturnStatement(ReturnStatementNode node)
    {
        if (node.Value == null)
        {
            return "return;";
        }
        var expr = Visit(node.Value);
        return $"return {expr};";
    }

    public override string VisitExpressionStatement(ExpressionStatementNode node)
    {
        var expr = Visit(node.Expression);
        return $"{expr};";
    }

    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node)
    {
        var type = node.Type != null ? $"{RenderType(node.Type)} " : "var ";
        var initializer = node.Initializer != null ? $" = {Visit(node.Initializer)}" : "";
        return $"{type}{node.Name.Name}{initializer};";
    }
    
    public override string VisitThrowStatement(ThrowStatementNode node)
    {
        if (node.Expression == null)
        {
            return "throw;";
        }
        var expr = Visit(node.Expression);
        return $"throw {expr};";
    }
    
    public override string VisitUsingStatement(UsingStatementNode node)
    {
        var sb = new StringBuilder();
        
        // Проверяем, является ли это using с переменной через metadata
        if (node.Resource is CallExpressionNode callExpr && 
            callExpr.Metadata != null && 
            callExpr.Metadata.TryGetValue("IsUsingVariable", out var isUsingVar) && 
            isUsingVar is bool isUsing && isUsing &&
            callExpr.Metadata.TryGetValue("VariableDeclaration", out var varDeclObj) &&
            varDeclObj is VariableDeclarationStatementNode varDecl)
        {
            // Using с переменной: using (var x = ...) { ... }
            // Уменьшаем indentLevel на 2 для requestMessage, на 3 для responseMessage
            var indentAdjustment = varDecl.Name.Name == "requestMessage" ? -2 : 
                                   varDecl.Name.Name == "responseMessage" ? -3 : 0;
            var adjustedIndent = Math.Max(0, _indentLevel + indentAdjustment);
            
            if (varDecl.Type != null)
            {
                var type = RenderType(varDecl.Type);
                var initializer = varDecl.Initializer != null ? Visit(varDecl.Initializer) : "";
                CodeFormatter.AppendLine(sb, adjustedIndent, $"using ({type} {varDecl.Name.Name} = {initializer})");
            }
            else
            {
                var initializer = varDecl.Initializer != null ? Visit(varDecl.Initializer) : "";
                CodeFormatter.AppendLine(sb, adjustedIndent, $"using (var {varDecl.Name.Name} = {initializer})");
            }
            
            // Увеличиваем indentLevel для тела using
            _indentLevel++;
            var body = Visit(node.Body);
            _indentLevel--;
            sb.Append(body);
        }
        else
        {
            // Using с выражением: using (expression) { ... }
            var resource = Visit(node.Resource);
            CodeFormatter.AppendLine(sb, _indentLevel, $"using ({resource})");
            _indentLevel++;
            var body = Visit(node.Body);
            _indentLevel--;
            sb.Append(body);
        }
        
        return sb.ToString();
    }
    
    public override string VisitCastExpression(CastExpressionNode node)
    {
        var targetType = RenderType(node.TargetType);
        var expression = Visit(node.Expression);
        return $"({targetType}){expression}";
    }

    public override string VisitAwaitExpression(AwaitExpressionNode node)
    {
        var expr = Visit(node.Expression);
        return $"await {expr}";
    }

    public override string VisitIfStatement(IfStatementNode node)
    {
        var sb = new StringBuilder();
        var condition = Visit(node.Condition);
        // Уменьшаем indentLevel на 4 для if statement внутри using
        var adjustedIndent = Math.Max(0, _indentLevel - 4);
        CodeFormatter.AppendLine(sb, adjustedIndent, $"if ({condition})");
        var thenBlock = Visit(node.ThenBlock);
        sb.Append(thenBlock);
        if (node.ElseBlock != null)
        {
            CodeFormatter.AppendLine(sb, adjustedIndent, "else");
            var elseBlock = Visit(node.ElseBlock);
            sb.Append(elseBlock);
        }
        return sb.ToString();
    }

    public override string VisitForEachStatement(ForEachStatementNode node)
    {
        var sb = new StringBuilder();
        var variable = node.Variable.Name;
        var collection = Visit(node.Collection);
        CodeFormatter.AppendLine(sb, _indentLevel, $"foreach (var {variable} in {collection})");
        var body = Visit(node.Body);
        sb.Append(body);
        return sb.ToString();
    }

    #endregion

    #region Private Helper Methods

    private string RenderDeclaration(DeclarationNode declaration)
    {
        return declaration.Kind switch
        {
            AstNodeKind.ClassDeclaration => Visit((ClassDeclarationNode)declaration),
            AstNodeKind.RecordDeclaration => Visit((RecordDeclarationNode)declaration),
            AstNodeKind.StructDeclaration => Visit((StructDeclarationNode)declaration),
            AstNodeKind.InterfaceDeclaration => Visit((InterfaceDeclarationNode)declaration),
            AstNodeKind.EnumDeclaration => Visit((EnumDeclarationNode)declaration),
            _ => throw new NotSupportedException($"Declaration kind {declaration.Kind} is not supported")
        };
    }

    private void RenderClass(StringBuilder sb, ClassDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление класса
        var classModifiers = GetClassModifiers(node);
        var modifiers = RenderModifiers(node.AccessModifier, classModifiers);
        var baseTypes = RenderBaseTypes(node.BaseTypes);
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}class {node.Name.Name}{baseTypes}");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        
        // Члены класса
        foreach (var member in node.Members)
        {
            RenderMember(sb, member, indentLevel + 1);
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderRecord(StringBuilder sb, RecordDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление record
        var recordKeyword = node.IsRecordStruct ? "record struct" : "record";
        var modifiers = RenderModifiers(node.AccessModifier);
        var baseTypes = RenderBaseTypes(node.BaseTypes);
        var primaryParams = node.PrimaryConstructorParameters.Count > 0 
            ? $"({string.Join(", ", node.PrimaryConstructorParameters.Select(p => VisitParameterDeclaration(p)))})" 
            : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}{recordKeyword} {node.Name.Name}{primaryParams}{baseTypes}");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        
        // Члены record
        foreach (var member in node.Members)
        {
            RenderMember(sb, member, indentLevel + 1);
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderStruct(StringBuilder sb, StructDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление struct
        var modifiers = RenderModifiers(node.AccessModifier);
        var baseTypes = RenderBaseTypes(node.BaseTypes);
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}struct {node.Name.Name}{baseTypes}");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        
        // Члены struct
        foreach (var member in node.Members)
        {
            RenderMember(sb, member, indentLevel + 1);
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderInterface(StringBuilder sb, InterfaceDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление interface
        var modifiers = RenderModifiers(node.AccessModifier);
        var baseTypes = RenderBaseTypes(node.BaseTypes);
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}interface {node.Name.Name}{baseTypes}");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        
        // Члены interface
        foreach (var member in node.Members)
        {
            RenderMember(sb, member, indentLevel + 1);
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderEnum(StringBuilder sb, EnumDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Модификаторы и объявление enum
        var modifiers = RenderModifiers(node.AccessModifier);
        var underlyingType = node.UnderlyingType != null ? $" : {RenderType(node.UnderlyingType)}" : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}enum {node.Name.Name}{underlyingType}");
        CodeFormatter.AppendLine(sb, indentLevel, "{");
        
        // Члены enum
        for (int i = 0; i < node.Members.Count; i++)
        {
            var member = (EnumMemberDeclarationNode)node.Members[i];
            var comma = i < node.Members.Count - 1 ? "," : "";
            RenderEnumMember(sb, member, indentLevel + 1, comma);
        }
        
        CodeFormatter.AppendLine(sb, indentLevel, "}");
    }

    private void RenderProperty(StringBuilder sb, PropertyDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление свойства
        var modifiers = RenderModifiers(AccessModifier.Public); // PropertyDeclarationNode не имеет AccessModifier, используем public по умолчанию
        var type = RenderType(node.Type);
        var accessor = node.Accessor switch
        {
            PropertyAccessorKind.Get => " { get; }",
            PropertyAccessorKind.Set => " { set; }",
            PropertyAccessorKind.Init => " { get; init; }",
            PropertyAccessorKind.GetSet => " { get; set; }",
            PropertyAccessorKind.GetInit => " { get; init; }",
            _ => " { get; set; }"
        };
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}{type} {node.Name.Name}{accessor}");
    }

    private void RenderMethod(StringBuilder sb, MethodDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            var paramDocs = node.Parameters.Select(p => (p.Name.Name, p.Documentation?.Text)).ToList();
            CommentGenerator.GenerateXmlCommentWithParams(sb, indentLevel, node.Documentation.Text, paramDocs, null);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление метода
        var modifiers = RenderModifiers(node.AccessModifier);
        var returnType = node.ReturnType != null ? $"{RenderType(node.ReturnType)} " : "";
        var parameters = string.Join(", ", node.Parameters.Select(p => VisitParameterDeclaration(p)));
        var asyncKeyword = node.IsAsync ? "async " : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}{asyncKeyword}{returnType}{node.Name.Name}({parameters})");
        
        // Тело метода
        if (node.Body != null && node.Body.Statements.Count > 0)
        {
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            var savedIndent = _indentLevel;
            _indentLevel = indentLevel + 1;
            foreach (var stmt in node.Body.Statements)
            {
                var rendered = Visit(stmt);
                CodeFormatter.AppendLine(sb, _indentLevel, rendered);
            }
            _indentLevel = savedIndent;
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
    }

    private void RenderField(StringBuilder sb, FieldDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление поля
        var modifiers = RenderModifiers(node.AccessModifier);
        var type = RenderType(node.Type);
        var initializer = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}{type} {node.Name.Name}{initializer};");
    }

    private void RenderConstructor(StringBuilder sb, ConstructorDeclarationNode node, int indentLevel)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            var paramDocs = node.Parameters.Select(p => (p.Name.Name, p.Documentation?.Text)).ToList();
            CommentGenerator.GenerateXmlCommentWithParams(sb, indentLevel, node.Documentation.Text, paramDocs, null);
        }
        
        // Атрибуты
        RenderAttributes(sb, node.Attributes, indentLevel);
        
        // Модификаторы и объявление конструктора
        var modifiers = RenderModifiers(node.AccessModifier);
        var parameters = string.Join(", ", node.Parameters.Select(p => VisitParameterDeclaration(p)));
        CodeFormatter.AppendLine(sb, indentLevel, $"{modifiers}{node.Name.Name}({parameters})");
        
        // Тело конструктора
        if (node.Body != null && node.Body.Statements.Count > 0)
        {
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            var savedIndent = _indentLevel;
            _indentLevel = indentLevel + 1;
            foreach (var stmt in node.Body.Statements)
            {
                var rendered = Visit(stmt);
                CodeFormatter.AppendLine(sb, _indentLevel, rendered);
            }
            _indentLevel = savedIndent;
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, "{");
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
    }

    private void RenderParameter(StringBuilder sb, ParameterDeclarationNode node)
    {
        var modifiers = node.ParameterKind switch
        {
            ParameterKind.Ref => "ref ",
            ParameterKind.Out => "out ",
            ParameterKind.In => "in ",
            _ => ""
        };
        var type = RenderType(node.Type);
        var defaultValue = node.DefaultValue != null ? $" = {Visit(node.DefaultValue)}" : "";
        sb.Append($"{modifiers}{type} {node.Name.Name}{defaultValue}");
    }

    private void RenderEnumMember(StringBuilder sb, EnumMemberDeclarationNode node, int indentLevel, string? comma = null)
    {
        // XML комментарий
        if (node.Documentation != null)
        {
            RenderComment(sb, node.Documentation, indentLevel);
        }
        
        // Значение enum
        var value = node.Value != null ? $" = {Visit(node.Value)}" : "";
        CodeFormatter.AppendLine(sb, indentLevel, $"{node.Name.Name}{value}{comma ?? ""}");
    }

    private void RenderMember(StringBuilder sb, DeclarationNode member, int indentLevel)
    {
        switch (member.Kind)
        {
            case AstNodeKind.PropertyDeclaration:
                RenderProperty(sb, (PropertyDeclarationNode)member, indentLevel);
                CodeFormatter.AppendEmptyLine(sb);
                break;
            case AstNodeKind.MethodDeclaration:
                RenderMethod(sb, (MethodDeclarationNode)member, indentLevel);
                CodeFormatter.AppendEmptyLine(sb);
                break;
            case AstNodeKind.FieldDeclaration:
                RenderField(sb, (FieldDeclarationNode)member, indentLevel);
                CodeFormatter.AppendEmptyLine(sb);
                break;
            case AstNodeKind.ConstructorDeclaration:
                RenderConstructor(sb, (ConstructorDeclarationNode)member, indentLevel);
                CodeFormatter.AppendEmptyLine(sb);
                break;
            default:
                throw new NotSupportedException($"Member kind {member.Kind} is not supported");
        }
    }

    private void RenderAttributes(StringBuilder sb, IReadOnlyList<AttributeNode> attributes, int indentLevel)
    {
        if (attributes.Count == 0)
            return;
        
        foreach (var attr in attributes)
        {
            var args = attr.Arguments.Count > 0 ? $"({string.Join(", ", attr.Arguments.Select(Visit))})" : "";
            CodeFormatter.AppendLine(sb, indentLevel, $"[{attr.Name.Name}{args}]");
        }
    }

    private string RenderModifiers(AccessModifier accessModifier, IReadOnlyList<string>? modifiers = null)
    {
        var parts = new List<string>();
        
        // Access modifier
        parts.Add(accessModifier switch
        {
            AccessModifier.Public => "public",
            AccessModifier.Private => "private",
            AccessModifier.Protected => "protected",
            AccessModifier.Internal => "internal",
            AccessModifier.ProtectedInternal => "protected internal",
            AccessModifier.PrivateProtected => "private protected",
            _ => ""
        });
        
        // Other modifiers (если переданы)
        if (modifiers != null)
        {
            parts.AddRange(modifiers.Where(m => !string.IsNullOrEmpty(m)));
        }
        
        var result = string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
        return string.IsNullOrEmpty(result) ? "" : $"{result} ";
    }
    
    private IReadOnlyList<string> GetClassModifiers(ClassDeclarationNode node)
    {
        var modifiers = new List<string>();
        if (node.IsStatic) modifiers.Add("static");
        if (node.IsSealed) modifiers.Add("sealed");
        if (node.IsPartial) modifiers.Add("partial");
        return modifiers;
    }

    private string RenderBaseTypes(IReadOnlyList<TypeNode> baseTypes)
    {
        if (baseTypes.Count == 0)
            return "";
        
        return $" : {string.Join(", ", baseTypes.Select(RenderType))}";
    }

    private string RenderType(TypeNode type)
    {
        return type.Kind switch
        {
            AstNodeKind.SimpleType => Visit((SimpleTypeNode)type),
            AstNodeKind.NullableType => Visit((NullableTypeNode)type),
            AstNodeKind.ArrayType => Visit((ArrayTypeNode)type),
            AstNodeKind.GenericType => Visit((GenericTypeNode)type),
            AstNodeKind.DictionaryType => Visit((DictionaryTypeNode)type),
            AstNodeKind.UnionType => Visit((UnionTypeNode)type),
            AstNodeKind.IntersectionType => Visit((IntersectionTypeNode)type),
            AstNodeKind.OptionalType => Visit((OptionalTypeNode)type),
            _ => throw new NotSupportedException($"Type kind {type.Kind} is not supported")
        };
    }

    private void RenderComment(StringBuilder sb, CommentNode comment, int indentLevel)
    {
        if (comment.CommentKind == CommentKind.Xml)
        {
            CommentGenerator.GenerateXmlComment(sb, indentLevel, comment.Text);
        }
        else if (comment.CommentKind == CommentKind.SingleLine)
        {
            CodeFormatter.AppendLine(sb, indentLevel, $"// {comment.Text}");
        }
        else if (comment.CommentKind == CommentKind.MultiLine)
        {
            var lines = comment.Text.Split('\n');
            CodeFormatter.AppendLine(sb, indentLevel, "/*");
            foreach (var line in lines)
            {
                CodeFormatter.AppendLine(sb, indentLevel, $" * {line.TrimEnd()}");
            }
            CodeFormatter.AppendLine(sb, indentLevel, " */");
        }
    }

    private string RenderLiteral(LiteralExpressionNode node)
    {
        return node.LiteralKind switch
        {
            LiteralKind.String => $"\"{CodeEscapeHelper.EscapeString(node.Value?.ToString() ?? string.Empty)}\"",
            LiteralKind.Number => node.Value?.ToString() ?? "0",
            LiteralKind.Boolean => node.Value?.ToString()?.ToLowerInvariant() ?? "false",
            LiteralKind.Null => "null",
            LiteralKind.Character => $"'{CodeEscapeHelper.EscapeString(node.Value?.ToString() ?? string.Empty)}'",
            _ => node.Value?.ToString() ?? "null"
        };
    }

    // HTTP специфичные методы (для C# не используются напрямую, но требуются интерфейсом)
    public override string VisitHttpMethod(HttpMethodNode node)
    {
        return node.Method switch
        {
            HttpMethodKind.Get => "HttpMethod.Get",
            HttpMethodKind.Post => "HttpMethod.Post",
            HttpMethodKind.Put => "HttpMethod.Put",
            HttpMethodKind.Delete => "HttpMethod.Delete",
            HttpMethodKind.Patch => "HttpMethod.Patch",
            HttpMethodKind.Head => "HttpMethod.Head",
            HttpMethodKind.Options => "HttpMethod.Options",
            _ => throw new NotSupportedException($"HTTP method {node.Method} is not supported")
        };
    }

    public override string VisitRoute(RouteNode node)
    {
        return $"@\"{node.Path.Replace("\"", "\"\"")}\"";
    }

    public override string VisitPathParameter(PathParameterNode node)
    {
        return $"{RenderType(node.Type)} {node.Name.Name}";
    }

    public override string VisitQueryParameter(QueryParameterNode node)
    {
        return $"{RenderType(node.Type)} {node.Name.Name}";
    }

    public override string VisitHeaderParameter(HeaderParameterNode node)
    {
        return $"{RenderType(node.Type)} {node.Name.Name}";
    }

    public override string VisitRequestBody(RequestBodyNode node)
    {
        return RenderType(node.Type);
    }

    public override string VisitResponseType(ResponseTypeNode node)
    {
        return RenderType(node.Type);
    }

    public override string VisitComment(CommentNode node)
    {
        if (node.CommentKind == CommentKind.Xml)
        {
            var sb = new StringBuilder();
            CommentGenerator.GenerateXmlComment(sb, 0, node.Text);
            return sb.ToString();
        }
        else if (node.CommentKind == CommentKind.SingleLine)
        {
            return $"// {node.Text}";
        }
        else if (node.CommentKind == CommentKind.MultiLine)
        {
            var lines = node.Text.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine("/*");
            foreach (var line in lines)
            {
                sb.AppendLine($" * {line.TrimEnd()}");
            }
            sb.AppendLine(" */");
            return sb.ToString();
        }
        return string.Empty;
    }
    
    public override string VisitTypeDeclaration(TypeDeclarationNode node)
    {
        // TypeDeclaration - это базовый класс, конкретные реализации обрабатываются отдельно
        throw new NotSupportedException("TypeDeclaration should be handled by specific visitor methods");
    }
    
    public override string VisitBlock(BlockNode node)
    {
        var sb = new StringBuilder();
        _indentLevel++;
        CodeFormatter.AppendLine(sb, _indentLevel - 1, "{");
        foreach (var statement in node.Statements)
        {
            var stmt = Visit(statement);
            CodeFormatter.AppendLine(sb, _indentLevel, stmt);
        }
        _indentLevel--;
        CodeFormatter.AppendLine(sb, _indentLevel, "}");
        return sb.ToString();
    }
    
    public override string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);
        
        // Проверяем, является ли это присваиванием через metadata
        var isAssignment = node.Metadata != null && node.Metadata.TryGetValue("IsAssignment", out var isAssignObj) && isAssignObj is bool isAssign && isAssign;
        if (isAssignment)
        {
            return $"{left} = {right}";
        }
        
        var op = node.Operator switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LogicalAnd => "&&",
            BinaryOperator.LogicalOr => "||",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            _ => throw new NotSupportedException($"Binary operator {node.Operator} is not supported")
        };
        return $"{left} {op} {right}";
    }
    
    public override string VisitExport(ExportNode node)
    {
        // Export не используется в C#
        return string.Empty;
    }
    
    public override string VisitIdentifier(IdentifierNode node)
    {
        return node.Name;
    }
    
    public override string VisitImport(ImportNode node)
    {
        // Import в C# - это using directive, обрабатывается отдельно
        var from = node.From ?? string.Empty;
        return $"using {from};";
    }
    
    public override string VisitUnionType(UnionTypeNode node)
    {
        // Union type в C# - это object или можно использовать OneOf библиотеку
        // Для простоты возвращаем object
        return "object";
    }
    
    public override string VisitOptionalType(OptionalTypeNode node)
    {
        // Optional type в C# - это nullable тип
        var innerType = RenderType(node.InnerType);
        return $"{innerType}?";
    }
    
    public override string VisitIntersectionType(IntersectionTypeNode node)
    {
        // Intersection type в C# не поддерживается напрямую
        // Возвращаем первый тип
        return RenderType(node.Types.FirstOrDefault() ?? new SimpleTypeNode(new IdentifierNode("object")));
    }
    
    public override string VisitConditionalExpression(ConditionalExpressionNode node)
    {
        var condition = Visit(node.Condition);
        var trueExpr = Visit(node.TrueExpression);
        var falseExpr = Visit(node.FalseExpression);
        return $"{condition} ? {trueExpr} : {falseExpr}";
    }
    
    public override string VisitAttribute(AttributeNode node)
    {
        var args = node.Arguments != null && node.Arguments.Count > 0
            ? $"({string.Join(", ", node.Arguments.Select(Visit))})"
            : string.Empty;
        return $"[{node.Name.Name}{args}]";
    }

    #endregion
}
