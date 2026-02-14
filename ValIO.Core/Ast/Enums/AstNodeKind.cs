namespace ValIO.Core.Ast.Enums;

/// <summary>
/// Типы AST узлов
/// </summary>
public enum AstNodeKind
{
    // Базовые узлы
    Identifier,
    Comment,
    Attribute,
    Block,
    Import,
    Export,
    
    // Типы
    SimpleType,
    GenericType,
    NullableType,
    OptionalType,
    ArrayType,
    UnionType,
    IntersectionType,
    DictionaryType,
    
    // Выражения
    LiteralExpression,
    IdentifierExpression,
    CallExpression,
    MemberAccessExpression,
    UnaryExpression,
    BinaryExpression,
    ConditionalExpression,
    AwaitExpression,
    CastExpression,
    
    // Statements
    ReturnStatement,
    IfStatement,
    ForEachStatement,
    ExpressionStatement,
    VariableDeclarationStatement,
    ThrowStatement,
    UsingStatement,
    TryCatchStatement,
    CatchClause,
    
    // Объявления
    Declaration,
    TypeDeclaration,
    PropertyDeclaration,
    MethodDeclaration,
    ParameterDeclaration,
    EnumMemberDeclaration,
    
    // C# специфичные
    CompilationUnit,
    NamespaceDeclaration,
    UsingDirective,
    ClassDeclaration,
    RecordDeclaration,
    StructDeclaration,
    InterfaceDeclaration,
    EnumDeclaration,
    FieldDeclaration,
    ConstructorDeclaration,
    
    // TypeScript специфичные
    SourceFile,
    TypeAliasDeclaration,
    FunctionDeclaration,
    VariableDeclaration,
    ObjectLiteralExpression,
    PropertyAssignment,
    TemplateLiteralExpression,
    GenericParameter,
    PropertySignature,
    
    // HTTP специфичные узлы
    HttpMethod,
    Route,
    PathParameter,
    QueryParameter,
    HeaderParameter,
    RequestBody,
    ResponseType,
    
    // Zod специфичные узлы
    ZodSchema,
    ZodObject,
    ZodArray,
    ZodUnion,
    ZodIntersection,
    ZodEnum,
    ZodEnumValue,
    ZodProperty,
    ZodEffects,
    
    // Axios специфичные узлы
    AxiosCallExpression,
    AxiosConfig,
    
    // TanStack Query специфичные узлы
    UseQuery,
    UseMutation,
    QueryOptions,
    MutationOptions,
    QueryKey,
    
    // C++ специфичные узлы
    CppHeaderFile,
    CppSourceFile,
    CppIncludeDirective,
    CppForwardDeclaration,
    CppUsingAlias,
    CppFreeFunction,
    CppParameter,
    CppMethodImplementation,
    CppPathBuilderImplementation,
    CppParseFunction,
    CppNativeApiFunction,
    CppAsyncFactoryImplementation,
    CppAsyncCompleteImplementation,
    CppAsyncActivateImplementation,
    CppLambdaExpression,
    
    // Unreal Engine специфичные узлы
    UStruct,
    UPropertyField,
    UClass,
    UFunction,
    UDelegate,
    UEnum,
}
