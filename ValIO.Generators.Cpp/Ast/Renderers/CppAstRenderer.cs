using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Ast.Visitors;

namespace ValIO.Generators.Cpp.Ast.Renderers;

/// <summary>
/// Рендерер C++/UE AST в строковый код.
/// Реализует Visitor Pattern для обхода всех типов C++/UE AST узлов.
/// Использует tab-based отступы (UE-конвенция).
/// </summary>
public sealed class CppAstRenderer : CppAstVisitorBase<string>
{
    private int _indentLevel;
    private const string IndentChar = "\t";

    /// <summary>
    /// Рендерит AST узел верхнего уровня (CppHeaderFileNode или CppSourceFileNode)
    /// </summary>
    public string Render(AstNode node)
    {
        _indentLevel = 0;
        return Visit(node);
    }

    // ===================================================================
    // File-level nodes
    // ===================================================================

    public override string VisitCppHeaderFile(CppHeaderFileNode node)
    {
        var sb = new StringBuilder();

        // Copyright / header comment
        if (node.HeaderComment != null)
        {
            sb.AppendLine(RenderBlockComment(node.HeaderComment.Text));
            sb.AppendLine();
        }

        // #pragma once
        if (node.PragmaOnce)
        {
            sb.AppendLine("#pragma once");
            sb.AppendLine();
        }

        // #includes
        foreach (var include in node.Includes)
            sb.AppendLine(Visit(include));
        if (node.Includes.Count > 0) sb.AppendLine();

        // Forward declarations
        if (node.ForwardDeclarations.Count > 0)
        {
            sb.AppendLine(RenderSeparator("Forward declarations"));
            sb.AppendLine();
            foreach (var fwd in node.ForwardDeclarations)
                sb.AppendLine(Visit(fwd));
            sb.AppendLine();
        }

        // Using aliases
        foreach (var alias in node.UsingAliases)
            sb.AppendLine(Visit(alias));
        if (node.UsingAliases.Count > 0) sb.AppendLine();

        // Declarations
        for (var i = 0; i < node.Declarations.Count; i++)
        {
            sb.Append(Visit(node.Declarations[i]));
            // Пустая строка между объявлениями верхнего уровня уже добавляется самими узлами
        }

        return sb.ToString();
    }

    public override string VisitCppSourceFile(CppSourceFileNode node)
    {
        var sb = new StringBuilder();

        // Copyright / header comment
        if (node.HeaderComment != null)
        {
            sb.AppendLine(RenderBlockComment(node.HeaderComment.Text));
            sb.AppendLine();
        }

        // #includes
        foreach (var include in node.Includes)
            sb.AppendLine(Visit(include));
        if (node.Includes.Count > 0) sb.AppendLine();

        // Anonymous namespace (parse functions)
        if (node.AnonymousNamespaceMembers.Count > 0)
        {
            sb.AppendLine(RenderSeparator("Response parsing"));
            sb.AppendLine();
            sb.AppendLine("namespace");
            sb.AppendLine("{");
            _indentLevel++;
            foreach (var member in node.AnonymousNamespaceMembers)
            {
                sb.Append(Visit(member));
                sb.AppendLine();
            }
            _indentLevel--;
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Implementations — group by type via metadata
        if (node.Implementations.Count > 0)
        {
            // Native API methods
            var nativeFuncs = node.Implementations
                .Where(n => n.Metadata.ContainsKey("NativeApiFunction")).ToList();
            if (nativeFuncs.Count > 0)
            {
                sb.AppendLine(RenderSeparator("API methods"));
                sb.AppendLine();
                foreach (var impl in nativeFuncs)
                {
                    sb.Append(Visit(impl));
                    sb.AppendLine();
                }
            }

            // Blueprint async action implementations
            var asyncImpls = node.Implementations
                .Where(n => n.Metadata.ContainsKey("AsyncFactory")
                         || n.Metadata.ContainsKey("AsyncComplete")
                         || n.Metadata.ContainsKey("AsyncActivate")).ToList();
            if (asyncImpls.Count > 0)
            {
                sb.AppendLine(RenderSeparator("Blueprint async actions"));
                sb.AppendLine();
                foreach (var impl in asyncImpls)
                {
                    sb.Append(Visit(impl));
                    sb.AppendLine();
                }
            }

            // Path builder implementations
            var pathImpls = node.Implementations
                .Where(n => n.Metadata.ContainsKey("PathBuilder")).ToList();
            if (pathImpls.Count > 0)
            {
                sb.AppendLine(RenderSeparator("Path builders"));
                sb.AppendLine();
                foreach (var impl in pathImpls)
                {
                    sb.Append(Visit(impl));
                    sb.AppendLine();
                }
            }

            // Прочие
            var others = node.Implementations
                .Where(n => !n.Metadata.ContainsKey("NativeApiFunction")
                         && !n.Metadata.ContainsKey("AsyncFactory")
                         && !n.Metadata.ContainsKey("AsyncComplete")
                         && !n.Metadata.ContainsKey("AsyncActivate")
                         && !n.Metadata.ContainsKey("PathBuilder")).ToList();
            foreach (var impl in others)
            {
                sb.Append(Visit(impl));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public override string VisitCppIncludeDirective(CppIncludeDirectiveNode node)
    {
        return node.IsSystemInclude
            ? $"#include <{node.Path}>"
            : $"#include \"{node.Path}\"";
    }

    public override string VisitCppForwardDeclaration(CppForwardDeclarationNode node)
    {
        var keyword = node.DeclarationKind switch
        {
            CppForwardDeclarationKind.Class => "class",
            CppForwardDeclarationKind.Struct => "struct",
            CppForwardDeclarationKind.Enum => "enum class",
            _ => "class"
        };
        return $"{keyword} {node.Name.Name};";
    }

    public override string VisitCppUsingAlias(CppUsingAliasNode node)
    {
        return $"using {node.Alias.Name} = {RenderType(node.TargetType)};";
    }

    // ===================================================================
    // UE Declaration nodes
    // ===================================================================

    public override string VisitUStruct(UStructDeclarationNode node)
    {
        var sb = new StringBuilder();

        if (node.Documentation != null)
            sb.AppendLine(RenderDoxygenComment(node.Documentation.Text));

        sb.AppendLine(RenderUStructMacro(node.Specifiers));
        sb.AppendLine($"struct {node.Name.Name}");
        sb.AppendLine("{");
        _indentLevel++;

        sb.AppendLine($"{Indent()}GENERATED_BODY()");
        if (node.Fields.Count > 0) sb.AppendLine();

        foreach (var field in node.Fields)
            sb.Append(Visit(field));

        foreach (var method in node.InlineMethods)
        {
            if (method.Name.Name == "IsSuccess")
                sb.AppendLine($"{Indent()}bool IsSuccess() const {{ return StatusCode >= 200 && StatusCode < 300; }}");
        }

        _indentLevel--;
        sb.AppendLine("};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUPropertyField(UPropertyFieldNode node)
    {
        var sb = new StringBuilder();

        if (node.Documentation != null)
            sb.AppendLine($"{Indent()}{RenderDoxygenComment(node.Documentation.Text)}");

        sb.AppendLine($"{Indent()}{RenderUPropertyMacro(node.Specifiers)}");

        var typeStr = RenderType(node.Type);
        var defaultStr = node.DefaultValue != null ? $" = {RenderDefaultValue(node.DefaultValue)}" : "";
        sb.AppendLine($"{Indent()}{typeStr} {node.Name.Name}{defaultStr};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUClass(UClassDeclarationNode node)
    {
        var sb = new StringBuilder();

        if (node.Documentation != null)
            sb.AppendLine(RenderDoxygenComment(node.Documentation.Text));

        sb.AppendLine(RenderUClassMacro(node.Specifiers));

        var apiStr = !string.IsNullOrEmpty(node.ApiMacro) ? $"{node.ApiMacro} " : "";
        var basesStr = node.BaseTypes.Count > 0
            ? $" : public {string.Join(", public ", node.BaseTypes.Select(RenderType))}"
            : "";
        sb.AppendLine($"class {apiStr}{node.Name.Name}{basesStr}");
        sb.AppendLine("{");
        _indentLevel++;
        sb.AppendLine($"{Indent()}GENERATED_BODY()");
        _indentLevel--;

        // Public section
        if (node.PublicMembers.Count > 0)
        {
            sb.AppendLine("public:");
            _indentLevel++;
            foreach (var member in node.PublicMembers)
                sb.Append(Visit(member));
            _indentLevel--;
        }

        // Protected section
        if (node.ProtectedMembers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("protected:");
            _indentLevel++;
            foreach (var member in node.ProtectedMembers)
                sb.Append(Visit(member));
            _indentLevel--;
        }

        sb.AppendLine("};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUFunction(UFunctionDeclarationNode node)
    {
        var sb = new StringBuilder();

        if (node.Documentation != null)
            sb.AppendLine($"{Indent()}{RenderDoxygenComment(node.Documentation.Text)}");

        // UFUNCTION(...) — only emit if it has blueprint specifiers
        if (node.Specifiers.BlueprintCallable || node.Specifiers.BlueprintPure)
            sb.AppendLine($"{Indent()}{RenderUFunctionMacro(node.Specifiers)}");

        var modifiers = new List<string>();
        if (node.IsVirtual) modifiers.Add("virtual");
        if (node.IsStatic) modifiers.Add("static");

        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) : "void";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        var suffix = "";
        if (node.IsConst) suffix += " const";
        if (node.IsOverride) suffix += " override";

        var mods = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
        sb.AppendLine($"{Indent()}{mods}{returnStr} {node.Name.Name}({paramsStr}){suffix};");
        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitUDelegate(UDelegateDeclarationNode node)
    {
        var macroSuffix = node.GetMacroParamSuffix();
        var paramsStr = string.Join(", ", node.Parameters.Select(p => $"{RenderType(p.Type)}, {p.Name.Name}"));
        var args = node.Parameters.Count > 0
            ? $"{node.DelegateName.Name}, {paramsStr}"
            : node.DelegateName.Name;
        return $"DECLARE_DYNAMIC_MULTICAST_DELEGATE{macroSuffix}({args});\n";
    }

    public override string VisitUEnum(UEnumDeclarationNode node)
    {
        var sb = new StringBuilder();

        if (node.Documentation != null)
            sb.AppendLine(RenderDoxygenComment(node.Documentation.Text));

        sb.AppendLine("UENUM(BlueprintType)");
        sb.AppendLine($"enum class {node.Name.Name} : {RenderType(node.UnderlyingType)}");
        sb.AppendLine("{");
        _indentLevel++;

        for (var i = 0; i < node.Values.Count; i++)
        {
            var val = node.Values[i];
            var displayMeta = !string.IsNullOrEmpty(val.DisplayName)
                ? $" UMETA(DisplayName = \"{val.DisplayName}\")"
                : "";
            var comma = i < node.Values.Count - 1 ? "," : "";
            sb.AppendLine($"{Indent()}{val.Name.Name}{comma}{displayMeta}");
        }

        _indentLevel--;
        sb.AppendLine("};");
        sb.AppendLine();
        return sb.ToString();
    }

    // ===================================================================
    // C++ Function / Method nodes
    // ===================================================================

    public override string VisitCppFreeFunction(CppFreeFunctionNode node)
    {
        // Metadata-driven dispatch for specialized function bodies
        if (node.Metadata.ContainsKey("ParseFunction"))
            return RenderParseFunction(node);
        if (node.Metadata.ContainsKey("NativeApiFunction"))
            return RenderNativeApiFunction(node);

        var sb = new StringBuilder();

        if (node.Documentation != null)
            sb.AppendLine($"{Indent()}{RenderDoxygenComment(node.Documentation.Text)}");

        var returnStr = RenderType(node.ReturnType);
        var paramsStr = string.Join(",\n" + Indent() + "\t", node.Parameters.Select(RenderParameterInline));

        if (node.IsDeclarationOnly)
        {
            if (node.Parameters.Count <= 2)
            {
                var inlineParams = string.Join(", ", node.Parameters.Select(RenderParameterInline));
                sb.AppendLine($"{Indent()}{returnStr} {node.Name.Name}(");
                sb.AppendLine($"{Indent()}\t{inlineParams});");
            }
            else
            {
                sb.AppendLine($"{Indent()}{returnStr} {node.Name.Name}(");
                sb.AppendLine($"{Indent()}\t{paramsStr});");
            }
        }
        else if (node.Body != null)
        {
            var inlineParams = string.Join(", ", node.Parameters.Select(RenderParameterInline));
            sb.AppendLine($"{Indent()}{returnStr} {node.Name.Name}({inlineParams})");
            sb.AppendLine($"{Indent()}{{");
            sb.AppendLine($"{Indent()}\t// TODO: implement");
            sb.AppendLine($"{Indent()}}}");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public override string VisitCppParameter(CppParameterNode node)
    {
        return RenderParameterInline(node);
    }

    public override string VisitCppMethodImplementation(CppMethodImplementationNode node)
    {
        if (node.Metadata.ContainsKey("AsyncFactory"))
            return RenderAsyncFactoryImpl(node);
        if (node.Metadata.ContainsKey("AsyncComplete"))
            return RenderAsyncCompleteImpl(node);
        if (node.Metadata.ContainsKey("AsyncActivate"))
            return RenderAsyncActivateImpl(node);
        if (node.Metadata.ContainsKey("PathBuilder"))
            return RenderPathBuilderImpl(node);

        // Generic fallback
        var sb = new StringBuilder();
        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) + " " : "";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        var constStr = node.IsConst ? " const" : "";

        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}({paramsStr}){constStr}");
        sb.AppendLine("{");
        sb.AppendLine("\t// TODO: implement");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ===================================================================
    // Specialized body renderers (metadata-driven)
    // ===================================================================

    /// <summary>
    /// Рендерит ParseXxxResult функцию. Точно повторяет паттерн из примера.
    /// </summary>
    private string RenderParseFunction(CppFreeFunctionNode node)
    {
        var sb = new StringBuilder();
        var resultType = (string)node.Metadata["ResultTypeName"];
        var hasSuccessBody = (bool)node.Metadata["HasSuccessBody"];
        var successBodyType = (string)node.Metadata["SuccessBodyTypeName"];

        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));
        sb.AppendLine($"{Indent()}{resultType} {node.Name.Name}({paramsStr})");
        sb.AppendLine($"{Indent()}{{");
        _indentLevel++;

        sb.AppendLine($"{Indent()}{resultType} Out;");
        sb.AppendLine($"{Indent()}Out.StatusCode = R.StatusCode;");

        if (hasSuccessBody)
        {
            sb.AppendLine($"{Indent()}if (R.IsSuccess())");
            sb.AppendLine($"{Indent()}{{");
            _indentLevel++;
            sb.AppendLine($"{Indent()}const TOptional<{successBodyType}> Dto = R.GetBodyAs<{successBodyType}>();");
            sb.AppendLine($"{Indent()}if (Dto.IsSet()) {{ Out.SuccessBody = Dto.GetValue(); }}");
            _indentLevel--;
            sb.AppendLine($"{Indent()}}}");
            sb.AppendLine($"{Indent()}else if (R.StatusCode >= 400 && R.StatusCode < 600)");
        }
        else
        {
            // Нет success body (например 204 No Content) — проверяем только ошибку
            sb.AppendLine($"{Indent()}if (!R.IsSuccess() && R.StatusCode >= 400 && R.StatusCode < 600)");
        }

        sb.AppendLine($"{Indent()}{{");
        _indentLevel++;
        sb.AppendLine($"{Indent()}const TOptional<FRestApiResponse> Err = R.GetBodyAs<FRestApiResponse>();");
        sb.AppendLine($"{Indent()}if (Err.IsSet()) {{ Out.ErrorBody = Err.GetValue(); }}");
        sb.AppendLine($"{Indent()}else {{ Out.ErrorBody.Exception.message = R.GetBodyAsString(); }}");
        _indentLevel--;
        sb.AppendLine($"{Indent()}}}");

        sb.AppendLine($"{Indent()}return Out;");
        _indentLevel--;
        sb.AppendLine($"{Indent()}}}");
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит нативную TFuture API-функцию. Повторяет паттерн из примера .cpp.
    /// </summary>
    private string RenderNativeApiFunction(CppFreeFunctionNode node)
    {
        var sb = new StringBuilder();
        var httpMethod = (string)node.Metadata["HttpMethod"];
        var pathsClassName = (string)node.Metadata["PathsClassName"];
        var pathMethodName = (string)node.Metadata["PathMethodName"];
        var parseFuncName = (string)node.Metadata["ParseFunctionName"];
        var requestName = (string)node.Metadata["RequestName"];
        var pathParams = (List<string>)node.Metadata["PathParams"];
        var hasBody = (bool)node.Metadata["HasBody"];
        var hasMultipleGroups = (bool)node.Metadata["HasMultipleParamGroups"];

        var returnStr = RenderType(node.ReturnType);
        var paramsStr = string.Join(",\n\t", node.Parameters.Select(RenderParameterInline));

        sb.AppendLine($"{returnStr} {node.Name.Name}(");
        sb.AppendLine($"\t{paramsStr})");
        sb.AppendLine("{");

        // Build path via PathsLibrary
        var prefix = hasMultipleGroups ? "Params.Route." : "Params.";
        var pathArgs = string.Join(", ", pathParams.Select(p => $"{prefix}{p}"));
        sb.AppendLine($"\tconst FString Path = {pathsClassName}::{pathMethodName}({pathArgs});");

        // HTTP verb
        var httpVerb = httpMethod switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            "HEAD" => "Head",
            "OPTIONS" => "Options",
            _ => "Get"
        };

        // Build request chain
        sb.AppendLine($"\tFValIORequest Request = Client->NewRequest(EValIOHttpVerb::{httpVerb}, Path)");

        // Query params
        var queryParams = node.Metadata.ContainsKey("QueryParams")
            ? (System.Collections.IList)node.Metadata["QueryParams"] : null;
        if (queryParams != null)
        {
            var queryPrefix = hasMultipleGroups ? "Params.Query." : "Params.";
            foreach (dynamic qp in queryParams)
            {
                string name = qp.Name;
                string propName = qp.PropertyName;
                sb.AppendLine($"\t\t.AddQueryParam(TEXT(\"{name}\"), FString::FromInt({queryPrefix}{propName}))");
            }
        }

        sb.AppendLine($"\t\t.SetRequestName(TEXT(\"{requestName}\"))");
        sb.AppendLine("\t\t.Build();");

        sb.AppendLine($"\treturn Client->Send(MoveTemp(Request)).Next({parseFuncName});");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Static factory: return NewAsyncObject&lt;T&gt;(...)
    /// </summary>
    private string RenderAsyncFactoryImpl(CppMethodImplementationNode node)
    {
        var sb = new StringBuilder();
        var className = (string)node.Metadata["ClassName"];

        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) + " " : "";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));

        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}(");
        sb.AppendLine($"\t{paramsStr})");
        sb.AppendLine("{");
        sb.AppendLine($"\treturn UValIOAsyncActionBase::NewAsyncObject<{className}>(WorldContextObject, ClientNameArg, Params);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Complete: store result, broadcast delegate, destroy
    /// </summary>
    private string RenderAsyncCompleteImpl(CppMethodImplementationNode node)
    {
        var sb = new StringBuilder();
        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) + " " : "";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));

        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}({paramsStr})");
        sb.AppendLine("{");
        sb.AppendLine("\tLastResult = Result;");
        sb.AppendLine("\tOnCompleted.Broadcast(Result);");
        sb.AppendLine("\tSetReadyToDestroy();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Activate: RunWithClientAndComplete with lambda calling native function
    /// </summary>
    private string RenderAsyncActivateImpl(CppMethodImplementationNode node)
    {
        var sb = new StringBuilder();
        var className = (string)node.Metadata["ClassName"];
        var resultType = (string)node.Metadata["ResultTypeName"];
        var nativeFunc = (string)node.Metadata["NativeFunctionName"];

        sb.AppendLine($"void {node.ClassName.Name}::Activate()");
        sb.AppendLine("{");
        sb.AppendLine($"\tRunWithClientAndComplete<{className}, {resultType}>(");
        sb.AppendLine($"\t\t[this](FValIOClientRef Client) {{ return {nativeFunc}(Client, State.Params); }});");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Path builder: FString::Printf(TEXT("/api/..."), ...)
    /// </summary>
    private string RenderPathBuilderImpl(CppMethodImplementationNode node)
    {
        var sb = new StringBuilder();
        var urlTemplate = (string)node.Metadata["UrlTemplate"];
        var pathParams = (List<object>)node.Metadata["PathParams"];

        var returnStr = node.ReturnType != null ? RenderType(node.ReturnType) + " " : "";
        var paramsStr = string.Join(", ", node.Parameters.Select(RenderParameterInline));

        sb.AppendLine($"{returnStr}{node.ClassName.Name}::{node.MethodName.Name}({paramsStr})");
        sb.AppendLine("{");

        var formatStr = urlTemplate;
        var printfArgs = new List<string>();

        foreach (dynamic param in pathParams)
        {
            string originalName = param.OriginalName;
            string propertyName = param.PropertyName;
            string specifier = param.PrintfSpecifier;
            bool needsDeref = param.NeedsDereference;

            formatStr = formatStr.Replace($"{{{originalName}}}", specifier);
            printfArgs.Add(needsDeref ? $"*{propertyName}" : propertyName);
        }

        var argsStr = printfArgs.Count > 0 ? ",\n\t\t" + string.Join(", ", printfArgs) : "";
        sb.AppendLine($"\treturn FString::Printf(TEXT(\"{formatStr}\"){argsStr});");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ===================================================================
    // Base AST visitor stubs (required by CppAstVisitorBase)
    // ===================================================================

    public override string VisitIdentifier(IdentifierNode node) => node.Name;
    public override string VisitComment(CommentNode node) => RenderDoxygenComment(node.Text);
    public override string VisitAttribute(AttributeNode node) => "";
    public override string VisitBlock(BlockNode node) => "{ }";
    public override string VisitImport(ImportNode node) => "";
    public override string VisitExport(ExportNode node) => "";

    // Types
    public override string VisitSimpleType(SimpleTypeNode node) => node.Name.Name;
    public override string VisitGenericType(GenericTypeNode node)
    {
        var args = string.Join(", ", node.TypeArguments.Select(RenderType));
        return $"{node.Name.Name}<{args}>";
    }
    public override string VisitNullableType(NullableTypeNode node) => RenderType(node.InnerType);
    public override string VisitOptionalType(OptionalTypeNode node) => RenderType(node.InnerType);
    public override string VisitArrayType(ArrayTypeNode node) => $"TArray<{RenderType(node.ElementType)}>";
    public override string VisitUnionType(UnionTypeNode node) =>
        node.Types.Count > 0 ? RenderType(node.Types[0]) : "FString";
    public override string VisitIntersectionType(IntersectionTypeNode node) =>
        node.Types.Count > 0 ? RenderType(node.Types[0]) : "FString";
    public override string VisitDictionaryType(DictionaryTypeNode node) =>
        $"TMap<{RenderType(node.KeyType)}, {RenderType(node.ValueType)}>";

    // Expressions
    public override string VisitLiteralExpression(LiteralExpressionNode node) => node.Value?.ToString() ?? "0";
    public override string VisitIdentifierExpression(IdentifierExpressionNode node) => node.Identifier.Name;
    public override string VisitCallExpression(CallExpressionNode node) => "";
    public override string VisitMemberAccessExpression(MemberAccessExpressionNode node) => "";
    public override string VisitBinaryExpression(BinaryExpressionNode node) => "";
    public override string VisitConditionalExpression(ConditionalExpressionNode node) => "";
    public override string VisitAwaitExpression(AwaitExpressionNode node) => "";
    public override string VisitCastExpression(CastExpressionNode node) => "";

    // Statements
    public override string VisitReturnStatement(ReturnStatementNode node) => "";
    public override string VisitIfStatement(IfStatementNode node) => "";
    public override string VisitForEachStatement(ForEachStatementNode node) => "";
    public override string VisitExpressionStatement(ExpressionStatementNode node) => "";
    public override string VisitVariableDeclarationStatement(VariableDeclarationStatementNode node) => "";
    public override string VisitThrowStatement(ThrowStatementNode node) => "";
    public override string VisitUsingStatement(UsingStatementNode node) => "";
    public override string VisitTryCatchStatement(TryCatchStatementNode node) => "";
    public override string VisitCatchClause(CatchClauseNode node) => "";

    // Declarations
    public override string VisitTypeDeclaration(TypeDeclarationNode node) => "";
    public override string VisitPropertyDeclaration(PropertyDeclarationNode node) => "";
    public override string VisitMethodDeclaration(MethodDeclarationNode node) => "";
    public override string VisitParameterDeclaration(ParameterDeclarationNode node) => "";
    public override string VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => "";
    public override string VisitEnumDeclaration(EnumDeclarationNode node) => "";
    public override string VisitInterfaceDeclaration(InterfaceDeclarationNode node) => "";

    // HTTP nodes (required by interface but not used directly in C++ rendering)
    public override string VisitHttpMethod(HttpMethodNode node) => "";
    public override string VisitRoute(RouteNode node) => "";
    public override string VisitPathParameter(PathParameterNode node) => "";
    public override string VisitQueryParameter(QueryParameterNode node) => "";
    public override string VisitHeaderParameter(HeaderParameterNode node) => "";
    public override string VisitRequestBody(RequestBodyNode node) => "";
    public override string VisitResponseType(ResponseTypeNode node) => "";

    // ===================================================================
    // Helper methods
    // ===================================================================

    private string RenderType(TypeNode type) => Visit(type);

    private string RenderParameterInline(CppParameterNode param)
    {
        var constStr = param.IsConst ? "const " : "";
        var typeStr = RenderType(param.Type);
        var refStr = param.IsReference ? "&" : param.IsPointer ? "*" : "";
        var defaultStr = param.DefaultValue != null ? $" = {RenderDefaultValue(param.DefaultValue)}" : "";
        return $"{constStr}{typeStr}{refStr} {param.Name.Name}{defaultStr}";
    }

    private static string RenderDefaultValue(ExpressionNode expr)
    {
        if (expr is LiteralExpressionNode lit)
        {
            return lit.LiteralKind switch
            {
                LiteralKind.String => lit.Value?.ToString() ?? "",
                LiteralKind.Number => lit.Value?.ToString() ?? "0",
                LiteralKind.Boolean => lit.Value?.ToString()?.ToLowerInvariant() ?? "false",
                LiteralKind.Null => "{}",
                _ => lit.Value?.ToString() ?? ""
            };
        }
        return expr.ToString() ?? "";
    }

    private string RenderDoxygenComment(string text)
    {
        if (text.Contains('\n'))
        {
            var lines = text.Split('\n');
            var sb = new StringBuilder();
            sb.AppendLine($"{Indent()}/**");
            foreach (var line in lines)
                sb.AppendLine($"{Indent()} * {line.TrimEnd()}");
            sb.Append($"{Indent()} */");
            return sb.ToString();
        }
        return $"/** {text} */";
    }

    private static string RenderBlockComment(string text)
    {
        return $"// {text}";
    }

    private static string RenderUStructMacro(UStructSpecifiers spec)
    {
        var parts = new List<string>();
        if (spec.BlueprintType) parts.Add("BlueprintType");
        foreach (var meta in spec.Meta)
            parts.Add($"meta = ({meta.Key} = \"{meta.Value}\")");
        return $"USTRUCT({string.Join(", ", parts)})";
    }

    private static string RenderUPropertyMacro(UPropertySpecifiers spec)
    {
        var parts = new List<string>();
        if (spec.EditAnywhere) parts.Add("EditAnywhere");
        if (spec.BlueprintReadWrite) parts.Add("BlueprintReadWrite");
        if (spec.BlueprintReadOnly) parts.Add("BlueprintReadOnly");
        if (spec.BlueprintAssignable) parts.Add("BlueprintAssignable");
        if (!string.IsNullOrEmpty(spec.Category)) parts.Add($"Category = \"{spec.Category}\"");

        if (spec.Meta.Count > 0)
        {
            var metaParts = spec.Meta.Select(kv => $"{kv.Key} = \"{kv.Value}\"");
            parts.Add($"meta = ({string.Join(", ", metaParts)})");
        }
        return $"UPROPERTY({string.Join(", ", parts)})";
    }

    private static string RenderUClassMacro(UClassSpecifiers spec)
    {
        var parts = new List<string>();
        if (spec.Meta.Count > 0)
        {
            var metaParts = spec.Meta.Select(kv => $"{kv.Key} = \"{kv.Value}\"");
            parts.Add($"meta = ({string.Join(", ", metaParts)})");
        }
        return $"UCLASS({string.Join(", ", parts)})";
    }

    private static string RenderUFunctionMacro(UFunctionSpecifiers spec)
    {
        var parts = new List<string>();
        if (spec.BlueprintCallable) parts.Add("BlueprintCallable");
        if (spec.BlueprintPure) parts.Add("BlueprintPure");
        if (!string.IsNullOrEmpty(spec.Category)) parts.Add($"Category = \"{spec.Category}\"");

        if (spec.Meta.Count > 0)
        {
            var metaParts = spec.Meta.Select(kv => $"{kv.Key} = \"{kv.Value}\"");
            parts.Add($"meta = ({string.Join(", ", metaParts)})");
        }
        return $"UFUNCTION({string.Join(", ", parts)})";
    }

    private static string RenderSeparator(string title)
    {
        return
            "// ---------------------------------------------------------------------------\n" +
            $"// {title}\n" +
            "// ---------------------------------------------------------------------------";
    }

    private string Indent() => string.Concat(Enumerable.Repeat(IndentChar, _indentLevel));
}
