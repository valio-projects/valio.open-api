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
    BinaryExpression,
    ConditionalExpression,
    AwaitExpression,
    
    // Statements
    ReturnStatement,
    IfStatement,
    ForEachStatement,
    ExpressionStatement,
    VariableDeclarationStatement,
    ThrowStatement,
    
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
}
