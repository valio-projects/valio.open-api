using System.Collections.Immutable;
using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.CSharp.Ast.Nodes;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>
/// Построитель AST для C# (Builder Pattern)
/// </summary>
public class CSharpAstBuilder : ICSharpAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly HttpAstBuilder _httpBuilder;
    private readonly CSharpGenerationOptions _options;
    private Dictionary<string, IrModel>? _modelsByName;
    
    public CSharpAstBuilder(INamingConverter namingConverter, CSharpGenerationOptions? options = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _httpBuilder = new HttpAstBuilder();
        _options = options ?? new CSharpGenerationOptions();
    }
    
    public NamespaceDeclarationNode BuildNamespace(string name, IReadOnlyList<DeclarationNode> declarations)
    {
        var nameIdentifier = new IdentifierNode(name);
        return new NamespaceDeclarationNode(
            name: nameIdentifier,
            declarations: declarations.ToImmutableList());
    }
    
    public TypeNode BuildType(TypeInfo typeInfo)
    {
        if (typeInfo.IsCollection)
        {
            var elementType = BuildTypeFromBaseType(typeInfo.GenericArguments.FirstOrDefault() ?? TypeInfo.Simple(typeInfo.BaseType));
            return new ArrayTypeNode(elementType, isNullable: typeInfo.IsNullable);
        }
        
        if (typeInfo.IsDictionary)
        {
            var keyType = BuildTypeFromBaseType(typeInfo.GenericArguments.ElementAtOrDefault(0) ?? TypeInfo.Simple("string"));
            var valueType = BuildTypeFromBaseType(typeInfo.GenericArguments.ElementAtOrDefault(1) ?? TypeInfo.Simple("object"));
            return new DictionaryTypeNode(keyType, valueType, isNullable: typeInfo.IsNullable);
        }
        
        if (!string.IsNullOrEmpty(typeInfo.BaseType) && typeInfo.GenericArguments.Count > 0)
        {
            var genericArgs = typeInfo.GenericArguments.Select(BuildType).ToImmutableList();
            var genericType = new GenericTypeNode(
                name: new IdentifierNode(typeInfo.BaseType),
                typeArguments: genericArgs,
                isNullable: typeInfo.IsNullable);
            return genericType;
        }
        
        return BuildTypeFromBaseType(typeInfo);
    }
    
    public LiteralExpressionNode BuildLiteral(object value, TypeInfo typeInfo)
    {
        var literalKind = value switch
        {
            string => LiteralKind.String,
            int or long or short or byte => LiteralKind.Number,
            float or double or decimal => LiteralKind.Number,
            bool => LiteralKind.Boolean,
            null => LiteralKind.Null,
            _ => LiteralKind.String
        };
        
        return new LiteralExpressionNode(value ?? string.Empty, literalKind);
    }
    
    // Приватные вспомогательные методы
    
    private IReadOnlyList<UsingDirectiveNode> BuildUsingDirectives(IReadOnlyList<string> dependencies)
    {
        return dependencies
            .Select(dep => new UsingDirectiveNode(new IdentifierNode(dep)))
            .ToImmutableList();
    }
    
    private TypeNode BuildTypeFromBaseType(TypeInfo typeInfo)
    {
        var simpleType = new SimpleTypeNode(
            name: new IdentifierNode(typeInfo.BaseType),
            isPrimitive: typeInfo.IsPrimitive,
            isNullable: typeInfo.IsNullable);
        return simpleType;
    }
    
    private SimpleTypeNode BuildSimpleType(string typeName)
    {
        return new SimpleTypeNode(new IdentifierNode(typeName), isPrimitive: false);
    }
    
    // IR → AST методы
    
    /// <summary>
    /// Строит TypeNode из IrType
    /// </summary>
    public TypeNode BuildType(IrType irType)
    {
        return irType.Kind switch
        {
            IrTypeKind.Primitive => BuildPrimitiveType((IrPrimitiveType)irType),
            IrTypeKind.ModelReference => BuildModelReference((IrModelReference)irType),
            IrTypeKind.Array => BuildArrayType((IrArrayType)irType),
            IrTypeKind.Dictionary => BuildDictionaryType((IrDictionaryType)irType),
            IrTypeKind.Union => BuildUnionType((IrUnionType)irType),
            IrTypeKind.Intersection => BuildIntersectionType((IrIntersectionType)irType),
            IrTypeKind.Nullable => BuildNullableType((IrNullableType)irType),
            IrTypeKind.Optional => BuildType(((IrOptionalType)irType).InnerType), // Optional не используется в C#
            _ => throw new NotSupportedException($"IrTypeKind {irType.Kind} is not supported")
        };
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrModel
    /// </summary>
    public CompilationUnitNode BuildCompilationUnit(IrModel model, string? @namespace = null)
    {
        var usings = BuildUsingDirectives(ExtractDependencies(model));
        var classNode = BuildClass(model);
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { classNode }.ToList<DeclarationNode>());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит ClassDeclarationNode из IrModel
    /// </summary>
    public ClassDeclarationNode BuildClass(IrModel model)
    {
        var name = new IdentifierNode(model.Name);
        var comment = !string.IsNullOrEmpty(model.Description)
            ? new CommentNode(model.Description, CommentKind.Xml)
            : null;
        
        var properties = model.Properties.Select(p => BuildProperty(p, model)).ToList<DeclarationNode>();
        
        // Обрабатываем Composition для allOf (наследование)
        var baseTypes = new List<TypeNode>();
        bool hasBaseClass = false;
        if (model.Composition != null && model.Composition.Kind == IrCompositionKind.AllOf)
        {
            // Для allOf в C# первый тип с IrModelReference становится базовым классом
            foreach (var type in model.Composition.Types)
            {
                if (type is IrModelReference modelRef)
                {
                    // Это базовый класс - добавляем его в baseTypes
                    var baseType = BuildType(modelRef);
                    baseTypes.Add(baseType);
                    hasBaseClass = true;
                    break; // В C# множественное наследование не поддерживается, берем только первый
                }
            }
        }
        
        // Определяем, должен ли класс быть sealed
        // Класс должен быть sealed, если:
        // 1. Включена опция UseSealed И
        // 2. Класс имеет базовый класс (является дочерним классом через allOf)
        // Базовые классы (без базового класса) не должны быть sealed, если есть наследники
        bool shouldBeSealed = _options.UseSealed && hasBaseClass;
        
        // Добавляем вложенные классы с константами валидации, если включено
        if (_options.GenerateValidationConstants)
        {
            var constantsClasses = BuildValidationConstantsClasses(model);
            properties.AddRange(constantsClasses.Cast<DeclarationNode>());
        }
        
        // Добавляем интерфейс IEquatable<TModel> и методы Equals/GetHashCode, если включено
        if (_options.GenerateEqualityMembers)
        {
            // Добавляем интерфейс IEquatable<TModel>
            var modelType = new SimpleTypeNode(new IdentifierNode(model.Name), isPrimitive: false);
            var equatableInterface = new GenericTypeNode(
                name: new IdentifierNode("IEquatable"),
                typeArguments: new[] { modelType }.ToImmutableList(),
                isNullable: false);
            baseTypes.Add(equatableInterface);
            
            var equalityMethods = BuildEqualityMembers(model, baseTypes);
            properties.AddRange(equalityMethods.Cast<DeclarationNode>());
        }
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            isSealed: shouldBeSealed,
            isStatic: false,
            isPartial: false,
            baseTypes: baseTypes.ToImmutableList(),
            members: properties.ToImmutableList(),
            documentation: comment);
    }
    
    /// <summary>
    /// Строит PropertyDeclarationNode из IrProperty
    /// </summary>
    public PropertyDeclarationNode BuildProperty(IrProperty property, IrModel? model = null)
    {
        var name = new IdentifierNode(property.Name);
        var type = BuildType(property.Type);
        var comment = !string.IsNullOrEmpty(property.Description)
            ? new CommentNode(property.Description, CommentKind.Xml)
            : null;
        
        var defaultValue = property.Constraints?.DefaultValue != null
            ? BuildLiteral(property.Constraints.DefaultValue, InferTypeInfo(property.Type))
            : null;
        
        // Собираем атрибуты
        var attributes = new List<AttributeNode>();
        
        // Добавляем JsonPropertyName атрибут с оригинальным именем, если имя изменилось
        if (property.Name != property.OriginalName)
        {
            var jsonPropertyNameAttr = new AttributeNode(
                name: new IdentifierNode("JsonPropertyName"),
                arguments: new[] { new LiteralExpressionNode(property.OriginalName, LiteralKind.String) }.ToImmutableList());
            attributes.Add(jsonPropertyNameAttr);
        }
        
        // Добавляем атрибуты валидации, если включено
        if (_options.GenerateValidationAttributes && model != null)
        {
            AddValidationAttributes(property, model, attributes);
        }
        
        // Определяем тип аксессора на основе опций
        var accessor = _options.PropertyAccessor == PropertyAccessorType.Init
            ? PropertyAccessorKind.GetInit
            : PropertyAccessorKind.GetSet;
        
        // Определяем, нужно ли добавлять required keyword
        var isRequiredKeyword = _options.UseRequired && property.IsRequired;
        
        return new PropertyDeclarationNode(
            name: name,
            type: type,
            isRequired: isRequiredKeyword,
            accessor: accessor,
            defaultValue: defaultValue,
            documentation: comment,
            attributes: attributes.Count > 0 ? attributes.ToImmutableList() : null);
    }
    
    /// <summary>
    /// Добавляет атрибуты валидации для свойства на основе ограничений
    /// </summary>
    private void AddValidationAttributes(IrProperty property, IrModel model, List<AttributeNode> attributes)
    {
        if (property.Constraints == null)
            return;

        var constraints = property.Constraints;
        
        // Определяем имя класса констант (если генерируем константы)
        string? constantsClassName = null;
        if (_options.GenerateValidationConstants)
        {
            var propertyNamePascalCase = _namingConverter.ToPascalCase(property.OriginalName);
            constantsClassName = $"{propertyNamePascalCase}Constants";
        }

        // Required атрибут для обязательных свойств
        if (property.IsRequired)
        {
            attributes.Add(new AttributeNode(new IdentifierNode("Required")));
        }

        // MinLength / MaxLength для строк
        if (constraints.MinLength.HasValue)
        {
            ExpressionNode minLengthValue;
            if (_options.GenerateValidationConstants && constantsClassName != null)
            {
                // Используем константу из класса констант
                var constantsClassAccess = new IdentifierExpressionNode(new IdentifierNode(constantsClassName));
                var minLengthAccess = new MemberAccessExpressionNode(
                    @object: constantsClassAccess,
                    member: new IdentifierNode("MinLength"));
                minLengthValue = minLengthAccess;
            }
            else
            {
                // Используем inline значение
                minLengthValue = new LiteralExpressionNode(constraints.MinLength.Value.ToString(), LiteralKind.Number);
            }
            
            var minLengthAttr = new AttributeNode(
                name: new IdentifierNode("MinLength"),
                arguments: new[] { minLengthValue }.ToImmutableList());
            attributes.Add(minLengthAttr);
        }

        if (constraints.MaxLength.HasValue)
        {
            ExpressionNode maxLengthValue;
            if (_options.GenerateValidationConstants && constantsClassName != null)
            {
                // Используем константу из класса констант
                var constantsClassAccess = new IdentifierExpressionNode(new IdentifierNode(constantsClassName));
                var maxLengthAccess = new MemberAccessExpressionNode(
                    @object: constantsClassAccess,
                    member: new IdentifierNode("MaxLength"));
                maxLengthValue = maxLengthAccess;
            }
            else
            {
                // Используем inline значение
                maxLengthValue = new LiteralExpressionNode(constraints.MaxLength.Value.ToString(), LiteralKind.Number);
            }
            
            var maxLengthAttr = new AttributeNode(
                name: new IdentifierNode("MaxLength"),
                arguments: new[] { maxLengthValue }.ToImmutableList());
            attributes.Add(maxLengthAttr);
        }

        // Range для чисел (Min/Max)
        if (constraints.Min.HasValue || constraints.Max.HasValue)
        {
            ExpressionNode minValue;
            if (_options.GenerateValidationConstants && constantsClassName != null && constraints.Min.HasValue)
            {
                // Используем константу из класса констант
                var constantsClassAccess = new IdentifierExpressionNode(new IdentifierNode(constantsClassName));
                var minAccess = new MemberAccessExpressionNode(
                    @object: constantsClassAccess,
                    member: new IdentifierNode("Min"));
                minValue = minAccess;
            }
            else
            {
                // Используем inline значение
                minValue = constraints.Min.HasValue
                    ? new LiteralExpressionNode(constraints.Min.Value.ToString(), LiteralKind.Number)
                    : new LiteralExpressionNode("0", LiteralKind.Number);
            }

            ExpressionNode maxValue;
            if (_options.GenerateValidationConstants && constantsClassName != null && constraints.Max.HasValue)
            {
                // Используем константу из класса констант
                var constantsClassAccess = new IdentifierExpressionNode(new IdentifierNode(constantsClassName));
                var maxAccess = new MemberAccessExpressionNode(
                    @object: constantsClassAccess,
                    member: new IdentifierNode("Max"));
                maxValue = maxAccess;
            }
            else
            {
                // Используем inline значение
                maxValue = constraints.Max.HasValue
                    ? new LiteralExpressionNode(constraints.Max.Value.ToString(), LiteralKind.Number)
                    : new LiteralExpressionNode("double.MaxValue", LiteralKind.String);
            }

            var rangeAttr = new AttributeNode(
                name: new IdentifierNode("Range"),
                arguments: new[] { minValue, maxValue }.ToImmutableList());
            attributes.Add(rangeAttr);
        }

        // RegularExpression для паттернов
        if (!string.IsNullOrEmpty(constraints.Pattern))
        {
            ExpressionNode patternValue;
            if (_options.GenerateValidationConstants && constantsClassName != null)
            {
                // Используем константу из класса констант
                var constantsClassAccess = new IdentifierExpressionNode(new IdentifierNode(constantsClassName));
                var patternAccess = new MemberAccessExpressionNode(
                    @object: constantsClassAccess,
                    member: new IdentifierNode("Pattern"));
                patternValue = patternAccess;
            }
            else
            {
                // Используем inline значение
                patternValue = new LiteralExpressionNode(constraints.Pattern, LiteralKind.String);
            }
            
            var regexAttr = new AttributeNode(
                name: new IdentifierNode("RegularExpression"),
                arguments: new[] { patternValue }.ToImmutableList());
            attributes.Add(regexAttr);
        }
    }
    
    /// <summary>
    /// Получает все свойства модели, включая свойства из родительских классов через allOf
    /// </summary>
    private IReadOnlyList<IrProperty> GetAllPropertiesIncludingInherited(IrModel model, IReadOnlyList<TypeNode> baseTypes)
    {
        var allProperties = new List<IrProperty>();
        
        // Добавляем свойства из родительских классов
        foreach (var baseType in baseTypes)
        {
            if (baseType is SimpleTypeNode simpleBaseType && _modelsByName != null)
            {
                var baseModelName = simpleBaseType.Name.Name;
                if (_modelsByName.TryGetValue(baseModelName, out var baseModel))
                {
                    // Рекурсивно получаем свойства базового класса (включая его базовые классы)
                    var baseBaseTypes = new List<TypeNode>();
                    if (baseModel.Composition != null && baseModel.Composition.Kind == IrCompositionKind.AllOf)
                    {
                        foreach (var type in baseModel.Composition.Types)
                        {
                            if (type is IrModelReference modelRef)
                            {
                                baseBaseTypes.Add(BuildType(modelRef));
                                break;
                            }
                        }
                    }
                    allProperties.AddRange(GetAllPropertiesIncludingInherited(baseModel, baseBaseTypes));
                }
            }
        }
        
        // Добавляем собственные свойства модели
        allProperties.AddRange(model.Properties);
        
        return allProperties;
    }
    
    /// <summary>
    /// Строит методы Equals и GetHashCode для класса модели
    /// </summary>
    private IReadOnlyList<MethodDeclarationNode> BuildEqualityMembers(IrModel model, IReadOnlyList<TypeNode> baseTypes)
    {
        var methods = new List<MethodDeclarationNode>();
        
        // Метод Equals(TModel? other) - реализация IEquatable<T>
        var modelType = new SimpleTypeNode(new IdentifierNode(model.Name), isPrimitive: false);
        var nullableModelType = new NullableTypeNode(modelType);
        
        var equalsTParam = new ParameterDeclarationNode(
            name: new IdentifierNode("other"),
            type: nullableModelType,
            isRequired: false);
        
        var allProperties = GetAllPropertiesIncludingInherited(model, baseTypes);
        var equalsTBody = BuildEqualsTMethodBody(model, allProperties);
        var equalsTMethod = new MethodDeclarationNode(
            name: new IdentifierNode("Equals"),
            returnType: new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
            parameters: new[] { equalsTParam }.ToImmutableList(),
            body: equalsTBody,
            accessModifier: AccessModifier.Public,
            isAsync: false,
            documentation: new CommentNode("Определяет, равен ли указанный объект текущему объекту.", CommentKind.Xml));
        methods.Add(equalsTMethod);
        
        // Метод Equals(object? obj) - переопределение из System.Object
        var equalsObjParam = new ParameterDeclarationNode(
            name: new IdentifierNode("obj"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("object"), isPrimitive: true)),
            isRequired: false);
        
        var equalsObjBody = BuildEqualsObjectMethodBody(model);
        var equalsObjMethod = new MethodDeclarationNode(
            name: new IdentifierNode("Equals"),
            returnType: new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
            parameters: new[] { equalsObjParam }.ToImmutableList(),
            body: equalsObjBody,
            accessModifier: AccessModifier.Public,
            isAsync: false,
            documentation: new CommentNode("Определяет, равен ли указанный объект текущему объекту.", CommentKind.Xml),
            isOverride: true);
        methods.Add(equalsObjMethod);
        
        // Метод GetHashCode()
        var getHashCodeBody = BuildGetHashCodeMethodBody(model, allProperties);
        var getHashCodeMethod = new MethodDeclarationNode(
            name: new IdentifierNode("GetHashCode"),
            returnType: new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
            parameters: ImmutableList<ParameterDeclarationNode>.Empty,
            body: getHashCodeBody,
            accessModifier: AccessModifier.Public,
            isAsync: false,
            documentation: new CommentNode("Служит хэш-функцией по умолчанию.", CommentKind.Xml),
            isOverride: true);
        methods.Add(getHashCodeMethod);
        
        return methods;
    }
    
    /// <summary>
    /// Строит тело метода Equals(object? obj) - вызывает Equals(TModel? other)
    /// </summary>
    private BlockNode BuildEqualsObjectMethodBody(IrModel model)
    {
        var statements = new List<StatementNode>();
        
        // return Equals(obj as TModel);
        var objParam = new IdentifierExpressionNode(new IdentifierNode("obj"));
        var modelType = new SimpleTypeNode(new IdentifierNode(model.Name), isPrimitive: false);
        
        // Используем CastExpressionNode для приведения типа, но рендерим как "as"
        // Для этого используем metadata или создаем специальный узел
        // Пока используем простой cast, но в рендерере нужно будет добавить поддержку "as"
        var castExpression = new CastExpressionNode(
            targetType: modelType,
            expression: objParam);
        
        // Используем "as" оператор вместо обычного cast
        var asExpression = new CastExpressionNode(
            targetType: modelType,
            expression: objParam,
            useAsOperator: true);
        
        var equalsCall = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("Equals")),
            arguments: new[] { asExpression }.ToImmutableList());
        
        statements.Add(new ReturnStatementNode(equalsCall));
        
        return new BlockNode(statements.ToImmutableList());
    }
    
    /// <summary>
    /// Строит тело метода Equals(TModel? other) - основная реализация
    /// </summary>
    private BlockNode BuildEqualsTMethodBody(IrModel model, IReadOnlyList<IrProperty> allProperties)
    {
        var statements = new List<StatementNode>();
        
        // if (other == null) return false;
        var otherParam = new IdentifierExpressionNode(new IdentifierNode("other"));
        var nullCheck = new BinaryExpressionNode(
            left: otherParam,
            @operator: BinaryOperator.Equal,
            right: new LiteralExpressionNode("null", LiteralKind.Null));
        var ifNullReturn = new IfStatementNode(
            condition: nullCheck,
            thenBlock: new BlockNode(new[] { new ReturnStatementNode(new LiteralExpressionNode("false", LiteralKind.Boolean)) }.ToImmutableList()),
            elseBlock: null);
        statements.Add(ifNullReturn);
        
        // if (ReferenceEquals(this, other)) return true;
        var thisExpr = new IdentifierExpressionNode(new IdentifierNode("this"));
        var objectType = new IdentifierExpressionNode(new IdentifierNode("object"));
        var referenceEqualsMethod = new MemberAccessExpressionNode(
            @object: objectType,
            member: new IdentifierNode("ReferenceEquals"));
        var referenceEqualsCall = new CallExpressionNode(
            target: referenceEqualsMethod,
            arguments: new[] { thisExpr, otherParam }.ToImmutableList());
        var ifReferenceEqualsReturn = new IfStatementNode(
            condition: referenceEqualsCall,
            thenBlock: new BlockNode(new[] { new ReturnStatementNode(new LiteralExpressionNode("true", LiteralKind.Boolean)) }.ToImmutableList()),
            elseBlock: null);
        statements.Add(ifReferenceEqualsReturn);
        
        // Параметр "other" уже имеет тип TModel?, так что не нужно приводить тип
        
        // Сравниваем все свойства (включая наследуемые) через Equals
        var equalityChecks = new List<ExpressionNode>();
        foreach (var property in allProperties)
        {
            var thisProperty = new MemberAccessExpressionNode(
                @object: thisExpr,
                member: new IdentifierNode(property.Name));
            var otherProperty = new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("other")),
                member: new IdentifierNode(property.Name));
            
            // Используем метод Equals для сравнения свойств
            var equalsMethod = new MemberAccessExpressionNode(
                @object: thisProperty,
                member: new IdentifierNode("Equals"));
            ExpressionNode propertyEquals = new CallExpressionNode(
                target: equalsMethod,
                arguments: new[] { otherProperty }.ToImmutableList());
            
            equalityChecks.Add(propertyEquals);
        }
        
        // Объединяем все проверки через &&
        if (equalityChecks.Count > 0)
        {
            ExpressionNode combinedCheck = equalityChecks[0];
            for (int i = 1; i < equalityChecks.Count; i++)
            {
                combinedCheck = new BinaryExpressionNode(
                    left: combinedCheck,
                    @operator: BinaryOperator.LogicalAnd,
                    right: equalityChecks[i]);
            }
            
            statements.Add(new ReturnStatementNode(combinedCheck));
        }
        else
        {
            statements.Add(new ReturnStatementNode(new LiteralExpressionNode("true", LiteralKind.Boolean)));
        }
        
        return new BlockNode(statements.ToImmutableList());
    }
    
    /// <summary>
    /// Строит тело метода GetHashCode
    /// </summary>
    private BlockNode BuildGetHashCodeMethodBody(IrModel model, IReadOnlyList<IrProperty> allProperties)
    {
        var statements = new List<StatementNode>();
        
        if (allProperties.Count == 0)
        {
            // return 0;
            statements.Add(new ReturnStatementNode(new LiteralExpressionNode("0", LiteralKind.Number)));
            return new BlockNode(statements.ToImmutableList());
        }
        
        // Создаем переменную HashCode hashCode = new();
        var hashCodeType = new SimpleTypeNode(new IdentifierNode("HashCode"), isPrimitive: false);
        var hashCodeVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("hashCode"),
            type: hashCodeType,
            initializer: new CallExpressionNode(
                target: new IdentifierExpressionNode(new IdentifierNode("HashCode")),
                arguments: ImmutableList<ExpressionNode>.Empty,
                isNewExpression: true));
        statements.Add(hashCodeVar);
        
        // Добавляем все свойства (включая наследуемые) через hashCode.Add()
        var hashCodeIdentifier = new IdentifierExpressionNode(new IdentifierNode("hashCode"));
        var addMethod = new MemberAccessExpressionNode(
            @object: hashCodeIdentifier,
            member: new IdentifierNode("Add"));
        
        foreach (var property in allProperties)
        {
            var thisProperty = new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("this")),
                member: new IdentifierNode(property.Name));
            
            var addCall = new CallExpressionNode(
                target: addMethod,
                arguments: new[] { thisProperty }.ToImmutableList());
            
            statements.Add(new ExpressionStatementNode(addCall));
        }
        
        // return hashCode.ToHashCode();
        var toHashCodeMethod = new MemberAccessExpressionNode(
            @object: hashCodeIdentifier,
            member: new IdentifierNode("ToHashCode"));
        var toHashCodeCall = new CallExpressionNode(
            target: toHashCodeMethod,
            arguments: ImmutableList<ExpressionNode>.Empty);
        
        statements.Add(new ReturnStatementNode(toHashCodeCall));
        
        return new BlockNode(statements.ToImmutableList());
    }
    
    /// <summary>
    /// Строит классы с константами валидации для свойств модели
    /// </summary>
    private IReadOnlyList<ClassDeclarationNode> BuildValidationConstantsClasses(IrModel model)
    {
        var constantsClasses = new List<ClassDeclarationNode>();
        
        foreach (var property in model.Properties)
        {
            if (property.Constraints == null)
                continue;
            
            var constraints = property.Constraints;
            var hasConstants = constraints.MinLength.HasValue ||
                              constraints.MaxLength.HasValue ||
                              constraints.Min.HasValue ||
                              constraints.Max.HasValue ||
                              !string.IsNullOrEmpty(constraints.Pattern) ||
                              constraints.DefaultValue != null;
            
            if (!hasConstants)
                continue;
            
            // Создаем вложенный класс с константами для свойства
            var propertyNamePascalCase = _namingConverter.ToPascalCase(property.OriginalName);
            var constantsClassName = $"{propertyNamePascalCase}Constants";
            
            // Добавляем XML комментарий с see cref на свойство
            var constantsClassComment = new CommentNode(
                $"Константы валидации для <see cref=\"{model.Name}.{property.Name}\"/>", 
                CommentKind.Xml);
            
            var constantsClass = new ClassDeclarationNode(
                name: new IdentifierNode(constantsClassName),
                accessModifier: AccessModifier.Public,
                isSealed: false, // static классы не могут быть sealed
                isStatic: true,
                isPartial: false,
                baseTypes: ImmutableList<TypeNode>.Empty,
                members: BuildConstantsClassMembers(model, property, constraints, constantsClassName).ToImmutableList(),
                documentation: constantsClassComment);
            
            constantsClasses.Add(constantsClass);
        }
        
        return constantsClasses;
    }
    
    /// <summary>
    /// Строит члены класса с константами валидации
    /// </summary>
    private IReadOnlyList<DeclarationNode> BuildConstantsClassMembers(IrModel model, IrProperty property, IrPropertyConstraints constraints, string constantsClassName)
    {
        var members = new List<DeclarationNode>();
        
        // MinLength константа
        if (constraints.MinLength.HasValue)
        {
            var comment = new CommentNode($"Минимальная длина для <see cref=\"{model.Name}.{property.Name}\"/>", CommentKind.Xml);
            var minLengthField = new FieldDeclarationNode(
                name: new IdentifierNode("MinLength"),
                type: new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
                accessModifier: AccessModifier.Public,
                isReadOnly: false, // const не требует readonly
                isStatic: true,
                defaultValue: new LiteralExpressionNode(constraints.MinLength.Value.ToString(), LiteralKind.Number),
                documentation: comment);
            members.Add(minLengthField);
        }
        
        // MaxLength константа
        if (constraints.MaxLength.HasValue)
        {
            var comment = new CommentNode($"Максимальная длина для <see cref=\"{model.Name}.{property.Name}\"/>", CommentKind.Xml);
            var maxLengthField = new FieldDeclarationNode(
                name: new IdentifierNode("MaxLength"),
                type: new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
                accessModifier: AccessModifier.Public,
                isReadOnly: false, // const не требует readonly
                isStatic: true,
                defaultValue: new LiteralExpressionNode(constraints.MaxLength.Value.ToString(), LiteralKind.Number),
                documentation: comment);
            members.Add(maxLengthField);
        }
        
        // Min константа
        if (constraints.Min.HasValue)
        {
            var numericTypeName = GetNumericTypeName(property.Type);
            var comment = new CommentNode($"Минимальное значение для <see cref=\"{model.Name}.{property.Name}\"/>", CommentKind.Xml);
            var minField = new FieldDeclarationNode(
                name: new IdentifierNode("Min"),
                type: new SimpleTypeNode(new IdentifierNode(numericTypeName), isPrimitive: true),
                accessModifier: AccessModifier.Public,
                isReadOnly: false, // const не требует readonly
                isStatic: true,
                defaultValue: new LiteralExpressionNode(constraints.Min.Value.ToString(), LiteralKind.Number),
                documentation: comment);
            members.Add(minField);
        }
        
        // Max константа
        if (constraints.Max.HasValue)
        {
            var numericTypeName = GetNumericTypeName(property.Type);
            var comment = new CommentNode($"Максимальное значение для <see cref=\"{model.Name}.{property.Name}\"/>", CommentKind.Xml);
            var maxField = new FieldDeclarationNode(
                name: new IdentifierNode("Max"),
                type: new SimpleTypeNode(new IdentifierNode(numericTypeName), isPrimitive: true),
                accessModifier: AccessModifier.Public,
                isReadOnly: false, // const не требует readonly
                isStatic: true,
                defaultValue: new LiteralExpressionNode(constraints.Max.Value.ToString(), LiteralKind.Number),
                documentation: comment);
            members.Add(maxField);
        }
        
        // Pattern константа
        if (!string.IsNullOrEmpty(constraints.Pattern))
        {
            var comment = new CommentNode($"Регулярное выражение для <see cref=\"{model.Name}.{property.Name}\"/>", CommentKind.Xml);
            var patternField = new FieldDeclarationNode(
                name: new IdentifierNode("Pattern"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                accessModifier: AccessModifier.Public,
                isReadOnly: false, // const не требует readonly
                isStatic: true,
                defaultValue: new LiteralExpressionNode(constraints.Pattern, LiteralKind.String),
                documentation: comment);
            members.Add(patternField);
        }
        
        // DefaultValue константа
        if (constraints.DefaultValue != null)
        {
            var comment = new CommentNode($"Значение по умолчанию для <see cref=\"{model.Name}.{property.Name}\"/>", CommentKind.Xml);
            var defaultValueType = InferTypeInfo(property.Type);
            var defaultValueField = new FieldDeclarationNode(
                name: new IdentifierNode("DefaultValue"),
                type: BuildType(property.Type),
                accessModifier: AccessModifier.Public,
                isReadOnly: false, // const не требует readonly, если примитивный тип
                isStatic: true,
                defaultValue: BuildLiteral(constraints.DefaultValue, defaultValueType),
                documentation: comment);
            members.Add(defaultValueField);
        }
        
        return members;
    }
    
    
    /// <summary>
    /// Строит CompilationUnit из IrEnum
    /// </summary>
    public CompilationUnitNode BuildCompilationUnit(IrEnum enumModel, string? @namespace = null)
    {
        var dependencies = new HashSet<string>();
        
        // Если enum имеет строковые значения, добавляем using для EnumMember
        if (enumModel.BaseType == IrEnumBaseType.String)
        {
            dependencies.Add("System.Runtime.Serialization");
        }
        
        var usings = BuildUsingDirectives(dependencies.ToList());
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { BuildEnum(enumModel) }.ToList<DeclarationNode>());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит EnumDeclarationNode из IrEnum
    /// </summary>
    public EnumDeclarationNode BuildEnum(IrEnum enumModel)
    {
        var name = new IdentifierNode(enumModel.Name);
        var comment = !string.IsNullOrEmpty(enumModel.Description)
            ? new CommentNode(enumModel.Description, CommentKind.Xml)
            : null;
        
        var underlyingType = enumModel.BaseType == IrEnumBaseType.Integer
            ? BuildSimpleType("int")
            : null;
        
        var isStringEnum = enumModel.BaseType == IrEnumBaseType.String;
        
        var members = enumModel.Values.Select(v =>
        {
            ExpressionNode? value = null;
            IReadOnlyList<AttributeNode>? attributes = null;
            
            if (isStringEnum)
            {
                // Для строковых enum'ов используем атрибут [EnumMember(Value = "...")] вместо присваивания значения
                var enumMemberAttributeName = new IdentifierNode("EnumMember");
                var valueArg = new LiteralExpressionNode(
                    v.Value?.ToString() ?? string.Empty,
                    LiteralKind.String);
                // В C# именованные аргументы атрибутов: [EnumMember(Value = "...")]
                // Используем BinaryExpression с metadata для указания что это именованный аргумент
                var namedArg = new BinaryExpressionNode(
                    left: new IdentifierExpressionNode(new IdentifierNode("Value")),
                    @operator: BinaryOperator.Add, // Временно, будет обработано в рендерере как именованный аргумент
                    right: valueArg,
                    isNamedArgument: true);
                attributes = new[] { new AttributeNode(enumMemberAttributeName, new[] { namedArg }.ToImmutableList()) }.ToImmutableList();
            }
            else
            {
                // Для числовых enum'ов используем обычное присваивание значения
                value = v.Value != null ? BuildLiteral(v.Value, TypeInfo.Simple("int")) : null;
            }
            
            return new EnumMemberDeclarationNode(
                name: new IdentifierNode(v.Name),
                value: value,
                documentation: !string.IsNullOrEmpty(v.Description)
                    ? new CommentNode(v.Description, CommentKind.Xml)
                    : null,
                attributes: attributes);
        }).ToList<DeclarationNode>();
        
        return new EnumDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            underlyingType: underlyingType,
            members: members.ToImmutableList(),
            documentation: comment);
    }
    
    // Приватные вспомогательные методы для IR
    
    private TypeNode BuildPrimitiveType(IrPrimitiveType primitiveType)
    {
        var typeName = MapPrimitiveToCSharp(primitiveType.PrimitiveKind);
        return new SimpleTypeNode(
            name: new IdentifierNode(typeName),
            isPrimitive: true,
            isNullable: false);
    }
    
    private TypeNode BuildModelReference(IrModelReference modelReference)
    {
        return new SimpleTypeNode(
            name: new IdentifierNode(modelReference.ModelName),
            isPrimitive: false,
            isNullable: false);
    }
    
    private TypeNode BuildArrayType(IrArrayType arrayType)
    {
        var elementType = BuildType(arrayType.ElementType);
        return new ArrayTypeNode(elementType, isNullable: false);
    }
    
    private TypeNode BuildDictionaryType(IrDictionaryType dictionaryType)
    {
        var keyType = BuildType(dictionaryType.KeyType);
        var valueType = BuildType(dictionaryType.ValueType);
        return new DictionaryTypeNode(keyType, valueType, isNullable: false);
    }
    
    private TypeNode BuildUnionType(IrUnionType unionType)
    {
        // В C# union обычно представляется как object или базовый класс
        // Для простоты возвращаем object
        return new SimpleTypeNode(
            name: new IdentifierNode("object"),
            isPrimitive: false,
            isNullable: false);
    }
    
    private TypeNode BuildIntersectionType(IrIntersectionType intersectionType)
    {
        // В C# intersection обычно представляется через наследование
        // Для простоты возвращаем первый тип
        if (intersectionType.Types.Count == 0)
            throw new InvalidOperationException("Intersection type must have at least one type");
        
        return BuildType(intersectionType.Types[0]);
    }
    
    private TypeNode BuildNullableType(IrNullableType nullableType)
    {
        var innerType = BuildType(nullableType.InnerType);
        return new NullableTypeNode(innerType);
    }
    
    private string MapPrimitiveToCSharp(IrPrimitiveKind primitiveKind)
    {
        return primitiveKind switch
        {
            IrPrimitiveKind.String => "string",
            IrPrimitiveKind.Number => "double",
            IrPrimitiveKind.Integer => "int",
            IrPrimitiveKind.Boolean => "bool",
            IrPrimitiveKind.DateTime => "DateTime",
            IrPrimitiveKind.Date => "DateOnly",
            IrPrimitiveKind.Time => "TimeOnly",
            IrPrimitiveKind.Binary => "byte[]",
            IrPrimitiveKind.ByteArray => "byte[]",
            IrPrimitiveKind.Uuid => "Guid",
            IrPrimitiveKind.Any => "object",
            IrPrimitiveKind.Void => "void",
            IrPrimitiveKind.File => "IFormFile",
            _ => "object"
        };
    }
    
    /// <summary>
    /// Получает имя числового типа для констант Min/Max на основе типа свойства
    /// </summary>
    private string GetNumericTypeName(IrType irType)
    {
        // Разворачиваем nullable
        IrType unwrappedType = irType;
        if (irType is IrNullableType nullableType)
        {
            unwrappedType = nullableType.InnerType;
        }
        
        // Если это примитивный тип, используем MapPrimitiveToCSharp
        if (unwrappedType is IrPrimitiveType primitiveType)
        {
            var csharpType = MapPrimitiveToCSharp(primitiveType.PrimitiveKind);
            // Для констант Min/Max используем тот же тип, что и у свойства
            // Integer -> int, Number -> double (можно было бы decimal, но double более универсальный)
            return csharpType;
        }
        
        // Если тип не определен или не числовой, используем decimal как fallback
        return "decimal";
    }
    
    private IReadOnlyList<string> ExtractDependencies(IrModel model)
    {
        // Не добавляем зависимости для типов моделей, так как они генерируются в том же namespace
        // Используем только системные типы и типы из других namespace'ов, если они нужны
        var dependencies = new HashSet<string>();
        
        // Проверяем, используется ли JsonPropertyName атрибут
        foreach (var property in model.Properties)
        {
            if (property.Name != property.OriginalName)
            {
                dependencies.Add("System.Text.Json.Serialization");
                break; // Достаточно одного использования
            }
        }
        
        // Добавляем using для атрибутов валидации, если они используются
        if (_options.GenerateValidationAttributes)
        {
            var hasValidationAttributes = model.Properties.Any(p => 
                p.IsRequired || 
                (p.Constraints != null && (
                    p.Constraints.MinLength.HasValue ||
                    p.Constraints.MaxLength.HasValue ||
                    p.Constraints.Min.HasValue ||
                    p.Constraints.Max.HasValue ||
                    !string.IsNullOrEmpty(p.Constraints.Pattern))));
            
            if (hasValidationAttributes)
            {
                dependencies.Add("System.ComponentModel.DataAnnotations");
            }
        }
        
        // HashCode находится в System, который уже включен по умолчанию
        
        // В будущем можно добавить проверку на типы из других namespace'ов
        // Например, если модель использует типы из System.Collections.Generic и т.д.
        // Но сейчас все модели в одном namespace, поэтому зависимости не нужны
        
        return dependencies.ToList();
    }
    
    private void ExtractDependenciesFromType(IrType type, HashSet<string> dependencies)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                dependencies.Add(modelRef.ModelName);
                break;
            case IrArrayType arrayType:
                ExtractDependenciesFromType(arrayType.ElementType, dependencies);
                break;
            case IrDictionaryType dictType:
                ExtractDependenciesFromType(dictType.KeyType, dependencies);
                ExtractDependenciesFromType(dictType.ValueType, dependencies);
                break;
            case IrUnionType unionType:
                foreach (var t in unionType.Types)
                    ExtractDependenciesFromType(t, dependencies);
                break;
            case IrIntersectionType intersectionType:
                foreach (var t in intersectionType.Types)
                    ExtractDependenciesFromType(t, dependencies);
                break;
            case IrNullableType nullableType:
                ExtractDependenciesFromType(nullableType.InnerType, dependencies);
                break;
            case IrOptionalType optionalType:
                ExtractDependenciesFromType(optionalType.InnerType, dependencies);
                break;
        }
    }
    
    private TypeInfo InferTypeInfo(IrType irType)
    {
        // Простая реализация для определения TypeInfo из IrType
        // Используется только для BuildLiteral
        return irType.Kind switch
        {
            IrTypeKind.Primitive => TypeInfo.Simple(MapPrimitiveToCSharp(((IrPrimitiveType)irType).PrimitiveKind), isPrimitive: true),
            IrTypeKind.ModelReference => TypeInfo.Simple(((IrModelReference)irType).ModelName),
            _ => TypeInfo.Simple("object")
        };
    }
    
    /// <summary>
    /// Строит MethodDeclarationNode из IrOperation
    /// </summary>
    public MethodDeclarationNode BuildMethod(IrOperation operation)
    {
        return BuildMethod(operation, includeBody: true, includeDocumentation: true);
    }
    
    /// <summary>
    /// Строит MethodDeclarationNode из IrOperation с опциями
    /// </summary>
    private MethodDeclarationNode BuildMethod(IrOperation operation, bool includeBody, bool includeDocumentation)
    {
        var name = new IdentifierNode(operation.MethodName);
        
        // Определяем тип возвращаемого значения из responses
        var returnType = ExtractReturnType(operation.Responses);
        
        CommentNode? comment = null;
        if (includeDocumentation)
        {
            comment = !string.IsNullOrEmpty(operation.Description)
                ? new CommentNode(operation.Description, CommentKind.Xml)
                : !string.IsNullOrEmpty(operation.Summary)
                    ? new CommentNode(operation.Summary, CommentKind.Xml)
                    : null;
        }
        
        var parameters = BuildMethodParameters(operation);
        var body = includeBody ? BuildMethodBody(operation) : null;
        
        return new MethodDeclarationNode(
            name: name,
            returnType: returnType,
            parameters: parameters,
            body: body,
            accessModifier: AccessModifier.Public,
            isAsync: true,
            documentation: comment,
            metadata: null,
            isInterfaceMethod: !includeBody,
            useInheritDoc: false);
    }
    
    /// <summary>
    /// Строит MethodDeclarationNode для интерфейса (без тела)
    /// </summary>
    private MethodDeclarationNode BuildInterfaceMethod(IrOperation operation)
    {
        return BuildMethod(operation, includeBody: false, includeDocumentation: true);
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrDocument (все модели)
    /// </summary>
    public IReadOnlyList<CompilationUnitNode> BuildCompilationUnits(IrDocument document, string? @namespace = null)
    {
        // Сохраняем модели по имени для доступа к родительским моделям в методах равенства
        _modelsByName = document.Models.ToDictionary(m => m.Name, m => m);
        
        return BuildCompilationUnitsInternal(document, @namespace);
    }
    
    /// <summary>
    /// Внутренний метод построения CompilationUnit
    /// </summary>
    private IReadOnlyList<CompilationUnitNode> BuildCompilationUnitsInternal(IrDocument document, string? @namespace = null)
    {
        var compilationUnits = new List<CompilationUnitNode>();
        
        // Генерируем модели в топологическом порядке
        var orderedModelNames = document.TypeDependencies.TopologicalOrder;
        var modelsByName = document.Models.ToDictionary(m => m.Name);
        
        foreach (var modelName in orderedModelNames)
        {
            if (modelsByName.TryGetValue(modelName, out var model))
            {
                compilationUnits.Add(BuildCompilationUnit(model, @namespace));
            }
        }
        
        // Добавляем модели, которые не были в топологическом порядке
        foreach (var model in document.Models)
        {
            if (!orderedModelNames.Contains(model.Name))
            {
                compilationUnits.Add(BuildCompilationUnit(model, @namespace));
            }
        }
        
        // Генерируем enum'ы
        foreach (var enumModel in document.Enums)
        {
            compilationUnits.Add(BuildCompilationUnit(enumModel, @namespace));
        }
        
        // Генерируем IHttpClientMetrics и HttpClientMetrics один раз, если включены метрики и есть хотя бы один клиент
        if (_options.GenerateMetrics && document.Clients.Count > 0)
        {
            compilationUnits.Add(BuildCompilationUnitForIHttpClientMetrics(@namespace));
            compilationUnits.Add(BuildCompilationUnitForHttpClientMetrics(@namespace));
        }
        
        // Генерируем клиенты
        foreach (var client in document.Clients)
        {
            // Если интерфейсы в отдельном файле, генерируем их отдельно
            if (_options.GenerateInterfaces && _options.InterfaceFileStrategy == InterfaceFileStrategy.SeparateFile)
            {
                compilationUnits.Add(BuildCompilationUnitForInterface(client, @namespace));
            }
            
            compilationUnits.Add(BuildCompilationUnit(client, @namespace));
            
            // Если включен OpenTelemetry (traces или metrics), генерируем декоратор
            if (_options.GenerateTraces || _options.GenerateMetrics)
            {
                compilationUnits.Add(BuildCompilationUnitForDecorator(client, @namespace));
            }
        }
        
        // Генерируем ApiException один раз, если есть хотя бы один клиент
        if (document.Clients.Count > 0)
        {
            compilationUnits.Add(BuildCompilationUnitApiException(@namespace));
        }
        
        return compilationUnits;
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrClient
    /// </summary>
    public CompilationUnitNode BuildCompilationUnit(IrClient client, string? @namespace = null)
    {
        var usings = BuildUsingDirectives(ExtractDependencies(client));
        var declarations = new List<DeclarationNode>();
        
        // Если нужно генерировать интерфейсы и они в том же файле
        if (_options.GenerateInterfaces && _options.InterfaceFileStrategy == InterfaceFileStrategy.SameFile)
        {
            var interfaceNode = BuildClientInterface(client);
            declarations.Add(interfaceNode);
        }
        
        var classNode = BuildClientClass(client);
        declarations.Add(classNode);
        
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, declarations);
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит CompilationUnit для интерфейса клиента (для отдельного файла)
    /// </summary>
    public CompilationUnitNode BuildCompilationUnitForInterface(IrClient client, string? @namespace = null)
    {
        var usings = BuildUsingDirectives(ExtractDependencies(client));
        var interfaceNode = BuildClientInterface(client);
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { (DeclarationNode)interfaceNode }.ToList());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит CompilationUnit для интерфейса IHttpClientMetrics
    /// </summary>
    public CompilationUnitNode BuildCompilationUnitForIHttpClientMetrics(string? @namespace = null)
    {
        var dependencies = new HashSet<string> { "System.Diagnostics" };
        var usings = BuildUsingDirectives(dependencies.ToList());
        var metricsInterface = BuildIHttpClientMetricsInterface();
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { (DeclarationNode)metricsInterface }.ToList());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит CompilationUnit для класса HttpClientMetrics
    /// </summary>
    public CompilationUnitNode BuildCompilationUnitForHttpClientMetrics(string? @namespace = null)
    {
        var dependencies = new HashSet<string> { "System.Diagnostics" };
        var usings = BuildUsingDirectives(dependencies.ToList());
        var metricsClass = BuildHttpClientMetricsClass();
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { (DeclarationNode)metricsClass }.ToList());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит CompilationUnit для декоратора OpenTelemetry
    /// </summary>
    public CompilationUnitNode BuildCompilationUnitForDecorator(IrClient client, string? @namespace = null)
    {
        var dependencies = ExtractDependencies(client).ToList();
        // Добавляем зависимости для OpenTelemetry
        dependencies.Add("System.Diagnostics");
        
        var usings = BuildUsingDirectives(dependencies);
        var decoratorNode = BuildOpenTelemetryDecorator(client);
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { (DeclarationNode)decoratorNode }.ToList());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит ClassDeclarationNode для декоратора OpenTelemetry
    /// </summary>
    private ClassDeclarationNode BuildOpenTelemetryDecorator(IrClient client)
    {
        var decoratorName = new IdentifierNode($"{client.Name}TelemetryDecorator");
        var baseClientName = new IdentifierNode(client.Name);
        var interfaceName = _options.GenerateInterfaces 
            ? new IdentifierNode($"I{client.Name}")
            : null;
        
        var members = new List<DeclarationNode>();
        
        // Поле для базового клиента
        var baseClientField = new FieldDeclarationNode(
            name: new IdentifierNode("_baseClient"),
            type: interfaceName != null 
                ? new SimpleTypeNode(interfaceName, isPrimitive: false)
                : new SimpleTypeNode(baseClientName, isPrimitive: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(baseClientField);
        
        // Поле для ActivitySource
        var activitySourceField = new FieldDeclarationNode(
            name: new IdentifierNode("_activitySource"),
            type: new SimpleTypeNode(new IdentifierNode("ActivitySource"), isPrimitive: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(activitySourceField);
        
        // Поле для метрик (если включены)
        if (_options.GenerateMetrics)
        {
            var metricsField = new FieldDeclarationNode(
                name: new IdentifierNode("_metrics"),
                type: new SimpleTypeNode(new IdentifierNode("HttpClientMetrics"), isPrimitive: false),
                accessModifier: AccessModifier.Private,
                isReadOnly: true,
                defaultValue: null);
            members.Add(metricsField);
        }
        
        // Конструктор
        var constructorParams = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("baseClient"),
                type: interfaceName != null 
                    ? new SimpleTypeNode(interfaceName, isPrimitive: false)
                    : new SimpleTypeNode(baseClientName, isPrimitive: false),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("activitySource"),
                type: new SimpleTypeNode(new IdentifierNode("ActivitySource"), isPrimitive: false),
                isRequired: true)
        };
        
        // Добавляем параметры для метрик, если включены
        if (_options.GenerateMetrics)
        {
            constructorParams.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("metrics"),
                type: new SimpleTypeNode(new IdentifierNode("IHttpClientMetrics"), isPrimitive: false),
                isRequired: true));
        }
        
        var constructorBodyStatements = new List<StatementNode>();
        // _baseClient = baseClient;
        var baseClientAssignment = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_baseClient")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("baseClient")));
        constructorBodyStatements.Add(new ExpressionStatementNode(baseClientAssignment));
        
        // _activitySource = activitySource;
        var activitySourceAssignment = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_activitySource")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("activitySource")));
        constructorBodyStatements.Add(new ExpressionStatementNode(activitySourceAssignment));
        
        // Инициализация метрик, если включены
        if (_options.GenerateMetrics)
        {
            // _metrics = metrics;
            var metricsAssignment = new BinaryExpressionNode(
                left: new IdentifierExpressionNode(new IdentifierNode("_metrics")),
                @operator: BinaryOperator.Assign,
                right: new IdentifierExpressionNode(new IdentifierNode("metrics")));
            constructorBodyStatements.Add(new ExpressionStatementNode(metricsAssignment));
        }
        
        var constructorBody = new BlockNode(constructorBodyStatements.ToImmutableList());
        var constructor = new ConstructorDeclarationNode(
            name: decoratorName,
            parameters: constructorParams.ToImmutableList(),
            body: constructorBody,
            accessModifier: AccessModifier.Public);
        members.Add(constructor);
        
        // Базовые типы (интерфейс если есть, иначе пусто)
        var baseTypes = new List<TypeNode>();
        if (interfaceName != null)
        {
            baseTypes.Add(new SimpleTypeNode(interfaceName, isPrimitive: false));
        }
        
        // Методы декоратора (оборачивают вызовы базового клиента в Activity)
        var decoratorMethods = client.Operations
            .Select(op => BuildDecoratorMethod(op, client.Name))
            .ToList<DeclarationNode>();
        members.AddRange(decoratorMethods);
        
        return new ClassDeclarationNode(
            name: decoratorName,
            accessModifier: AccessModifier.Public,
            isSealed: true,
            isStatic: false,
            isPartial: false,
            baseTypes: baseTypes.ToImmutableList(),
            members: members.ToImmutableList());
    }
    
    /// <summary>
    /// Строит MethodDeclarationNode для декоратора (оборачивает вызов в Activity)
    /// </summary>
    private MethodDeclarationNode BuildDecoratorMethod(IrOperation operation, string clientName)
    {
        var name = new IdentifierNode(operation.MethodName);
        var returnType = ExtractReturnType(operation.Responses);
        var parameters = BuildMethodParameters(operation);
        
        // Тело метода декоратора
        var statements = new List<StatementNode>();
        
        // var activity = _activitySource.StartActivity("{serviceName}.ClientName.MethodName" или "ClientName.MethodName");
        var serviceName = _options.ServiceName;
        var activityName = string.IsNullOrWhiteSpace(serviceName)
            ? $"{clientName}.{operation.MethodName}"
            : $"{serviceName}.{clientName}.{operation.MethodName}";
        var activityNameLiteral = new LiteralExpressionNode(activityName, LiteralKind.String);
        var startActivityCall = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("_activitySource")),
                member: new IdentifierNode("StartActivity")),
            arguments: new[] { activityNameLiteral }.ToImmutableList());
        
        var activityVar = new IdentifierNode("activity");
        TypeNode activityType = _options.NullableActivity
            ? new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Activity"), isPrimitive: false))
            : new SimpleTypeNode(new IdentifierNode("Activity"), isPrimitive: false);
        var activityDeclaration = new VariableDeclarationStatementNode(
            name: activityVar,
            type: activityType,
            initializer: startActivityCall);
        statements.Add(activityDeclaration);
        
        // Вызов базового клиента с метриками (если включены) или без них
        ExpressionNode baseClientCallExpression;
        
        if (_options.GenerateMetrics)
        {
            // return await _metrics.Measure(clientName, methodName, async () => await _baseClient.MethodName(...));
            var baseClientCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(new IdentifierNode("_baseClient")),
                    member: name),
                arguments: parameters.Select(p => new IdentifierExpressionNode(p.Name)).ToImmutableList<ExpressionNode>());
            
            var awaitBaseClientCall = new AwaitExpressionNode(baseClientCall);
            
            // Создаем лямбда-выражение: async () => await _baseClient.MethodName(...)
            var lambdaExpression = new CallExpressionNode(
                target: new IdentifierExpressionNode(new IdentifierNode("async")),
                arguments: ImmutableList<ExpressionNode>.Empty,
                isLambda: true,
                lambdaBody: awaitBaseClientCall);
            
            // _metrics.Measure<T>(clientName, methodName, async () => await _baseClient.MethodName(...))
            var returnTypeGenericArg = ExtractReturnTypeGenericArgument(returnType);
            var clientNameLiteral = new LiteralExpressionNode(clientName, LiteralKind.String);
            var methodNameLiteral = new LiteralExpressionNode(operation.MethodName, LiteralKind.String);
            var measureCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(new IdentifierNode("_metrics")),
                    member: new IdentifierNode("Measure")),
                arguments: new ExpressionNode[] { clientNameLiteral, methodNameLiteral, lambdaExpression }.ToImmutableList<ExpressionNode>(),
                genericTypeArgument: returnTypeGenericArg);
            
            baseClientCallExpression = new AwaitExpressionNode(measureCall);
        }
        else
        {
            // return await _baseClient.MethodName(...);
            var baseClientCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(new IdentifierNode("_baseClient")),
                    member: name),
                arguments: parameters.Select(p => new IdentifierExpressionNode(p.Name)).ToImmutableList<ExpressionNode>());
            
            baseClientCallExpression = new AwaitExpressionNode(baseClientCall);
        }
        
        // try {
        var tryStatements = new List<StatementNode>();
        
        var returnStatement = new ReturnStatementNode(baseClientCallExpression);
        tryStatements.Add(returnStatement);
        
        var tryBlock = new BlockNode(tryStatements.ToImmutableList());
        
        // catch (Exception ex) {
        var catchStatements = new List<StatementNode>();
        
        // if (activity != null) { activity.SetStatus(ActivityStatusCode.Error, ex.Message); activity.RecordException(ex); }
        // или просто activity.SetStatus(...) если NullableActivity = false
        if (_options.GenerateTraces)
        {
            var exceptionVar = new IdentifierNode("ex");
            
            var setStatusCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(activityVar),
                    member: new IdentifierNode("SetStatus")),
                arguments: new[]
                {
                    new MemberAccessExpressionNode(
                        @object: new IdentifierExpressionNode(new IdentifierNode("ActivityStatusCode")),
                        member: new IdentifierNode("Error")),
                    new MemberAccessExpressionNode(
                        @object: new IdentifierExpressionNode(exceptionVar),
                        member: new IdentifierNode("Message"))
                }.ToImmutableList<ExpressionNode>());
            
            var recordExceptionCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(activityVar),
                    member: new IdentifierNode("RecordException")),
                arguments: new[] { new IdentifierExpressionNode(exceptionVar) }.ToImmutableList<ExpressionNode>());
            
            var ifBodyStatements = new List<StatementNode>
            {
                new ExpressionStatementNode(setStatusCall),
                new ExpressionStatementNode(recordExceptionCall)
            };
            
            if (_options.NullableActivity)
            {
                // Если Activity nullable, проверяем на null
                var activityNotNull = new BinaryExpressionNode(
                    left: new IdentifierExpressionNode(activityVar),
                    @operator: BinaryOperator.NotEqual,
                    right: new LiteralExpressionNode("null", LiteralKind.Null));
                var ifBody = new BlockNode(ifBodyStatements.ToImmutableList());
                var ifStatement = new IfStatementNode(activityNotNull, ifBody);
                catchStatements.Add(ifStatement);
            }
            else
            {
                // Если Activity not nullable, просто выполняем без проверки
                catchStatements.AddRange(ifBodyStatements.Select(s => s));
            }
        }
        
        // throw; (rethrow)
        catchStatements.Add(new ThrowStatementNode(null));
        
        var catchBlock = new BlockNode(catchStatements.ToImmutableList());
        var catchClause = new CatchClauseNode(
            body: catchBlock,
            exceptionType: new SimpleTypeNode(new IdentifierNode("Exception"), isPrimitive: false),
            exceptionVariable: new IdentifierNode("ex"));
        
        // finally {
        var finallyStatements = new List<StatementNode>();
        
        // if (activity != null) { activity.Dispose(); }
        // или просто activity.Dispose() если NullableActivity = false
        if (_options.GenerateTraces)
        {
            var activityDisposeCall = new CallExpressionNode(
                target: new MemberAccessExpressionNode(
                    @object: new IdentifierExpressionNode(activityVar),
                    member: new IdentifierNode("Dispose")),
                arguments: ImmutableList<ExpressionNode>.Empty);
            
            if (_options.NullableActivity)
            {
                // Если Activity nullable, проверяем на null
                var activityNotNull = new BinaryExpressionNode(
                    left: new IdentifierExpressionNode(activityVar),
                    @operator: BinaryOperator.NotEqual,
                    right: new LiteralExpressionNode("null", LiteralKind.Null));
                var ifBody = new BlockNode(new[] { new ExpressionStatementNode(activityDisposeCall) }.ToImmutableList());
                var ifStatement = new IfStatementNode(activityNotNull, ifBody);
                finallyStatements.Add(ifStatement);
            }
            else
            {
                // Если Activity not nullable, просто вызываем Dispose
                finallyStatements.Add(new ExpressionStatementNode(activityDisposeCall));
            }
        }
        
        var finallyBlock = new BlockNode(finallyStatements.ToImmutableList());
        
        // Создаем try-catch-finally
        var tryCatchStatement = new TryCatchStatementNode(
            tryBlock: tryBlock,
            catchClauses: new[] { catchClause }.ToImmutableList(),
            finallyBlock: finallyBlock);
        statements.Add(tryCatchStatement);
        
        // Метрики для успешных вызовов (перед try-catch, так как они должны быть в finally)
        // Но лучше добавить их в finally после измерения длительности
        
        var body = new BlockNode(statements.ToImmutableList());
        
        return new MethodDeclarationNode(
            name: name,
            returnType: returnType,
            parameters: parameters,
            body: body,
            accessModifier: AccessModifier.Public,
            isAsync: true,
            documentation: null);
    }
    
    // Приватные вспомогательные методы для IR операций
    
    private IReadOnlyList<ParameterDeclarationNode> BuildMethodParameters(IrOperation operation)
    {
        var parameters = new List<ParameterDeclarationNode>();
        
        // Path parameters
        foreach (var param in operation.PathParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: true));
        }
        
        // Query parameters
        foreach (var param in operation.QueryParameters)
        {
            var type = BuildType(param.Type);
            // В C# опциональные параметры делаются через nullable или default значения
            var nullableType = param.IsRequired ? type : new NullableTypeNode(type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: nullableType,
                isRequired: param.IsRequired));
        }
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            var type = BuildType(param.Type);
            var nullableType = param.IsRequired ? type : new NullableTypeNode(type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: nullableType,
                isRequired: param.IsRequired));
        }
        
        // Request body
        if (operation.RequestBody != null)
        {
            var bodyType = BuildType(operation.RequestBody.Type);
            // Логика проверки required свойств теперь на уровне IR
            // IrRequestBody.IsRequired уже учитывает наличие required свойств в модели
            var nullableType = operation.RequestBody.IsRequired ? bodyType : new NullableTypeNode(bodyType);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("request"),
                type: nullableType,
                isRequired: operation.RequestBody.IsRequired));
        }
        
        // CancellationToken с default value
        var cancellationTokenType = new SimpleTypeNode(new IdentifierNode("CancellationToken"), isPrimitive: false);
        var defaultCancellationToken = new LiteralExpressionNode("default", LiteralKind.String);
        parameters.Add(new ParameterDeclarationNode(
            name: new IdentifierNode("cancellationToken"),
            type: cancellationTokenType,
            isRequired: false,
            defaultValue: defaultCancellationToken));
        
        return parameters.ToImmutableList();
    }
    
    private BlockNode? BuildMethodBody(IrOperation operation)
    {
        var statements = new List<StatementNode>();
        
        // Находим success и error response types
        var successResponse = operation.Responses.FirstOrDefault(r =>
        {
            if (int.TryParse(r.StatusCode, out var code))
                return code >= 200 && code < 300;
            return false;
        });
        
        var errorResponses = operation.Responses
            .Where(r => !int.TryParse(r.StatusCode, out var code) || code < 200 || code >= 300)
            .ToList();
        
        var successType = successResponse?.Type != null ? BuildType(successResponse.Type) : null;
        var errorTypes = errorResponses
            .Where(r => r.Type != null)
            .Select(r => new { StatusCode = r.StatusCode, Type = BuildType(r.Type!) })
            .ToList();
        
        // Подсчитываем capacity для StringBuilder
        var estimatedCapacity = EstimateStringBuilderCapacity(operation);
        
        // 1. Строим URL через StringBuilder с предварительно рассчитанной capacity
        var capacityLiteral = new LiteralExpressionNode(estimatedCapacity.ToString(), LiteralKind.Number);
        var urlBuilderVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("urlBuilder"),
            type: new SimpleTypeNode(new IdentifierNode("StringBuilder"), isPrimitive: false),
            initializer: new CallExpressionNode(
                target: new IdentifierExpressionNode(new IdentifierNode("StringBuilder")),
                arguments: new ExpressionNode[] { capacityLiteral }.ToImmutableList(),
                isNewExpression: true));
        statements.Add(urlBuilderVar);
        
        // Строим URL по частям, заменяя {param} на значения
        var basePath = operation.Path;
        var appendMethod = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("urlBuilder")),
            member: new IdentifierNode("Append"));
        
        // Парсим путь и добавляем части в StringBuilder
        var pathParts = new List<(string? Text, string? ParamName)>();
        var currentIndex = 0;
        
        while (currentIndex < basePath.Length)
        {
            var paramStart = basePath.IndexOf('{', currentIndex);
            if (paramStart == -1)
            {
                // Остаток пути без параметров
                if (currentIndex < basePath.Length)
                {
                    pathParts.Add((basePath.Substring(currentIndex), null));
                }
                break;
            }
            
            // Добавляем текст до параметра
            if (paramStart > currentIndex)
            {
                pathParts.Add((basePath.Substring(currentIndex, paramStart - currentIndex), null));
            }
            
            // Находим конец параметра
            var paramEnd = basePath.IndexOf('}', paramStart);
            if (paramEnd == -1)
            {
                // Некорректный формат, добавляем как есть
                pathParts.Add((basePath.Substring(paramStart), null));
                break;
            }
            
            // Извлекаем имя параметра
            var paramName = basePath.Substring(paramStart + 1, paramEnd - paramStart - 1);
            var sanitizedParamName = _namingConverter.SanitizeIdentifier(paramName);
            pathParts.Add((Text: (string?)null, ParamName: sanitizedParamName));
            
            currentIndex = paramEnd + 1;
        }
        
        // Добавляем части в StringBuilder
        foreach (var (text, paramName) in pathParts)
        {
            if (text != null)
            {
                // Исправляем двойное экранирование - убираем лишние кавычки
                var appendTextCall = new CallExpressionNode(
                    target: appendMethod,
                    arguments: new[] { new LiteralExpressionNode(text, LiteralKind.String) }.ToImmutableList());
                statements.Add(new ExpressionStatementNode(appendTextCall));
            }
            else if (paramName != null)
            {
                // Находим соответствующий path parameter
                var pathParam = operation.PathParameters.FirstOrDefault(p => p.Name == paramName);
                if (pathParam != null)
                {
                    var paramExpr = new IdentifierExpressionNode(new IdentifierNode(pathParam.Name));
                    var paramType = BuildType(pathParam.Type);
                    
                    // Если это строка, используем Uri.EscapeDataString
                    var isStringType = paramType is SimpleTypeNode simple && simple.Name.Name == "string";
                    
                    ExpressionNode paramValue;
                    if (isStringType)
                    {
                        // Uri.EscapeDataString(param)
                        var uriType = new IdentifierExpressionNode(new IdentifierNode("Uri"));
                        var escapeDataString = new MemberAccessExpressionNode(
                            @object: uriType,
                            member: new IdentifierNode("EscapeDataString"));
                        paramValue = new CallExpressionNode(
                            target: escapeDataString,
                            arguments: new[] { paramExpr }.ToImmutableList());
                    }
                    else
                    {
                        // Для не-строк просто ToString()
                        var toStringMethod = new MemberAccessExpressionNode(
                            @object: paramExpr,
                            member: new IdentifierNode("ToString"));
                        paramValue = new CallExpressionNode(
                            target: toStringMethod,
                            arguments: ImmutableList<ExpressionNode>.Empty);
                    }
                    
                    var appendParamCall = new CallExpressionNode(
                        target: appendMethod,
                        arguments: new[] { paramValue }.ToImmutableList());
                    statements.Add(new ExpressionStatementNode(appendParamCall));
                }
            }
        }
        
        // Добавляем query parameters
        if (operation.QueryParameters.Count > 0)
        {
            var firstParam = operation.QueryParameters[0];
            var appendQuerySeparator = new CallExpressionNode(
                target: appendMethod,
                arguments: new[] { new LiteralExpressionNode("?", LiteralKind.String) }.ToImmutableList());
            statements.Add(new ExpressionStatementNode(appendQuerySeparator));
            
            for (int i = 0; i < operation.QueryParameters.Count; i++)
            {
                var queryParam = operation.QueryParameters[i];
                if (i > 0)
                {
                    var appendAnd = new CallExpressionNode(
                        target: appendMethod,
                        arguments: new[] { new LiteralExpressionNode("&", LiteralKind.String) }.ToImmutableList());
                    statements.Add(new ExpressionStatementNode(appendAnd));
                }
                
                var paramName = new LiteralExpressionNode($"{queryParam.Name}=", LiteralKind.String);
                var appendName = new CallExpressionNode(
                    target: appendMethod,
                    arguments: new[] { paramName }.ToImmutableList());
                statements.Add(new ExpressionStatementNode(appendName));
                
                var paramExpr = new IdentifierExpressionNode(new IdentifierNode(queryParam.Name));
                var paramType = BuildType(queryParam.Type);
                var isStringType = paramType is SimpleTypeNode simple && simple.Name.Name == "string";
                
                ExpressionNode paramValue;
                if (isStringType)
                {
                    var uriType = new IdentifierExpressionNode(new IdentifierNode("Uri"));
                    var escapeDataString = new MemberAccessExpressionNode(
                        @object: uriType,
                        member: new IdentifierNode("EscapeDataString"));
                    paramValue = new CallExpressionNode(
                        target: escapeDataString,
                        arguments: new[] { paramExpr }.ToImmutableList());
                }
                else
                {
                    var toStringMethod = new MemberAccessExpressionNode(
                        @object: paramExpr,
                        member: new IdentifierNode("ToString"));
                    paramValue = new CallExpressionNode(
                        target: toStringMethod,
                        arguments: ImmutableList<ExpressionNode>.Empty);
                }
                
                var appendValue = new CallExpressionNode(
                    target: appendMethod,
                    arguments: new[] { paramValue }.ToImmutableList());
                statements.Add(new ExpressionStatementNode(appendValue));
            }
        }
        
        // Получаем финальный URL: urlBuilder.ToString()
        var urlBuilderToString = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("urlBuilder")),
            member: new IdentifierNode("ToString"));
        var urlStringVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("url"),
            type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            initializer: new CallExpressionNode(
                target: urlBuilderToString,
                arguments: ImmutableList<ExpressionNode>.Empty));
        statements.Add(urlStringVar);
        
        // 2-5. Создаем HttpRequestMessage и отправляем запрос через HttpClient.SendAsync с using
        // Используем HttpMethod.Get, HttpMethod.Post и т.д. вместо new HttpMethod("GET")
        var httpMethodName = GetHttpMethodPropertyName(operation.HttpMethod);
        var httpMethodStaticProperty = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("HttpMethod")),
            member: new IdentifierNode(httpMethodName));
        
        var requestMessageType = new IdentifierExpressionNode(new IdentifierNode("HttpRequestMessage"));
        var urlExpr = new IdentifierExpressionNode(new IdentifierNode("url"));
        var requestMessageCtorArgs = new ExpressionNode[] { httpMethodStaticProperty, urlExpr };
        var requestMessageCtor = new CallExpressionNode(
            target: requestMessageType,
            arguments: requestMessageCtorArgs.ToImmutableList(),
            isNewExpression: true);
        
        var requestMessageVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("requestMessage"),
            type: new SimpleTypeNode(new IdentifierNode("HttpRequestMessage"), isPrimitive: false),
            initializer: requestMessageCtor);
        
        // Создаем блок для using statement requestMessage
        var requestMessageUsingBodyStatements = new List<StatementNode>();
        
        // 3. Устанавливаем Headers (Accept: application/json)
        var requestMessageHeaders = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("requestMessage")),
            member: new IdentifierNode("Headers"));
        var acceptHeader = new MemberAccessExpressionNode(
            @object: requestMessageHeaders,
            member: new IdentifierNode("Accept"));
        var acceptAdd = new MemberAccessExpressionNode(
            @object: acceptHeader,
            member: new IdentifierNode("Add"));
        var mediaTypeJson = new IdentifierExpressionNode(new IdentifierNode("MediaTypeWithQualityHeaderValue"));
        var mediaTypeJsonCtor = new CallExpressionNode(
            target: mediaTypeJson,
            arguments: new ExpressionNode[] { new LiteralExpressionNode("application/json", LiteralKind.String) }.ToImmutableList(),
            isNewExpression: true);
        var acceptAddCall = new CallExpressionNode(
            target: acceptAdd,
            arguments: new[] { mediaTypeJsonCtor }.ToImmutableList());
        requestMessageUsingBodyStatements.Add(new ExpressionStatementNode(acceptAddCall));
        
        // 4. Добавляем request body если есть
        if (operation.RequestBody != null)
        {
            var requestBodyParam = new IdentifierExpressionNode(new IdentifierNode("request")); // Параметр метода request body
            var jsonSerializerForSerialize = new IdentifierExpressionNode(new IdentifierNode("JsonSerializer"));
            var serializeMethod = new MemberAccessExpressionNode(
                @object: jsonSerializerForSerialize,
                member: new IdentifierNode("Serialize"));
            var serializeCall = new CallExpressionNode(
                target: serializeMethod,
                arguments: new[] { requestBodyParam }.ToImmutableList());
            
            // Помещаем JsonSerializer.Serialize(request) в промежуточную переменную requestJsonContent
            var requestJsonContentVar = new VariableDeclarationStatementNode(
                name: new IdentifierNode("requestJsonContent"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                initializer: serializeCall);
            requestMessageUsingBodyStatements.Add(requestJsonContentVar);
            
            var stringContentTypeExpr = new IdentifierExpressionNode(new IdentifierNode("StringContent"));
            var encodingExpr = new IdentifierExpressionNode(new IdentifierNode("Encoding"));
            var encodingUtf8 = new MemberAccessExpressionNode(
                @object: encodingExpr,
                member: new IdentifierNode("UTF8"));
            var mediaTypeHeader = new LiteralExpressionNode("application/json", LiteralKind.String);
            
            var requestJsonContentExpr = new IdentifierExpressionNode(new IdentifierNode("requestJsonContent"));
            var stringContentCtorArgs = new ExpressionNode[] { requestJsonContentExpr, encodingUtf8, mediaTypeHeader };
            var stringContentCtor = new CallExpressionNode(
                target: stringContentTypeExpr,
                arguments: stringContentCtorArgs.ToImmutableList(),
                isNewExpression: true);
            
            var requestMessageContent = new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("requestMessage")),
                member: new IdentifierNode("Content"));
            var contentAssign = new BinaryExpressionNode(
                left: requestMessageContent,
                @operator: BinaryOperator.Assign,
                right: stringContentCtor);
            requestMessageUsingBodyStatements.Add(new ExpressionStatementNode(contentAssign));
        }
        
        // Извлекаем данные из requestMessage для ApiException (до отправки запроса, чтобы были доступны в блоках обработки ошибок)
        var requestUriAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("requestMessage")),
            member: new IdentifierNode("RequestUri"));
        var requestUriVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("requestUri"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Uri"), isPrimitive: false)),
            initializer: requestUriAccess);
        requestMessageUsingBodyStatements.Add(requestUriVar);
        
        var requestMethodAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("requestMessage")),
            member: new IdentifierNode("Method"));
        var requestMethodVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("httpMethod"),
            type: new SimpleTypeNode(new IdentifierNode("HttpMethod"), isPrimitive: false),
            initializer: requestMethodAccess);
        requestMessageUsingBodyStatements.Add(requestMethodVar);
        
        // 5. Отправляем запрос через HttpClient.SendAsync
        var httpClientField = new IdentifierExpressionNode(new IdentifierNode("_httpClient"));
        var sendAsyncMethod = new MemberAccessExpressionNode(
            @object: httpClientField,
            member: new IdentifierNode("SendAsync"));
        var cancellationTokenParam = new IdentifierExpressionNode(new IdentifierNode("cancellationToken"));
        var requestMessageExpr = new IdentifierExpressionNode(new IdentifierNode("requestMessage"));
        var sendAsyncCall = new CallExpressionNode(
            target: sendAsyncMethod,
            arguments: new[] { requestMessageExpr, cancellationTokenParam }.ToImmutableList());
        var awaitSend = new AwaitExpressionNode(sendAsyncCall);
        
        // Создаем блок для вложенного using statement (responseMessage внутри requestMessage)
        // Сначала создаем переменную responseMessage
        var responseMessageVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("responseMessage"),
            type: new SimpleTypeNode(new IdentifierNode("HttpResponseMessage"), isPrimitive: false),
            initializer: awaitSend);
        
        var responseMessageUsingBodyStatements = new List<StatementNode>();
        
        // ReadAsStringAsync (переименовано content -> responseContent)
        var responseMessageContentAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("responseMessage")),
            member: new IdentifierNode("Content"));
        var readAsString = new MemberAccessExpressionNode(
            @object: responseMessageContentAccess,
            member: new IdentifierNode("ReadAsStringAsync"));
        var cancellationTokenParamForRead = new IdentifierExpressionNode(new IdentifierNode("cancellationToken"));
        var readAsStringCall = new CallExpressionNode(
            target: readAsString,
            arguments: new[] { cancellationTokenParamForRead }.ToImmutableList());
        var awaitRead = new AwaitExpressionNode(readAsStringCall);
        
        var responseContentVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("responseContent"),
            type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            initializer: awaitRead);
        responseMessageUsingBodyStatements.Add(responseContentVar);
        
        // Извлекаем данные из responseMessage для ApiException
        var statusCodeAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("responseMessage")),
            member: new IdentifierNode("StatusCode"));
        var statusCodeVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("statusCode"),
            type: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
            initializer: statusCodeAccess);
        responseMessageUsingBodyStatements.Add(statusCodeVar);
        
        var reasonPhraseAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("responseMessage")),
            member: new IdentifierNode("ReasonPhrase"));
        var reasonPhraseVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("reasonPhrase"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)),
            initializer: reasonPhraseAccess);
        responseMessageUsingBodyStatements.Add(reasonPhraseVar);
        
        var versionAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("responseMessage")),
            member: new IdentifierNode("Version"));
        var versionVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("version"),
            type: new SimpleTypeNode(new IdentifierNode("Version"), isPrimitive: false),
            initializer: versionAccess);
        responseMessageUsingBodyStatements.Add(versionVar);
        
        // Используем CastExpressionNode вместо metadata
        var statusCodeExpr = new IdentifierExpressionNode(new IdentifierNode("statusCode"));
        var intType = new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true);
        var castExpr = new CastExpressionNode(
            targetType: intType,
            expression: statusCodeExpr);
        var intStatusCodeVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("statusCodeInt"),
            type: intType,
            initializer: castExpr);
        responseMessageUsingBodyStatements.Add(intStatusCodeVar);
        
        var isSuccess = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt")),
            @operator: BinaryOperator.GreaterThanOrEqual,
            right: new LiteralExpressionNode("200", LiteralKind.Number));
        var isSuccessAnd = new BinaryExpressionNode(
            left: isSuccess,
            @operator: BinaryOperator.LogicalAnd,
            right: new BinaryExpressionNode(
                left: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt")),
                @operator: BinaryOperator.LessThan,
                right: new LiteralExpressionNode("300", LiteralKind.Number)));
        
        // If statement для обработки success/error
        var ifStatements = new List<StatementNode>();
        var elseStatements = new List<StatementNode>();
        
        // Success branch: десериализуем в success type и помещаем в промежуточную переменную response
        if (successType != null)
        {
            var jsonSerializerExpr = new IdentifierExpressionNode(new IdentifierNode("JsonSerializer"));
            var deserializeAccess = new MemberAccessExpressionNode(
                @object: jsonSerializerExpr,
                member: new IdentifierNode("Deserialize"));
            var responseContentExpr = new IdentifierExpressionNode(new IdentifierNode("responseContent"));
            var deserializeCall = new CallExpressionNode(
                target: deserializeAccess,
                arguments: new ExpressionNode[] { responseContentExpr }.ToImmutableList(),
                genericTypeArgument: successType);
            
            // Десериализуем в nullable тип
            var nullableSuccessType = new NullableTypeNode(successType);
            
            // Помещаем десериализацию в промежуточную переменную response
            var responseVar = new VariableDeclarationStatementNode(
                name: new IdentifierNode("response"),
                type: nullableSuccessType,
                initializer: deserializeCall);
            ifStatements.Add(responseVar);
            
            // Проверяем response на null
            var responseExpr = new IdentifierExpressionNode(new IdentifierNode("response"));
            var nullCheck = new BinaryExpressionNode(
                left: responseExpr,
                @operator: BinaryOperator.Equal,
                right: new LiteralExpressionNode("null", LiteralKind.Null));
            
            var throwIfNullStatements = new List<StatementNode>();
            var apiExceptionForNullCtor = BuildApiExceptionCall(
                requestUri: new IdentifierExpressionNode(new IdentifierNode("requestUri")),
                httpMethod: new IdentifierExpressionNode(new IdentifierNode("httpMethod")),
                statusCode: new CastExpressionNode(
                    targetType: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
                    expression: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt"))),
                reasonPhrase: new IdentifierExpressionNode(new IdentifierNode("reasonPhrase")),
                version: new IdentifierExpressionNode(new IdentifierNode("version")),
                content: new IdentifierExpressionNode(new IdentifierNode("responseContent")),
                message: new LiteralExpressionNode("Response deserialization returned null", LiteralKind.String),
                innerException: null);
            throwIfNullStatements.Add(new ThrowStatementNode(apiExceptionForNullCtor));
            var throwIfNullBlock = new BlockNode(throwIfNullStatements.ToImmutableList());
            
            var ifNullCheck = new IfStatementNode(
                condition: nullCheck,
                thenBlock: throwIfNullBlock,
                elseBlock: null);
            ifStatements.Add(ifNullCheck);
            
            var returnStatement = new ReturnStatementNode(responseExpr);
            ifStatements.Add(returnStatement);
        }
        else
        {
            var nullReturn = new ReturnStatementNode(null);
            ifStatements.Add(nullReturn);
        }
        
        // Error branch: десериализуем в error type и выбрасываем исключение
        if (errorTypes.Count > 0)
        {
            // Используем первый error type (можно улучшить для множественных error types)
            var errorType = errorTypes[0].Type;
            var jsonSerializerExpr = new IdentifierExpressionNode(new IdentifierNode("JsonSerializer"));
            var deserializeAccess = new MemberAccessExpressionNode(
                @object: jsonSerializerExpr,
                member: new IdentifierNode("Deserialize"));
            var responseContentExpr = new IdentifierExpressionNode(new IdentifierNode("responseContent"));
            var deserializeErrorCall = new CallExpressionNode(
                target: deserializeAccess,
                arguments: new ExpressionNode[] { responseContentExpr }.ToImmutableList(),
                genericTypeArgument: errorType);
            
            // Десериализуем в nullable тип
            var nullableErrorType = new NullableTypeNode(errorType);
            
            var errorVar = new VariableDeclarationStatementNode(
                name: new IdentifierNode("error"),
                type: nullableErrorType,
                initializer: deserializeErrorCall);
            elseStatements.Add(errorVar);
            
            // Проверяем error на null
            var errorExpr = new IdentifierExpressionNode(new IdentifierNode("error"));
            var nullCheck = new BinaryExpressionNode(
                left: errorExpr,
                @operator: BinaryOperator.Equal,
                right: new LiteralExpressionNode("null", LiteralKind.Null));
            
            var throwIfNullStatements = new List<StatementNode>();
            var apiExceptionForNullCtor = BuildApiExceptionCall(
                requestUri: new IdentifierExpressionNode(new IdentifierNode("requestUri")),
                httpMethod: new IdentifierExpressionNode(new IdentifierNode("httpMethod")),
                statusCode: new CastExpressionNode(
                    targetType: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
                    expression: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt"))),
                reasonPhrase: new IdentifierExpressionNode(new IdentifierNode("reasonPhrase")),
                version: new IdentifierExpressionNode(new IdentifierNode("version")),
                content: new IdentifierExpressionNode(new IdentifierNode("responseContent")),
                message: new LiteralExpressionNode("Error deserialization returned null", LiteralKind.String),
                innerException: null);
            throwIfNullStatements.Add(new ThrowStatementNode(apiExceptionForNullCtor));
            var throwIfNullBlock = new BlockNode(throwIfNullStatements.ToImmutableList());
            
            var ifNullCheck = new IfStatementNode(
                condition: nullCheck,
                thenBlock: throwIfNullBlock,
                elseBlock: null);
            elseStatements.Add(ifNullCheck);
            
            // Выбрасываем исключение с данными об ошибке
            var errorMessage = new MemberAccessExpressionNode(
                @object: errorExpr,
                member: new IdentifierNode("ToString"));
            var errorMessageCall = new CallExpressionNode(
                target: errorMessage,
                arguments: ImmutableList<ExpressionNode>.Empty);
            var apiExceptionCtor = BuildApiExceptionCall(
                requestUri: new IdentifierExpressionNode(new IdentifierNode("requestUri")),
                httpMethod: new IdentifierExpressionNode(new IdentifierNode("httpMethod")),
                statusCode: new CastExpressionNode(
                    targetType: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
                    expression: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt"))),
                reasonPhrase: new IdentifierExpressionNode(new IdentifierNode("reasonPhrase")),
                version: new IdentifierExpressionNode(new IdentifierNode("version")),
                content: new IdentifierExpressionNode(new IdentifierNode("responseContent")),
                message: errorMessageCall,
                innerException: null);
            var throwStatement = new ThrowStatementNode(apiExceptionCtor);
            elseStatements.Add(throwStatement);
        }
        else
        {
            // Если нет error type, просто выбрасываем общее исключение
            var apiExceptionCtor = BuildApiExceptionCall(
                requestUri: new IdentifierExpressionNode(new IdentifierNode("requestUri")),
                httpMethod: new IdentifierExpressionNode(new IdentifierNode("httpMethod")),
                statusCode: new CastExpressionNode(
                    targetType: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
                    expression: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt"))),
                reasonPhrase: new IdentifierExpressionNode(new IdentifierNode("reasonPhrase")),
                version: new IdentifierExpressionNode(new IdentifierNode("version")),
                content: new IdentifierExpressionNode(new IdentifierNode("responseContent")),
                message: new LiteralExpressionNode("Unexpected status code", LiteralKind.String),
                innerException: null);
            var throwStatement = new ThrowStatementNode(apiExceptionCtor);
            elseStatements.Add(throwStatement);
        }
        
        var thenBlock = new BlockNode(ifStatements.ToImmutableList());
        var elseBlock = new BlockNode(elseStatements.ToImmutableList());
        var ifStatement = new IfStatementNode(
            condition: isSuccessAnd,
            thenBlock: thenBlock,
            elseBlock: elseBlock);
        responseMessageUsingBodyStatements.Add(ifStatement);
        
        // Создаем using statement для responseMessage
        var responseMessageUsingBlock = new BlockNode(responseMessageUsingBodyStatements.ToImmutableList());
        // Используем metadata для указания, что это using с переменной
        var responseMessageUsingResource = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("responseMessage")),
            arguments: ImmutableList<ExpressionNode>.Empty,
            isUsingVariable: true,
            variableDeclaration: responseMessageVar);
        var responseMessageUsing = new UsingStatementNode(
            resource: responseMessageUsingResource,
            body: responseMessageUsingBlock);
        
        // Добавляем using для responseMessage в requestMessageUsingBodyStatements (который уже содержит объявления requestUri и httpMethod)
        requestMessageUsingBodyStatements.Add(responseMessageUsing);
        
        // Создаем using statement для requestMessage, который содержит объявления переменных и using для responseMessage
        var requestMessageUsingBlock = new BlockNode(requestMessageUsingBodyStatements.ToImmutableList());
        var requestMessageUsingResource = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("requestMessage")),
            arguments: ImmutableList<ExpressionNode>.Empty,
            isUsingVariable: true,
            variableDeclaration: requestMessageVar);
        var requestMessageUsing = new UsingStatementNode(
            resource: requestMessageUsingResource,
            body: requestMessageUsingBlock);
        
        statements.Add(requestMessageUsing);
        
        return new BlockNode(statements.ToImmutableList());
    }
    
    /// <summary>
    /// Оценивает необходимую capacity для StringBuilder на основе длины URL и параметров
    /// </summary>
    private int EstimateStringBuilderCapacity(IrOperation operation)
    {
        var capacity = operation.Path.Length;
        
        // Добавляем место для path parameters
        foreach (var pathParam in operation.PathParameters)
        {
            var paramType = BuildType(pathParam.Type);
            if (paramType is SimpleTypeNode simple)
            {
                // Оценка длины для разных типов
                capacity += simple.Name.Name switch
                {
                    "int" => 8, // Максимальная длина int: -2147483648
                    "long" => 16, // Максимальная длина long: -9223372036854775808
                    "string" => 32, // Средняя оценка для строки
                    "bool" => 5, // "true" или "false"
                    "float" => 10,
                    "double" => 15,
                    "decimal" => 20,
                    _ => 16 // Дефолтная оценка
                };
            }
            else
            {
                capacity += 16; // Дефолтная оценка для сложных типов
            }
        }
        
        // Добавляем место для query parameters
        if (operation.QueryParameters.Count > 0)
        {
            capacity += 1; // "?"
            foreach (var queryParam in operation.QueryParameters)
            {
                capacity += queryParam.Name.Length + 1; // "name="
                var paramType = BuildType(queryParam.Type);
                if (paramType is SimpleTypeNode simple)
                {
                    capacity += simple.Name.Name switch
                    {
                        "int" => 8,
                        "long" => 16,
                        "string" => 32,
                        "bool" => 5,
                        "float" => 10,
                        "double" => 15,
                        "decimal" => 20,
                        _ => 16
                    };
                }
                else
                {
                    capacity += 16;
                }
                capacity += 1; // "&" между параметрами
            }
        }
        
        // Добавляем запас 20% для безопасности
        return (int)(capacity * 1.2);
    }
    
    private ExpressionNode BuildUrlExpression(IrOperation operation)
    {
        // Строим строку с интерполяцией для path parameters
        var route = operation.Path;
        var parts = new List<string>();
        var currentIndex = 0;
        
        // Парсим путь и заменяем {param} на интерполяцию
        while (currentIndex < route.Length)
        {
            var paramStart = route.IndexOf('{', currentIndex);
            if (paramStart == -1)
            {
                // Остаток пути без параметров
                if (currentIndex < route.Length)
                {
                    parts.Add(route.Substring(currentIndex));
                }
                break;
            }
            
            // Добавляем текст до параметра
            if (paramStart > currentIndex)
            {
                parts.Add(route.Substring(currentIndex, paramStart - currentIndex));
            }
            
            // Находим конец параметра
            var paramEnd = route.IndexOf('}', paramStart);
            if (paramEnd == -1)
            {
                // Некорректный формат, добавляем как есть
                parts.Add(route.Substring(paramStart));
                break;
            }
            
            // Извлекаем имя параметра
            var paramName = route.Substring(paramStart + 1, paramEnd - paramStart - 1);
            var sanitizedParamName = _namingConverter.SanitizeIdentifier(paramName);
            
            // Находим соответствующий path parameter
            var pathParam = operation.PathParameters.FirstOrDefault(p => p.Name == sanitizedParamName);
            if (pathParam != null)
            {
                // Добавляем интерполяцию: {paramName}
                parts.Add($"{{{sanitizedParamName}}}");
            }
            else
            {
                // Параметр не найден, добавляем как есть
                parts.Add(route.Substring(paramStart, paramEnd - paramStart + 1));
            }
            
            currentIndex = paramEnd + 1;
        }
        
        // Собираем строку с интерполяцией
        var interpolatedRoute = string.Join("", parts);
        
        // Если есть path parameters, создаем строку с интерполяцией
        // Используем простую конкатенацию строк для упрощения
        // В будущем можно создать специальный узел для интерполяции
        if (operation.PathParameters.Count > 0)
        {
            // Для упрощения используем конкатенацию строк
            // Например: "/api/users/" + userId + "/posts"
            var expressions = new List<ExpressionNode>();
            var currentPart = "";
            
            foreach (var part in parts)
            {
                if (part.StartsWith("{") && part.EndsWith("}"))
                {
                    // Это параметр
                    if (!string.IsNullOrEmpty(currentPart))
                    {
                        expressions.Add(new LiteralExpressionNode($"\"{currentPart}\"", LiteralKind.String));
                        currentPart = "";
                    }
                    var paramName = part.Substring(1, part.Length - 2);
                    var sanitizedParamName = _namingConverter.SanitizeIdentifier(paramName);
                    expressions.Add(new IdentifierExpressionNode(new IdentifierNode(sanitizedParamName)));
                }
                else
                {
                    currentPart += part;
                }
            }
            
            if (!string.IsNullOrEmpty(currentPart))
            {
                expressions.Add(new LiteralExpressionNode($"\"{currentPart}\"", LiteralKind.String));
            }
            
            // Если есть только один элемент, возвращаем его
            if (expressions.Count == 1)
            {
                return expressions[0];
            }
            
            // Создаем цепочку конкатенаций: expr1 + expr2 + expr3
            ExpressionNode result = expressions[0];
            for (int i = 1; i < expressions.Count; i++)
            {
                result = new BinaryExpressionNode(
                    left: result,
                    @operator: BinaryOperator.Add,
                    right: expressions[i]);
            }
            
            return result;
        }
        
        return new LiteralExpressionNode($"\"{route}\"", LiteralKind.String);
    }
    
    private string GetHttpClientMethodName(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => "GetAsync",
            "POST" => "PostAsync",
            "PUT" => "PutAsync",
            "DELETE" => "DeleteAsync",
            "PATCH" => "PatchAsync",
            _ => "SendAsync"
        };
    }
    
    /// <summary>
    /// Преобразует HTTP метод в имя статического свойства HttpMethod (Get, Post, Put и т.д.)
    /// </summary>
    private string GetHttpMethodPropertyName(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            "HEAD" => "Head",
            "OPTIONS" => "Options",
            "TRACE" => "Trace",
            _ => throw new NotSupportedException($"HTTP method {httpMethod} is not supported")
        };
    }
    
    /// <summary>
    /// Строит HTTP узлы из IrOperation (для использования в других местах)
    /// </summary>
    private (HttpMethodNode Method, RouteNode Route, RequestBodyNode? Body, IReadOnlyList<QueryParameterNode> QueryParams, IReadOnlyList<HeaderParameterNode> HeaderParams) BuildHttpNodes(IrOperation operation)
    {
        var method = _httpBuilder.BuildHttpMethod(operation.HttpMethod);
        var route = _httpBuilder.BuildRoute(operation.Path, operation.PathParameters, BuildType);
        
        var queryParams = operation.QueryParameters
            .Select(p => _httpBuilder.BuildQueryParameter(p, BuildType))
            .ToImmutableList();
        
        var headerParams = operation.HeaderParameters
            .Select(p => _httpBuilder.BuildHeaderParameter(p, BuildType))
            .ToImmutableList();
        
        var body = operation.RequestBody != null
            ? _httpBuilder.BuildRequestBody(operation.RequestBody, BuildType)
            : null;
        
        return (method, route, body, queryParams, headerParams);
    }
    
    private TypeNode ExtractReturnType(IReadOnlyList<IrResponse> responses)
    {
        // Ищем первый успешный ответ (200-299)
        var successResponse = responses.FirstOrDefault(r =>
        {
            if (int.TryParse(r.StatusCode, out var code))
                return code >= 200 && code < 300;
            return false;
        });
        
        if (successResponse?.Type != null)
        {
            // Для async методов оборачиваем тип в Task<T>
            var responseType = BuildType(successResponse.Type);
            return new GenericTypeNode(
                name: new IdentifierNode("Task"),
                typeArguments: new[] { responseType }.ToImmutableList(),
                isNullable: false);
        }
        
        // Если нет успешного ответа, возвращаем Task (для async методов без возвращаемого значения)
        return new SimpleTypeNode(
            name: new IdentifierNode("Task"),
            isPrimitive: false,
            isNullable: false,
            isOptional: false);
    }
    
    /// <summary>
    /// Строит InterfaceDeclarationNode из IrClient
    /// </summary>
    private InterfaceDeclarationNode BuildClientInterface(IrClient client)
    {
        var interfaceName = new IdentifierNode($"I{client.Name}");
        // IrClient не имеет Description, используем null
        CommentNode? comment = null;
        
        // Методы интерфейса (без тела, с документацией)
        var interfaceMethods = client.Operations
            .Select(BuildInterfaceMethod)
            .ToList<DeclarationNode>();
        
        return new InterfaceDeclarationNode(
            name: interfaceName,
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: interfaceMethods.ToImmutableList(),
            documentation: comment);
    }
    
    private ClassDeclarationNode BuildClientClass(IrClient client)
    {
        var name = new IdentifierNode(client.Name);
        var members = new List<DeclarationNode>();
        
        // Определяем базовые типы (интерфейсы)
        var baseTypes = new List<TypeNode>();
        if (_options.GenerateInterfaces)
        {
            var interfaceType = new SimpleTypeNode(new IdentifierNode($"I{client.Name}"), isPrimitive: false);
            baseTypes.Add(interfaceType);
        }
        
        // Добавляем поле _httpClient
        var httpClientField = new FieldDeclarationNode(
            name: new IdentifierNode("_httpClient"),
            type: new SimpleTypeNode(new IdentifierNode("HttpClient"), isPrimitive: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(httpClientField);
        
        // Добавляем конструктор
        var constructorParams = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("httpClient"),
                type: new SimpleTypeNode(new IdentifierNode("HttpClient"), isPrimitive: false),
                isRequired: true)
        };
        
        // _httpClient = httpClient;
        var assignmentExpr = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_httpClient")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("httpClient")));
        
        var constructorBody = new BlockNode(new List<StatementNode>
        {
            new ExpressionStatementNode(assignmentExpr)
        }.ToImmutableList());
        
        var constructor = new ConstructorDeclarationNode(
            name: name,
            parameters: constructorParams.ToImmutableList(),
            body: constructorBody,
            accessModifier: AccessModifier.Public);
        members.Add(constructor);
        
        // Добавляем методы (без документации, будет inheritdoc)
        var methods = client.Operations
            .Select(op =>
            {
                var method = BuildMethod(op, includeBody: true, includeDocumentation: false);
                
                // Создаем новый метод с UseInheritDoc = true
                return new MethodDeclarationNode(
                    name: method.Name,
                    returnType: method.ReturnType,
                    parameters: method.Parameters,
                    body: method.Body,
                    isAsync: method.IsAsync,
                    accessModifier: method.AccessModifier,
                    documentation: method.Documentation,
                    attributes: method.Attributes,
                    metadata: method.Metadata,
                    isInterfaceMethod: method.IsInterfaceMethod,
                    useInheritDoc: true);
            })
            .ToList<DeclarationNode>();
        
        members.AddRange(methods);
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: _options.ClientAccessModifier,
            isSealed: _options.UseSealed,
            isStatic: false,
            isPartial: false,
            baseTypes: baseTypes.ToImmutableList(),
            members: members.ToImmutableList());
    }
    
    /// <summary>
    /// Строит CompilationUnit для ApiException
    /// </summary>
    public CompilationUnitNode BuildCompilationUnitApiException(string? @namespace = null)
    {
        var usings = BuildUsingDirectives(new[] { "System", "System.Net", "System.Net.Http" });
        var classNode = BuildApiExceptionClass();
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { (DeclarationNode)classNode }.ToList());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит ClassDeclarationNode для ApiException
    /// </summary>
    private ClassDeclarationNode BuildApiExceptionClass()
    {
        var name = new IdentifierNode("ApiException");
        var comment = new CommentNode("Исключение, выбрасываемое при ошибках HTTP запросов", CommentKind.Xml);
        
        var members = new List<DeclarationNode>();
        
        // Свойства (get; set; для присваивания в конструкторе)
        var requestUriProperty = new PropertyDeclarationNode(
            name: new IdentifierNode("RequestUri"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Uri"), isPrimitive: false)),
            isRequired: false,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: null,
            documentation: new CommentNode("URI запроса", CommentKind.Xml));
        members.Add(requestUriProperty);
        
        var httpMethodProperty = new PropertyDeclarationNode(
            name: new IdentifierNode("HttpMethod"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("HttpMethod"), isPrimitive: false)),
            isRequired: false,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: null,
            documentation: new CommentNode("HTTP метод запроса", CommentKind.Xml));
        members.Add(httpMethodProperty);
        
        var statusCodeProperty = new PropertyDeclarationNode(
            name: new IdentifierNode("StatusCode"),
            type: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
            isRequired: true,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: null,
            documentation: new CommentNode("HTTP статус код ответа", CommentKind.Xml));
        members.Add(statusCodeProperty);
        
        var reasonPhraseProperty = new PropertyDeclarationNode(
            name: new IdentifierNode("ReasonPhrase"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)),
            isRequired: false,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: null,
            documentation: new CommentNode("Фраза причины статус кода", CommentKind.Xml));
        members.Add(reasonPhraseProperty);
        
        var versionProperty = new PropertyDeclarationNode(
            name: new IdentifierNode("Version"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Version"), isPrimitive: false)),
            isRequired: false,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: null,
            documentation: new CommentNode("Версия HTTP протокола", CommentKind.Xml));
        members.Add(versionProperty);
        
        var contentProperty = new PropertyDeclarationNode(
            name: new IdentifierNode("Content"),
            type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)),
            isRequired: false,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: null,
            documentation: new CommentNode("Содержимое ответа", CommentKind.Xml));
        members.Add(contentProperty);
        
        // Конструкторы
        // Конструктор с основными параметрами
        var constructor1Params = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("statusCode"),
                type: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("message"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                isRequired: false)
        };
        
        var constructor1Body = new BlockNode(new List<StatementNode>
        {
            new ExpressionStatementNode(new CallExpressionNode(
                target: new IdentifierExpressionNode(new IdentifierNode("base")),
                arguments: new ExpressionNode[] { new IdentifierExpressionNode(new IdentifierNode("message")) }.ToImmutableList(),
                isConstructorBaseCall: true)),
            new ExpressionStatementNode(new BinaryExpressionNode(
                left: new IdentifierExpressionNode(new IdentifierNode("StatusCode")),
                @operator: BinaryOperator.Assign,
                right: new IdentifierExpressionNode(new IdentifierNode("statusCode"))))
        }.ToImmutableList());
        
        var constructor1 = new ConstructorDeclarationNode(
            name: name,
            parameters: constructor1Params.ToImmutableList(),
            body: constructor1Body,
            accessModifier: AccessModifier.Public);
        members.Add(constructor1);
        
        // Конструктор с полным набором параметров
        var constructor2Params = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("requestUri"),
                type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Uri"), isPrimitive: false)),
                isRequired: false),
            new ParameterDeclarationNode(
                name: new IdentifierNode("httpMethod"),
                type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("HttpMethod"), isPrimitive: false)),
                isRequired: false),
            new ParameterDeclarationNode(
                name: new IdentifierNode("statusCode"),
                type: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("reasonPhrase"),
                type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)),
                isRequired: false),
            new ParameterDeclarationNode(
                name: new IdentifierNode("version"),
                type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Version"), isPrimitive: false)),
                isRequired: false),
            new ParameterDeclarationNode(
                name: new IdentifierNode("content"),
                type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)),
                isRequired: false),
            new ParameterDeclarationNode(
                name: new IdentifierNode("message"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                isRequired: false),
            new ParameterDeclarationNode(
                name: new IdentifierNode("innerException"),
                type: new NullableTypeNode(new SimpleTypeNode(new IdentifierNode("Exception"), isPrimitive: false)),
                isRequired: false)
        };
        
        var constructor2BodyStatements = new List<StatementNode>();
        constructor2BodyStatements.Add(new ExpressionStatementNode(new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("base")),
            arguments: new ExpressionNode[] { 
                new IdentifierExpressionNode(new IdentifierNode("message")),
                new IdentifierExpressionNode(new IdentifierNode("innerException"))
            }.ToImmutableList(),
            isConstructorBaseCall: true)));
        constructor2BodyStatements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("RequestUri")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("requestUri")))));
        constructor2BodyStatements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("HttpMethod")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("httpMethod")))));
        constructor2BodyStatements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("StatusCode")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("statusCode")))));
        constructor2BodyStatements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("ReasonPhrase")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("reasonPhrase")))));
        constructor2BodyStatements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("Version")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("version")))));
        constructor2BodyStatements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("Content")),
            @operator: BinaryOperator.Assign,
            right: new IdentifierExpressionNode(new IdentifierNode("content")))));
        
        var constructor2Body = new BlockNode(constructor2BodyStatements.ToImmutableList());
        
        var constructor2 = new ConstructorDeclarationNode(
            name: name,
            parameters: constructor2Params.ToImmutableList(),
            body: constructor2Body,
            accessModifier: AccessModifier.Public);
        members.Add(constructor2);
        
        var baseTypes = new[] { new SimpleTypeNode(new IdentifierNode("ApplicationException"), isPrimitive: false) }.ToImmutableList();
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            isSealed: false,
            isStatic: false,
            isPartial: false,
            baseTypes: baseTypes,
            members: members.ToImmutableList(),
            documentation: comment);
    }
    
    /// <summary>
    /// Создает вызов конструктора ApiException с полным набором параметров
    /// </summary>
    private CallExpressionNode BuildApiExceptionCall(
        ExpressionNode requestUri,
        ExpressionNode httpMethod,
        ExpressionNode statusCode,
        ExpressionNode reasonPhrase,
        ExpressionNode version,
        ExpressionNode content,
        ExpressionNode message,
        ExpressionNode? innerException)
    {
        var apiExceptionType = new IdentifierExpressionNode(new IdentifierNode("ApiException"));
        
        var arguments = new List<ExpressionNode>
        {
            requestUri,
            httpMethod,
            statusCode,
            reasonPhrase,
            version,
            content,
            message,
            innerException ?? new LiteralExpressionNode("null", LiteralKind.Null)
        };
        
        return new CallExpressionNode(
            target: apiExceptionType,
            arguments: arguments.ToImmutableList(),
            isNewExpression: true);
    }
    
    private IReadOnlyList<string> ExtractDependencies(IrClient client)
    {
        var dependencies = new HashSet<string>();
        
        // Добавляем только необходимые системные using для HTTP клиентов
        // Модели (IrModelReference) не добавляются, так как они генерируются в том же namespace
        dependencies.Add("System");
        dependencies.Add("System.Net"); // HttpStatusCode
        dependencies.Add("System.Net.Http"); // HttpClient, HttpRequestMessage, HttpResponseMessage
        dependencies.Add("System.Net.Http.Headers"); // MediaTypeWithQualityHeaderValue
        dependencies.Add("System.Text"); // StringBuilder, Encoding
        dependencies.Add("System.Text.Json"); // JsonSerializer
        dependencies.Add("System.Threading"); // CancellationToken
        dependencies.Add("System.Threading.Tasks"); // Task, Task<T>
        
        // Не извлекаем зависимости из типов параметров и response,
        // так как модели генерируются в том же namespace и using'и для них не нужны
        
        return dependencies.ToList();
    }
    
    /// <summary>
    /// Строит InterfaceDeclarationNode для интерфейса IHttpClientMetrics
    /// </summary>
    private InterfaceDeclarationNode BuildIHttpClientMetricsInterface()
    {
        var interfaceName = new IdentifierNode("IHttpClientMetrics");
        var members = new List<DeclarationNode>();
        
        // Метод Measure<T>
        var measureMethod = BuildMeasureMethodSignature();
        members.Add(measureMethod);
        
        return new InterfaceDeclarationNode(
            name: interfaceName,
            accessModifier: AccessModifier.Public,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: members.ToImmutableList());
    }
    
    /// <summary>
    /// Строит сигнатуру метода Measure<T> для интерфейса
    /// </summary>
    private MethodDeclarationNode BuildMeasureMethodSignature()
    {
        var methodName = new IdentifierNode("Measure");
        
        // Параметры метода
        var parameters = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("clientName"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("methodName"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("func"),
                type: new GenericTypeNode(
                    name: new IdentifierNode("Func"),
                    typeArguments: new[]
                    {
                        new GenericTypeNode(
                            name: new IdentifierNode("Task"),
                            typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("T"), isPrimitive: false) }.ToImmutableList(),
                            isNullable: false)
                    }.ToImmutableList(),
                    isNullable: false),
                isRequired: true)
        };
        
        return new MethodDeclarationNode(
            name: methodName,
            returnType: new GenericTypeNode(
                name: new IdentifierNode("Task"),
                typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("T"), isPrimitive: false) }.ToImmutableList(),
                isNullable: false),
            parameters: parameters.ToImmutableList(),
            body: null,
            isAsync: true,
            accessModifier: AccessModifier.Public,
            isInterfaceMethod: true,
            genericTypeParameters: new[] { "T" });
    }
    
    /// <summary>
    /// Получает название метрики с префиксом ServiceName
    /// </summary>
    private string GetMetricName(string metricName)
    {
        var serviceName = _options.ServiceName;
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return metricName;
        }
        return $"{serviceName}.{metricName}";
    }
    
    /// <summary>
    /// Строит ClassDeclarationNode для класса HttpClientMetrics
    /// </summary>
    private ClassDeclarationNode BuildHttpClientMetricsClass()
    {
        var className = new IdentifierNode("HttpClientMetrics");
        var members = new List<DeclarationNode>();
        
        // Поля для счетчиков
        var successCounterField = new FieldDeclarationNode(
            name: new IdentifierNode("_successCounter"),
            type: new GenericTypeNode(
                name: new IdentifierNode("Counter"),
                typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("long"), isPrimitive: true) }.ToImmutableList(),
                isNullable: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(successCounterField);
        
        var errorCounterField = new FieldDeclarationNode(
            name: new IdentifierNode("_errorCounter"),
            type: new GenericTypeNode(
                name: new IdentifierNode("Counter"),
                typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("long"), isPrimitive: true) }.ToImmutableList(),
                isNullable: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(errorCounterField);
        
        var durationHistogramField = new FieldDeclarationNode(
            name: new IdentifierNode("_durationHistogram"),
            type: new GenericTypeNode(
                name: new IdentifierNode("Histogram"),
                typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("double"), isPrimitive: true) }.ToImmutableList(),
                isNullable: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(durationHistogramField);
        
        // Конструктор
        var constructorParams = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("meter"),
                type: new SimpleTypeNode(new IdentifierNode("Meter"), isPrimitive: false),
                isRequired: true)
        };
        
        var constructorBodyStatements = new List<StatementNode>();
        
        // _successCounter = meter.CreateCounter<long>("{serviceName}.requests.success", "requests", "Total number of successful HTTP requests");
        var successMetricName = GetMetricName("requests.success");
        var successCounterCreate = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("meter")),
                member: new IdentifierNode("CreateCounter")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode(successMetricName, LiteralKind.String),
                new LiteralExpressionNode("requests", LiteralKind.String),
                new LiteralExpressionNode("Total number of successful HTTP requests", LiteralKind.String)
            }.ToImmutableList<ExpressionNode>(),
            genericTypeArgument: new SimpleTypeNode(new IdentifierNode("long"), isPrimitive: true));
        
        var successCounterAssignment = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_successCounter")),
            @operator: BinaryOperator.Assign,
            right: successCounterCreate);
        constructorBodyStatements.Add(new ExpressionStatementNode(successCounterAssignment));
        
        // _errorCounter = meter.CreateCounter<long>("{serviceName}.requests.error", "requests", "Total number of failed HTTP requests");
        var errorMetricName = GetMetricName("requests.error");
        var errorCounterCreate = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("meter")),
                member: new IdentifierNode("CreateCounter")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode(errorMetricName, LiteralKind.String),
                new LiteralExpressionNode("requests", LiteralKind.String),
                new LiteralExpressionNode("Total number of failed HTTP requests", LiteralKind.String)
            }.ToImmutableList<ExpressionNode>(),
            genericTypeArgument: new SimpleTypeNode(new IdentifierNode("long"), isPrimitive: true));
        
        var errorCounterAssignment = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_errorCounter")),
            @operator: BinaryOperator.Assign,
            right: errorCounterCreate);
        constructorBodyStatements.Add(new ExpressionStatementNode(errorCounterAssignment));
        
        // _durationHistogram = meter.CreateHistogram<double>("{serviceName}.request.duration", "seconds", "Duration of HTTP requests");
        var durationMetricName = GetMetricName("request.duration");
        var durationHistogramCreate = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("meter")),
                member: new IdentifierNode("CreateHistogram")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode(durationMetricName, LiteralKind.String),
                new LiteralExpressionNode("seconds", LiteralKind.String),
                new LiteralExpressionNode("Duration of HTTP requests", LiteralKind.String)
            }.ToImmutableList<ExpressionNode>(),
            genericTypeArgument: new SimpleTypeNode(new IdentifierNode("double"), isPrimitive: true));
        
        var durationHistogramAssignment = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_durationHistogram")),
            @operator: BinaryOperator.Assign,
            right: durationHistogramCreate);
        constructorBodyStatements.Add(new ExpressionStatementNode(durationHistogramAssignment));
        
        var constructorBody = new BlockNode(constructorBodyStatements.ToImmutableList());
        var constructor = new ConstructorDeclarationNode(
            name: className,
            parameters: constructorParams.ToImmutableList(),
            body: constructorBody,
            accessModifier: AccessModifier.Public);
        members.Add(constructor);
        
        // Метод Measure<T>
        var measureMethod = BuildMeasureMethod();
        members.Add(measureMethod);
        
        // Базовые типы (интерфейс)
        var baseTypes = new List<TypeNode>
        {
            new SimpleTypeNode(new IdentifierNode("IHttpClientMetrics"), isPrimitive: false)
        };
        
        return new ClassDeclarationNode(
            name: className,
            accessModifier: AccessModifier.Internal,
            isSealed: true,
            isStatic: false,
            isPartial: false,
            baseTypes: baseTypes.ToImmutableList(),
            members: members.ToImmutableList());
    }
    
    /// <summary>
    /// Строит метод Measure<T> для HttpClientMetrics
    /// </summary>
    private MethodDeclarationNode BuildMeasureMethod()
    {
        var methodName = new IdentifierNode("Measure");
        
        // Параметры метода
        var parameters = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("clientName"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("methodName"),
                type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
                isRequired: true),
            new ParameterDeclarationNode(
                name: new IdentifierNode("func"),
                type: new GenericTypeNode(
                    name: new IdentifierNode("Func"),
                    typeArguments: new[]
                    {
                        new GenericTypeNode(
                            name: new IdentifierNode("Task"),
                            typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("T"), isPrimitive: false) }.ToImmutableList(),
                            isNullable: false)
                    }.ToImmutableList(),
                    isNullable: false),
                isRequired: true)
        };
        
        // Тело метода
        var statements = new List<StatementNode>();
        
        // var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var stopwatchVar = new IdentifierNode("stopwatch");
        var stopwatchType = new SimpleTypeNode(new IdentifierNode("System.Diagnostics.Stopwatch"), isPrimitive: false);
        var stopwatchStartNew = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("System.Diagnostics.Stopwatch")),
                member: new IdentifierNode("StartNew")),
            arguments: ImmutableList<ExpressionNode>.Empty);
        var stopwatchDeclaration = new VariableDeclarationStatementNode(
            name: stopwatchVar,
            type: stopwatchType,
            initializer: stopwatchStartNew);
        statements.Add(stopwatchDeclaration);
        
        // var isSuccess = true;
        var isSuccessVar = new IdentifierNode("isSuccess");
        var isSuccessDeclaration = new VariableDeclarationStatementNode(
            name: isSuccessVar,
            type: new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
            initializer: new LiteralExpressionNode(true, LiteralKind.Boolean));
        statements.Add(isSuccessDeclaration);
        
        // try {
        var tryStatements = new List<StatementNode>();
        
        // var result = await func();
        var funcCall = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("func")),
            arguments: ImmutableList<ExpressionNode>.Empty);
        var awaitExpression = new AwaitExpressionNode(funcCall);
        var resultVar = new IdentifierNode("result");
        // Для generic параметра типа T используем SimpleTypeNode, а не GenericTypeNode
        var resultType = new SimpleTypeNode(
            name: new IdentifierNode("T"),
            isPrimitive: false);
        var resultDeclaration = new VariableDeclarationStatementNode(
            name: resultVar,
            type: resultType,
            initializer: awaitExpression);
        tryStatements.Add(resultDeclaration);
        
        // return result;
        tryStatements.Add(new ReturnStatementNode(new IdentifierExpressionNode(resultVar)));
        
        var tryBlock = new BlockNode(tryStatements.ToImmutableList());
        
        // catch (Exception) {
        var catchStatements = new List<StatementNode>();
        
        // isSuccess = false;
        var isSuccessAssignment = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(isSuccessVar),
            @operator: BinaryOperator.Assign,
            right: new LiteralExpressionNode(false, LiteralKind.Boolean));
        catchStatements.Add(new ExpressionStatementNode(isSuccessAssignment));
        
        // throw;
        catchStatements.Add(new ThrowStatementNode(null));
        
        var catchBlock = new BlockNode(catchStatements.ToImmutableList());
        var catchClause = new CatchClauseNode(
            body: catchBlock,
            exceptionType: new SimpleTypeNode(new IdentifierNode("Exception"), isPrimitive: false),
            exceptionVariable: null);
        
        // finally {
        var finallyStatements = new List<StatementNode>();
        
        // var duration = stopwatch.Elapsed.TotalSeconds;
        var durationVar = new IdentifierNode("duration");
        var durationType = new SimpleTypeNode(new IdentifierNode("double"), isPrimitive: true);
        var elapsedTotalSeconds = new MemberAccessExpressionNode(
            @object: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(stopwatchVar),
                member: new IdentifierNode("Elapsed")),
            member: new IdentifierNode("TotalSeconds"));
        var durationDeclaration = new VariableDeclarationStatementNode(
            name: durationVar,
            type: durationType,
            initializer: elapsedTotalSeconds);
        finallyStatements.Add(durationDeclaration);
        
        // Создаем теги для метрик
        var clientTag = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("KeyValuePair")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode("client", LiteralKind.String),
                new IdentifierExpressionNode(new IdentifierNode("clientName"))
            }.ToImmutableList<ExpressionNode>(),
            isNewExpression: true,
            genericTypeName: "KeyValuePair<string, object?>");
        
        var methodTag = new CallExpressionNode(
            target: new IdentifierExpressionNode(new IdentifierNode("KeyValuePair")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode("method", LiteralKind.String),
                new IdentifierExpressionNode(new IdentifierNode("methodName"))
            }.ToImmutableList<ExpressionNode>(),
            isNewExpression: true,
            genericTypeName: "KeyValuePair<string, object?>");
        
        // _durationHistogram.Record(duration, clientTag, methodTag);
        var recordCall = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("_durationHistogram")),
                member: new IdentifierNode("Record")),
            arguments: new ExpressionNode[]
            {
                new IdentifierExpressionNode(durationVar),
                clientTag,
                methodTag
            }.ToImmutableList<ExpressionNode>());
        finallyStatements.Add(new ExpressionStatementNode(recordCall));
        
        // Используем два отдельных счетчика вместо одного с тегами
        // if (isSuccess) { _successCounter.Add(1, clientTag, methodTag); } else { _errorCounter.Add(1, clientTag, methodTag); }
        var successCounterAddCall = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("_successCounter")),
                member: new IdentifierNode("Add")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode(1L, LiteralKind.Number),
                clientTag,
                methodTag
            }.ToImmutableList<ExpressionNode>());
        
        var errorCounterAddCall = new CallExpressionNode(
            target: new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("_errorCounter")),
                member: new IdentifierNode("Add")),
            arguments: new ExpressionNode[]
            {
                new LiteralExpressionNode(1L, LiteralKind.Number),
                clientTag,
                methodTag
            }.ToImmutableList<ExpressionNode>());
        
        var ifSuccessBlock = new BlockNode(new[] { new ExpressionStatementNode(successCounterAddCall) }.ToImmutableList());
        var ifErrorBlock = new BlockNode(new[] { new ExpressionStatementNode(errorCounterAddCall) }.ToImmutableList());
        
        var ifSuccessStatement = new IfStatementNode(
            condition: new IdentifierExpressionNode(isSuccessVar),
            thenBlock: ifSuccessBlock,
            elseBlock: ifErrorBlock);
        finallyStatements.Add(ifSuccessStatement);
        
        var finallyBlock = new BlockNode(finallyStatements.ToImmutableList());
        
        // Создаем try-catch-finally
        var tryCatchStatement = new TryCatchStatementNode(
            tryBlock: tryBlock,
            catchClauses: new[] { catchClause }.ToImmutableList(),
            finallyBlock: finallyBlock);
        statements.Add(tryCatchStatement);
        
        var methodBody = new BlockNode(statements.ToImmutableList());
        
        return new MethodDeclarationNode(
            name: methodName,
            returnType: new GenericTypeNode(
                name: new IdentifierNode("Task"),
                typeArguments: new[] { new SimpleTypeNode(new IdentifierNode("T"), isPrimitive: false) }.ToImmutableList(),
                isNullable: false),
            parameters: parameters.ToImmutableList(),
            body: methodBody,
            isAsync: true,
            accessModifier: AccessModifier.Public,
            genericTypeParameters: new[] { "T" });
    }
    
    /// <summary>
    /// Извлекает generic аргумент из типа возврата для использования в generic методах
    /// </summary>
    private TypeNode? ExtractReturnTypeGenericArgument(TypeNode? returnType)
    {
        if (returnType is GenericTypeNode genericType && genericType.Name.Name == "Task")
        {
            return genericType.TypeArguments.FirstOrDefault();
        }
        return returnType;
    }
}
