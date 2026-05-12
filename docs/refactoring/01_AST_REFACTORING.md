# 01 — Рефакторинг AST: удаление AstNodeKind и унификация нод

> **Этап:** 0 (фундамент)  
> **Приоритет:** Критический  
> **Блокирует:** все последующие этапы

---

## Содержание

1. [Анализ проблемы AstNodeKind](#1-анализ-проблемы-astnodekind)
2. [Корень проблемы: сломанный Accept в абстрактных базовых нодах](#2-корень-проблемы-сломанный-accept-в-абстрактных-базовых-нодах)
3. [Целевая модель Visitor (чистый double-dispatch)](#3-целевая-модель-visitor-чистый-double-dispatch)
4. [Алгоритм удаления AstNodeKind](#4-алгоритм-удаления-astnodekind)
5. [Рефакторинг BaseAstVisitor](#5-рефакторинг-baseastvisitor)
6. [Рефакторинг AstNode и его наследников](#6-рефакторинг-astnode-и-его-наследников)
7. [Унификация дублирующихся AST-нод](#7-унификация-дублирующихся-ast-нод)
8. [Расширяемый рендерер (ExtensibleAstRenderer)](#8-расширяемый-рендерер-extensibleastrenderer)
9. [IAstRenderer и IHttpAstVisitor — удаление](#9-iastrenderer-и-ihttpaстvisitor--удаление)
10. [ToString() и отладка без Kind](#10-tostring-и-отладка-без-kind)
11. [Карта файлов и изменений](#11-карта-файлов-и-изменений)

---

## 1. Анализ проблемы AstNodeKind

### Что такое AstNodeKind сейчас

```csharp
// ValIO.Core/Ast/Enums/AstNodeKind.cs
public enum AstNodeKind
{
    // Базовые узлы
    Identifier, Comment, Attribute, Block, Import, Export,
    // Типы
    RawType, SimpleType, GenericType, NullableType, ...
    // Expressions
    LiteralExpression, IdentifierExpression, CallExpression, ...
    // Statements
    BlankLineStatement, ReturnStatement, IfStatement, ...
    // C# специфичные
    CompilationUnit, NamespaceDeclaration, ClassDeclaration, ...
    // TypeScript специфичные
    SourceFile, TypeAliasDeclaration, FunctionDeclaration, ...
    // HTTP специфичные
    HttpMethod, Route, PathParameter, ...
    // Zod специфичные
    ZodSchema, ZodObject, ZodArray, ...
    // Axios специфичные
    AxiosCallExpression, AxiosConfig,
    // TanStack Query специфичные
    UseQuery, UseMutation, QueryOptions, ...
    // C++ специфичные
    CppHeaderFile, CppSourceFile, ...
    // UE специфичные
    UStruct, UClass, UFunction, ...
}
```

**130+ значений в одном enum в Core** — знает о всех языках и всех стеках.

### Почему это проблема

1. **Нарушение OCP:** При добавлении нового стека (Ky, Valibot, Angular) нужно добавлять значения в `AstNodeKind` → редактировать Core.
2. **Нарушение SRP:** Один enum знает обо всём (C#, TypeScript, Zod, Axios, C++, UE).
3. **Бесполезность:** `Kind` используется только в switch-dispatch в `BaseAstVisitor`. Сам Visitor Pattern уже реализует диспетчеризацию через double-dispatch (`Accept`). Switch на Kind — это дублирование того, что делает система типов.
4. **ToString ломается:** `return $"{Kind}"` в `AstNode.ToString()` — единственная «полезная» функция Kind, легко заменяется `GetType().Name`.

### Где используется Kind (полный аудит)

| Файл | Использование |
|------|--------------|
| `BaseAstVisitor.cs` | `VisitDeclaration`, `VisitType`, `VisitExpression`, `VisitStatement` — switch dispatch |
| `AstNode.ToString()` | `return $"{Kind}"` |
| `AstNode.cs` | `public abstract AstNodeKind Kind { get; }` |
| Каждый concrete node | `public override AstNodeKind Kind => AstNodeKind.XXX;` |
| `CSharpAstRenderer.cs` | switch на Kind в Visit методах (аналогично) |

**Вывод:** `AstNodeKind` нужен исключительно для эмуляции virtual dispatch там, где он уже реализован нативно через `Accept`. Его можно и нужно удалить полностью.

---

## 2. Корень проблемы: сломанный Accept в абстрактных базовых нодах

> **Это самый важный раздел документа.** Без понимания этой проблемы рефакторинг будет неполным и сломает dispatch.

### Почему AstNodeKind вообще понадобился

На первый взгляд кажется, что достаточно просто убрать `AstNodeKind` и switch-switch в `BaseAstVisitor`. Но есть более глубокая причина, по которой switch на `Kind` вообще существует: **четыре абстрактных базовых ноды переопределяют `Accept` конкретными телами, которые вызывают dispatch-методы вместо того чтобы быть abstract.**

Это полностью ломает паттерн Visitor — double-dispatch не работает, и switch стал единственным способом добраться до нужного метода.

### Сломанный Accept — код как есть

```csharp
// ValIO.Core/Ast/Nodes/TypeNode.cs  — СЛОМАНО
public abstract class TypeNode : AstNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitType(this);   // ← вызывает dispatch-метод, НЕ конкретный Visit
}

// ValIO.Core/Ast/Nodes/StatementNode.cs  — СЛОМАНО
public abstract class StatementNode : AstNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitStatement(this);   // ← dispatch-метод
}

// ValIO.Core/Ast/Nodes/ExpressionNode.cs  — СЛОМАНО
public abstract class ExpressionNode : AstNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitExpression(this);  // ← dispatch-метод
}

// ValIO.Core/Ast/Nodes/DeclarationNode.cs  — СЛОМАНО
public abstract class DeclarationNode : AstNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitDeclaration(this); // ← dispatch-метод
}
```

### Что происходит при вызове

```csharp
// Вызываем Accept на конкретной ноде:
SimpleTypeNode node = new SimpleTypeNode("string");
string result = node.Accept(renderer);

// Шаг 1: SimpleTypeNode.Accept — НЕ переопределён,
//         поэтому вызывается TypeNode.Accept:
//         => visitor.VisitType(this);

// Шаг 2: VisitType в BaseAstVisitor смотрит на node.Kind:
//         return node.Kind switch {
//             AstNodeKind.SimpleType => VisitSimpleType((SimpleTypeNode)node),
//             ...
//         };

// Шаг 3: Только теперь вызывается нужный метод.
```

**Маршрут:** `SimpleTypeNode.Accept → TypeNode.Accept → visitor.VisitType → switch(Kind) → VisitSimpleType`

Вместо правильного маршрута: `SimpleTypeNode.Accept → visitor.VisitSimpleType`

Именно поэтому switch на `Kind` кажется «необходимым» — он компенсирует то, что `Accept` в конкретных нодах не переопределён.

### Почему конкретные ноды не переопределяют Accept

Потому что абстрактные базовые классы (`TypeNode`, `StatementNode`, etc.) уже дают **конкретную** реализацию `Accept`. Конкретные наследники (например, `SimpleTypeNode`) видят, что `Accept` уже реализован в базовом классе, и не переопределяют его. C# не вынуждает это делать — базовый класс не объявил Accept как `abstract`, поэтому наследники просто «используют» его реализацию.

Посмотрим на `SimpleTypeNode` — он реализован правильно, но работает неправильно именно из-за этой ловушки:

```csharp
// ValIO.Core/Ast/Nodes/Types/SimpleTypeNode.cs
public sealed class SimpleTypeNode : TypeNode
{
    public override AstNodeKind Kind => AstNodeKind.SimpleType;   // нужен только для switch
    
    // Accept НЕ переопределён! Используется TypeNode.Accept → visitor.VisitType(this)
    // Хотя казалось бы — нода sealed, она должна была вызывать visitor.VisitSimpleType(this)
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);  // это НАПИСАНО, но TypeNode.Accept перехватывает вызов выше
}
```

Подождите — `SimpleTypeNode` ПЕРЕОПРЕДЕЛЯЕТ `Accept`? Значит `TypeNode.Accept` не вызывается? Посмотрим внимательнее...

Фактически: если `SimpleTypeNode` явно переопределяет `Accept`, то double-dispatch **работает** для этой конкретной ноды. Проблема в том, что это переопределение может отсутствовать в других нодах, или компилятор не гарантирует что оно есть во всех.

### Правильное исправление: сделать Accept abstract в базовых нодах

Единственный способ гарантировать, что каждая конкретная нода реализует правильный dispatch — сделать `Accept` **abstract** в промежуточных базовых классах:

```csharp
// БЫЛО (TypeNode.cs):
public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    => visitor.VisitType(this);   // конкретная реализация — ОШИБКА

// СТАЛО (TypeNode.cs):
// Accept не переопределяется здесь вообще — остаётся abstract из AstNode
// Каждая конкретная нода ОБЯЗАНА переопределить Accept
```

```csharp
// БЫЛО (AstNode.cs):
public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);

// СТАЛО (AstNode.cs):
// Без изменений — Accept остаётся abstract.
// Но теперь TypeNode, StatementNode, ExpressionNode, DeclarationNode
// НЕ переопределяют Accept → компилятор заставляет конкретные ноды делать это.
```

```csharp
// СТАЛО (SimpleTypeNode.cs):
public sealed class SimpleTypeNode : TypeNode
{
    // Нет Kind — удалён
    public string Name { get; }
    
    // Accept ОБЯЗАТЕЛЕН компилятором, так как TypeNode не переопределяет его
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);  // ← правильный double-dispatch
    
    public override IReadOnlyList<AstNode> Children => [];
    public override AstNode Clone() => new SimpleTypeNode(Name, Metadata);
}
```

### ToString в базовых нодах — тоже исправить

`DeclarationNode.ToString()` использует `Kind`:

```csharp
// БЫЛО (DeclarationNode.cs):
public override string ToString()
{
    return $"{Kind}({Name.Name})";   // ← Kind удаляется
}

// СТАЛО:
public override string ToString()
{
    return $"{GetType().Name}({Name.Name})";  // → "TypeDeclarationNode(UserDto)"
}
```

`TypeNode.ToString()` тоже использует `Kind`:

```csharp
// БЫЛО (TypeNode.cs):
public override string ToString()
{
    var nullableStr = IsNullable ? "?" : "";
    var optionalStr = IsOptional ? "?" : "";
    return $"{Kind}{nullableStr}{optionalStr}";  // ← Kind удаляется
}

// СТАЛО:
public override string ToString()
{
    var nullableStr = IsNullable ? "?" : "";
    var optionalStr = IsOptional ? "?" : "";
    return $"{GetType().Name}{nullableStr}{optionalStr}";
}
```

### Полный список файлов с конкретными Accept-реализациями для исправления

Следующие файлы объявляют `Accept` в абстрактных промежуточных классах — реализации нужно **удалить** (или оставить `Accept` не переопределённым), чтобы компилятор обязал конкретные ноды сделать это:

| Файл | Проблема | Действие |
|------|----------|----------|
| `ValIO.Core/Ast/Nodes/TypeNode.cs` | `Accept → visitor.VisitType(this)` | Удалить override Accept |
| `ValIO.Core/Ast/Nodes/StatementNode.cs` | `Accept → visitor.VisitStatement(this)` | Удалить override Accept |
| `ValIO.Core/Ast/Nodes/ExpressionNode.cs` | `Accept → visitor.VisitExpression(this)` | Удалить override Accept |
| `ValIO.Core/Ast/Nodes/DeclarationNode.cs` | `Accept → visitor.VisitDeclaration(this)` | Удалить override Accept |

После удаления этих реализаций — все конкретные ноды, у которых нет явного `Accept`, **перестанут компилироваться**. Это и есть цель: компилятор сам найдёт все ноды, которым нужно добавить `Accept`.

### Алгоритм исправления (пошагово)

1. Открыть `TypeNode.cs`, `StatementNode.cs`, `ExpressionNode.cs`, `DeclarationNode.cs`
2. Удалить метод `Accept` из каждого (не трогать другие методы)
3. Запустить компиляцию — получить список ошибок «XXXNode не реализует abstract член AstNode.Accept»
4. Для каждой ноды из списка ошибок добавить правильный `Accept`:
   - Для нод из `Nodes/Types/`: `=> visitor.VisitXxx(this)`
   - Для нод из `Nodes/Expressions/`: `=> visitor.VisitXxxExpression(this)`
   - Для нод из `Nodes/Statements/`: `=> visitor.VisitXxxStatement(this)`
   - Для нод из `Nodes/Declarations/` в Core: `=> visitor.VisitXxxDeclaration(this)`
   - Для нод в `ValIO.Generators.CSharp/Ast/Nodes/`: `=> visitor.VisitXxx(this)` через ICSharpAstVisitor
   - Для нод в `ValIO.Generators.TypeScript/Ast/Nodes/`: `=> visitor.VisitXxx(this)` через ITypeScriptAstVisitor
5. Убедиться, что компиляция проходит без ошибок
6. **Только после этого** убирать `AstNodeKind` и switch из `BaseAstVisitor`

> **Важно:** шаг 6 нельзя делать до шага 5 — иначе switch перестанет работать и сломается весь dispatch.

---

## 3. Целевая модель Visitor (чистый double-dispatch)

### Принцип Visitor Pattern (GoF)

Double-dispatch означает: **нода сама знает**, какой метод visitor'а вызвать.

```csharp
// Нода определяет метод visitor'а — НЕ switch
public sealed class SimpleTypeNode : TypeNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);    // <── диспетчеризация через тип
}

public sealed class GenericTypeNode : TypeNode
{
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitGenericType(this);
}
```

Visitor не нужен switch — он просто реализует методы:
```csharp
public class TypeScriptAstRenderer : IBaseAstVisitor<string>
{
    public string VisitSimpleType(SimpleTypeNode node) => node.Name;
    public string VisitGenericType(GenericTypeNode node) => $"{node.Name}<{...}>";
    // нет никакого switch!
}
```

### Контракт IBaseAstVisitor после рефакторинга

```csharp
// ValIO.Core/Ast/Visitors/IBaseAstVisitor.cs
// УБИРАЕМ VisitDeclaration(DeclarationNode), VisitType(TypeNode), VisitExpression(ExpressionNode), VisitStatement(StatementNode)
// Эти методы были нужны только как точки входа для switch-dispatch.
// Теперь вызов идёт через node.Accept(visitor) напрямую.

public interface IBaseAstVisitor<TResult>
{
    // Базовые узлы
    TResult VisitIdentifier(IdentifierNode node);
    TResult VisitComment(CommentNode node);
    TResult VisitAttribute(AttributeNode node);
    TResult VisitBlock(BlockNode node);
    TResult VisitImport(ImportNode node);
    TResult VisitExport(ExportNode node);

    // Типы
    TResult VisitRawType(RawTypeNode node);
    TResult VisitSimpleType(SimpleTypeNode node);
    TResult VisitGenericType(GenericTypeNode node);
    TResult VisitNullableType(NullableTypeNode node);
    TResult VisitOptionalType(OptionalTypeNode node);
    TResult VisitArrayType(ArrayTypeNode node);
    TResult VisitUnionType(UnionTypeNode node);
    TResult VisitIntersectionType(IntersectionTypeNode node);
    TResult VisitDictionaryType(DictionaryTypeNode node);

    // Expressions
    TResult VisitLiteralExpression(LiteralExpressionNode node);
    TResult VisitIdentifierExpression(IdentifierExpressionNode node);
    TResult VisitCallExpression(CallExpressionNode node);
    TResult VisitMemberAccessExpression(MemberAccessExpressionNode node);
    TResult VisitUnaryExpression(UnaryExpressionNode node);
    TResult VisitBinaryExpression(BinaryExpressionNode node);
    TResult VisitConditionalExpression(ConditionalExpressionNode node);
    TResult VisitAwaitExpression(AwaitExpressionNode node);
    TResult VisitCastExpression(CastExpressionNode node);

    // Statements
    TResult VisitBlankLineStatement(BlankLineStatementNode node);
    TResult VisitReturnStatement(ReturnStatementNode node);
    TResult VisitIfStatement(IfStatementNode node);
    TResult VisitForEachStatement(ForEachStatementNode node);
    TResult VisitExpressionStatement(ExpressionStatementNode node);
    TResult VisitVariableDeclarationStatement(VariableDeclarationStatementNode node);
    TResult VisitThrowStatement(ThrowStatementNode node);
    TResult VisitUsingStatement(UsingStatementNode node);
    TResult VisitTryCatchStatement(TryCatchStatementNode node);
    TResult VisitCatchClause(CatchClauseNode node);

    // Declarations (общие)
    TResult VisitTypeDeclaration(TypeDeclarationNode node);
    TResult VisitPropertyDeclaration(PropertyDeclarationNode node);
    TResult VisitMethodDeclaration(MethodDeclarationNode node);
    TResult VisitParameterDeclaration(ParameterDeclarationNode node);
    TResult VisitEnumMemberDeclaration(EnumMemberDeclarationNode node);
    TResult VisitEnumDeclaration(EnumDeclarationNode node);
    TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
}
```

---

## 4. Алгоритм удаления AstNodeKind

### Шаг 1: Изменить `AstNode` — убрать `Kind`

```csharp
// БЫЛО:
public abstract class AstNode
{
    public abstract AstNodeKind Kind { get; }
    public abstract IReadOnlyList<AstNode> Children { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);
    public abstract AstNode Clone();

    public override string ToString() => $"{Kind}";  // УБИРАЕМ
}

// СТАЛО:
public abstract class AstNode
{
    public abstract IReadOnlyList<AstNode> Children { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }

    protected AstNode(IReadOnlyDictionary<string, object>? metadata = null)
    {
        Metadata = metadata ?? ImmutableDictionary<string, object>.Empty;
    }

    public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);
    public abstract AstNode Clone();

    // Отладочная информация через систему типов
    public override string ToString() => GetType().Name;
}
```

### Шаг 2: Убрать `Kind` из каждой concrete node

**До:**
```csharp
public sealed class SimpleTypeNode : TypeNode
{
    public override AstNodeKind Kind => AstNodeKind.SimpleType;   // УДАЛИТЬ
    
    public string Name { get; }
    
    public SimpleTypeNode(string name) { Name = name; }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);
    
    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();
    public override AstNode Clone() => new SimpleTypeNode(Name, Metadata);
}
```

**После:**
```csharp
public sealed class SimpleTypeNode : TypeNode
{
    // Kind — УДАЛЁН
    
    public string Name { get; }
    
    public SimpleTypeNode(string name, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata) { Name = name; }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitSimpleType(this);   // диспетчеризация — через Accept
    
    public override IReadOnlyList<AstNode> Children => [];
    public override AstNode Clone() => new SimpleTypeNode(Name, Metadata);
}
```

### Шаг 3: Рефакторинг BaseAstVisitor — убрать все switch

**До:**
```csharp
public virtual TResult VisitType(TypeNode node)
{
    return node.Kind switch
    {
        AstNodeKind.RawType       => VisitRawType((RawTypeNode)node),
        AstNodeKind.SimpleType    => VisitSimpleType((SimpleTypeNode)node),
        AstNodeKind.GenericType   => VisitGenericType((GenericTypeNode)node),
        // ...
        _ => VisitDefault(node)
    };
}
```

**После:** Метод `VisitType` вообще не нужен — каждая нода сама вызывает нужный метод через `Accept`. `BaseAstVisitor` превращается в чисто абстрактный класс с `abstract` методами или default-реализациями (throw + NotImplementedException для неиспользуемых нод в конкретном visitor'е).

```csharp
// ValIO.Core/Ast/Visitors/BaseAstVisitor.cs
public abstract class BaseAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // НЕТ VisitType, VisitExpression, VisitStatement, VisitDeclaration — не нужны

    // Базовые узлы
    public abstract TResult VisitIdentifier(IdentifierNode node);
    public abstract TResult VisitComment(CommentNode node);
    public abstract TResult VisitAttribute(AttributeNode node);
    public abstract TResult VisitBlock(BlockNode node);
    public abstract TResult VisitImport(ImportNode node);
    public abstract TResult VisitExport(ExportNode node);

    // Типы — abstract (каждый visitor обязан реализовать или унаследовать default)
    public abstract TResult VisitRawType(RawTypeNode node);
    public abstract TResult VisitSimpleType(SimpleTypeNode node);
    // ...

    // Protected virtual "default" для необязательных нод
    protected virtual TResult DefaultVisit(AstNode node)
        => throw new NotSupportedException(
            $"Visiting {node.GetType().Name} is not supported by {GetType().Name}");
}
```

### Шаг 4: Удалить файл `AstNodeKind.cs`

После того как все `Kind` свойства и все switch-использования убраны, файл `ValIO.Core/Ast/Enums/AstNodeKind.cs` удаляется.

### Шаг 5: Обновить `AstNodeCloner`

```csharp
// БЫЛО: использовало Kind switch для клонирования
// СТАЛО: использует паттерн Visitor

public class AstNodeClonerVisitor : BaseAstVisitor<AstNode>
{
    public override AstNode VisitSimpleType(SimpleTypeNode node) => node.Clone();
    public override AstNode VisitGenericType(GenericTypeNode node) 
        => new GenericTypeNode(
            node.Name,
            node.TypeArguments.Select(a => (TypeNode)a.Accept(this)).ToImmutableList(),
            node.Metadata);
    // ... и т.д.
}
```

---

## 5. Рефакторинг BaseAstVisitor

### Проблема текущего `BaseAstVisitor`

```csharp
// ПРОБЛЕМА: VisitDeclaration используется как диспетчер — это было нужно только потому что
// у нас был switch на Kind. После удаления Kind — метод не нужен.
public virtual TResult VisitDeclaration(DeclarationNode node)
{
    return node.Kind switch { ... };  // УДАЛЯЕТСЯ
}
```

### Целевой BaseAstVisitor

```csharp
// ValIO.Core/Ast/Visitors/BaseAstVisitor.cs

/// <summary>
/// Базовый abstract visitor с default реализациями через NotSupportedException.
/// Конкретные visitor'ы переопределяют только нужные методы.
/// Нет switch-dispatch — чистый double-dispatch через Accept.
/// </summary>
public abstract class BaseAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // ── Базовые узлы ──────────────────────────────────────────────────
    public virtual TResult VisitIdentifier(IdentifierNode node) => DefaultVisit(node);
    public virtual TResult VisitComment(CommentNode node)       => DefaultVisit(node);
    public virtual TResult VisitAttribute(AttributeNode node)   => DefaultVisit(node);
    public virtual TResult VisitBlock(BlockNode node)           => DefaultVisit(node);
    public virtual TResult VisitImport(ImportNode node)         => DefaultVisit(node);
    public virtual TResult VisitExport(ExportNode node)         => DefaultVisit(node);

    // ── Типы ──────────────────────────────────────────────────────────
    public virtual TResult VisitRawType(RawTypeNode node)               => DefaultVisit(node);
    public virtual TResult VisitSimpleType(SimpleTypeNode node)         => DefaultVisit(node);
    public virtual TResult VisitGenericType(GenericTypeNode node)       => DefaultVisit(node);
    public virtual TResult VisitNullableType(NullableTypeNode node)     => DefaultVisit(node);
    public virtual TResult VisitOptionalType(OptionalTypeNode node)     => DefaultVisit(node);
    public virtual TResult VisitArrayType(ArrayTypeNode node)           => DefaultVisit(node);
    public virtual TResult VisitUnionType(UnionTypeNode node)           => DefaultVisit(node);
    public virtual TResult VisitIntersectionType(IntersectionTypeNode node) => DefaultVisit(node);
    public virtual TResult VisitDictionaryType(DictionaryTypeNode node) => DefaultVisit(node);

    // ── Expressions ───────────────────────────────────────────────────
    public virtual TResult VisitLiteralExpression(LiteralExpressionNode node)               => DefaultVisit(node);
    public virtual TResult VisitIdentifierExpression(IdentifierExpressionNode node)         => DefaultVisit(node);
    public virtual TResult VisitCallExpression(CallExpressionNode node)                     => DefaultVisit(node);
    public virtual TResult VisitMemberAccessExpression(MemberAccessExpressionNode node)     => DefaultVisit(node);
    public virtual TResult VisitUnaryExpression(UnaryExpressionNode node)                   => DefaultVisit(node);
    public virtual TResult VisitBinaryExpression(BinaryExpressionNode node)                 => DefaultVisit(node);
    public virtual TResult VisitConditionalExpression(ConditionalExpressionNode node)       => DefaultVisit(node);
    public virtual TResult VisitAwaitExpression(AwaitExpressionNode node)                   => DefaultVisit(node);
    public virtual TResult VisitCastExpression(CastExpressionNode node)                     => DefaultVisit(node);

    // ── Statements ────────────────────────────────────────────────────
    public virtual TResult VisitBlankLineStatement(BlankLineStatementNode node)             => DefaultVisit(node);
    public virtual TResult VisitReturnStatement(ReturnStatementNode node)                   => DefaultVisit(node);
    public virtual TResult VisitIfStatement(IfStatementNode node)                           => DefaultVisit(node);
    public virtual TResult VisitForEachStatement(ForEachStatementNode node)                 => DefaultVisit(node);
    public virtual TResult VisitExpressionStatement(ExpressionStatementNode node)           => DefaultVisit(node);
    public virtual TResult VisitVariableDeclarationStatement(VariableDeclarationStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitThrowStatement(ThrowStatementNode node)                     => DefaultVisit(node);
    public virtual TResult VisitUsingStatement(UsingStatementNode node)                     => DefaultVisit(node);
    public virtual TResult VisitTryCatchStatement(TryCatchStatementNode node)               => DefaultVisit(node);
    public virtual TResult VisitCatchClause(CatchClauseNode node)                           => DefaultVisit(node);

    // ── Declarations ──────────────────────────────────────────────────
    public virtual TResult VisitTypeDeclaration(TypeDeclarationNode node)       => DefaultVisit(node);
    public virtual TResult VisitPropertyDeclaration(PropertyDeclarationNode node) => DefaultVisit(node);
    public virtual TResult VisitMethodDeclaration(MethodDeclarationNode node)   => DefaultVisit(node);
    public virtual TResult VisitParameterDeclaration(ParameterDeclarationNode node) => DefaultVisit(node);
    public virtual TResult VisitEnumMemberDeclaration(EnumMemberDeclarationNode node) => DefaultVisit(node);
    public virtual TResult VisitEnumDeclaration(EnumDeclarationNode node)       => DefaultVisit(node);
    public virtual TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node) => DefaultVisit(node);

    // ── Default ───────────────────────────────────────────────────────
    protected virtual TResult DefaultVisit(AstNode node)
        => throw new NotSupportedException(
            $"{GetType().Name} does not support visiting {node.GetType().Name}");
}
```

**Ключевое отличие:** Все методы теперь `virtual` (не `abstract`), с default-реализацией `=> DefaultVisit(node)`. Это позволяет конкретным visitor'ам переопределять только нужные методы — не обязательно все 30+.

---

## 6. Рефакторинг AstNode и его наследников

### Иерархия узлов (целевая, без Kind)

```
AstNode (abstract)
├── IdentifierNode
├── CommentNode
├── AttributeNode
├── BlockNode
├── ImportNode
├── ExportNode
│
├── TypeNode (abstract)
│   ├── RawTypeNode
│   ├── SimpleTypeNode
│   ├── GenericTypeNode
│   ├── NullableTypeNode
│   ├── OptionalTypeNode
│   ├── ArrayTypeNode
│   ├── UnionTypeNode
│   ├── IntersectionTypeNode
│   └── DictionaryTypeNode
│
├── ExpressionNode (abstract)
│   ├── LiteralExpressionNode
│   ├── IdentifierExpressionNode
│   ├── CallExpressionNode
│   ├── MemberAccessExpressionNode
│   ├── UnaryExpressionNode
│   ├── BinaryExpressionNode
│   ├── ConditionalExpressionNode
│   ├── AwaitExpressionNode
│   ├── CastExpressionNode
│   └── [TS] ArrayLiteralExpressionNode, ArrowFunctionExpressionNode,
│           ObjectLiteralExpressionNode, SpreadExpressionNode,
│           TemplateLiteralExpressionNode
│
├── StatementNode (abstract)
│   ├── BlankLineStatementNode
│   ├── ReturnStatementNode
│   ├── IfStatementNode
│   ├── ForEachStatementNode
│   ├── ExpressionStatementNode
│   ├── VariableDeclarationStatementNode
│   ├── ThrowStatementNode
│   ├── UsingStatementNode
│   └── TryCatchStatementNode (содержит CatchClauseNode)
│
└── DeclarationNode (abstract)
    ├── TypeDeclarationNode
    ├── PropertyDeclarationNode
    ├── MethodDeclarationNode
    ├── ParameterDeclarationNode
    ├── EnumMemberDeclarationNode
    ├── EnumDeclarationNode        ← общая для C# и TS
    ├── InterfaceDeclarationNode   ← общая для C# и TS
    │
    ├── [C#] CompilationUnitNode, NamespaceDeclarationNode, UsingDirectiveNode,
    │         ClassDeclarationNode, RecordDeclarationNode, StructDeclarationNode,
    │         FieldDeclarationNode, ConstructorDeclarationNode
    │
    ├── [TS] SourceFileNode, TypeAliasDeclarationNode, FunctionDeclarationNode,
    │         VariableDeclarationNode, GenericParameterNode,
    │         PropertySignatureNode, PropertyAssignmentNode
    │
    └── [HTTP] HttpMethodNode, RouteNode, PathParameterNode,
               QueryParameterNode, HeaderParameterNode,
               RequestBodyNode, ResponseTypeNode
```

### Алгоритм каждой ноды

Каждая конкретная нода (пример — `CallExpressionNode`):

**Поля:** только данные, immutable (init-only или readonly)  
**Конструктор:** принимает все поля + optional metadata  
**Children:** возвращает все дочерние `AstNode` в порядке обхода  
**Accept:** вызывает строго один метод visitor'а (нет switch, нет if)  
**Clone:** возвращает новый экземпляр с теми же данными (Prototype)  
**ToString:** `GetType().Name` (наследуется от `AstNode`)  

```csharp
// Пример: CallExpressionNode
public sealed class CallExpressionNode : ExpressionNode
{
    // Данные узла
    public ExpressionNode Callee { get; }
    public IReadOnlyList<ExpressionNode> Arguments { get; }
    public IReadOnlyList<TypeNode>? TypeArguments { get; }

    public CallExpressionNode(
        ExpressionNode callee,
        IReadOnlyList<ExpressionNode> arguments,
        IReadOnlyList<TypeNode>? typeArguments = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Callee = callee;
        Arguments = arguments;
        TypeArguments = typeArguments;
    }

    // Double-dispatch — нет switch!
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
        => visitor.VisitCallExpression(this);

    // Дочерние узлы для обхода дерева
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Callee };
            children.AddRange(Arguments);
            if (TypeArguments != null) children.AddRange(TypeArguments);
            return children;
        }
    }

    // Prototype Pattern
    public override AstNode Clone()
        => new CallExpressionNode(
            (ExpressionNode)Callee.Clone(),
            Arguments.Select(a => (ExpressionNode)a.Clone()).ToImmutableList(),
            TypeArguments?.Select(t => (TypeNode)t.Clone()).ToImmutableList(),
            Metadata);
}
```

---

## 7. Унификация дублирующихся AST-нод

### Проблема: дублирование между языками

Текущие дубли, которые можно унифицировать
---

## 8. Расширяемый рендерер (ExtensibleAstRenderer)

### Архитектура

Цель: **один рендерер на язык**, расширяемый стек-специфичными обработчиками через паттерн **Chain of Responsibility + Strategy**.

```csharp
// ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs

public class TypeScriptAstRenderer : BaseAstVisitor<string>
{
    private readonly ImmutableArray<ITypeScriptNodeRenderer> _extensions;
    private readonly StringBuilderPool _pool;

    public TypeScriptAstRenderer(
        IEnumerable<ITypeScriptNodeRenderer>? extensions = null)
    {
        _extensions = extensions?.ToImmutableArray() ?? ImmutableArray<ITypeScriptNodeRenderer>.Empty;
        _pool = new StringBuilderPool();
    }

    // Точка входа — принимает любой AstNode
    public string Render(AstNode node) => node.Accept(this);

    // Все стандартные TS ноды — без switch, чистый visitor
    public override string VisitSimpleType(SimpleTypeNode node) => node.Name;
    public override string VisitArrayType(ArrayTypeNode node) => $"{node.ElementType.Accept(this)}[]";
    public override string VisitUnionType(UnionTypeNode node)
        => string.Join(" | ", node.Types.Select(t => t.Accept(this)));
    public override string VisitNullableType(NullableTypeNode node)
        => $"{node.InnerType.Accept(this)} | null";
    public override string VisitOptionalType(OptionalTypeNode node)
        => node.InnerType.Accept(this);   // optional в TS выражается через ?, не через тип
    // ... все остальные TS методы

    // Неизвестные ноды → Chain of Responsibility через extensions
    protected override string DefaultVisit(AstNode node)
    {
        foreach (var ext in _extensions)
        {
            if (ext.CanRender(node))
                return ext.Render(node, this);
        }
        throw new NotSupportedException(
            $"{GetType().Name}: no renderer registered for {node.GetType().Name}");
    }
}
```

### Контракт расширения

```csharp
// ValIO.Generators.TypeScript/Ast/Renderers/ITypeScriptNodeRenderer.cs

/// <summary>
/// Расширение рендерера для стек-специфичных нод.
/// Реализуется каждым плагином (Zod, Axios, TanStack, Ky, Valibot и т.д.)
/// </summary>
public interface ITypeScriptNodeRenderer
{
    /// <summary>
    /// Может ли этот рендерер обработать данный тип ноды
    /// </summary>
    bool CanRender(AstNode node);

    /// <summary>
    /// Рендерит ноду, может делегировать в baseRenderer для вложенных базовых нод
    /// </summary>
    string Render(AstNode node, TypeScriptAstRenderer baseRenderer);
}
```

### Пример: ZodNodeRenderer

```csharp
// ValIO.Generators.TypeScript/Zod/Ast/Renderers/ZodNodeRenderer.cs

public sealed class ZodNodeRenderer : ITypeScriptNodeRenderer
{
    public bool CanRender(AstNode node) => node is ZodSchemaNode;

    public string Render(AstNode node, TypeScriptAstRenderer baseRenderer)
        => node switch
        {
            ZodObjectNode obj   => RenderObject(obj, baseRenderer),
            ZodArrayNode arr    => $"z.array({baseRenderer.Render(arr.ElementSchema)})",
            ZodUnionNode union  => $"z.union([{string.Join(", ", union.Schemas.Select(s => baseRenderer.Render(s)))}])",
            ZodStringNode       => "z.string()",
            ZodNumberNode       => "z.number()",
            ZodBooleanNode      => "z.boolean()",
            ZodNullableNode n   => $"{baseRenderer.Render(n.InnerSchema)}.nullable()",
            ZodOptionalNode o   => $"{baseRenderer.Render(o.InnerSchema)}.optional()",
            _ => throw new NotSupportedException($"ZodNodeRenderer: unknown node {node.GetType().Name}")
        };

    private string RenderObject(ZodObjectNode node, TypeScriptAstRenderer baseRenderer)
    {
        var props = node.Properties
            .Select(p => $"  {p.Name}: {baseRenderer.Render(p.Schema)}")
            .ToList();
        return $"z.object({{\n{string.Join(",\n", props)}\n}})";
    }
}
```

### Пример: AxiosNodeRenderer

```csharp
// ValIO.Generators.TypeScript/Axios/Ast/Renderers/AxiosNodeRenderer.cs

public sealed class AxiosNodeRenderer : ITypeScriptNodeRenderer
{
    public bool CanRender(AstNode node) => node is AxiosCallNode;

    public string Render(AstNode node, TypeScriptAstRenderer baseRenderer)
        => node switch
        {
            AxiosGetNode get     => RenderAxiosCall("get", get, baseRenderer),
            AxiosPostNode post   => RenderAxiosCall("post", post, baseRenderer),
            AxiosPutNode put     => RenderAxiosCall("put", put, baseRenderer),
            AxiosDeleteNode del  => RenderAxiosCall("delete", del, baseRenderer),
            AxiosPatchNode patch => RenderAxiosCall("patch", patch, baseRenderer),
            _ => throw new NotSupportedException(node.GetType().Name)
        };

    private string RenderAxiosCall(string method, AxiosCallNode node, TypeScriptAstRenderer r)
        => $"axios.{method}<{r.Render(node.ResponseType)}>({r.Render(node.UrlExpr)}{(node.Data != null ? $", {r.Render(node.Data)}" : "")}{(node.Config != null ? $", {r.Render(node.Config)}" : "")})";
}
```

### Регистрация в DI (плагинная)

```csharp
// Каждый плагин регистрирует свой ITypeScriptNodeRenderer
// ValIO.Generators.TypeScript.Zod/ServiceRegistration.cs
public static class ZodServiceRegistration
{
    public static IServiceCollection AddZodGenerator(this IServiceCollection services)
    {
        services.AddSingleton<ITypeScriptNodeRenderer, ZodNodeRenderer>();
        services.AddSingleton<ICodeGenerationStep, ZodSchemasStep>();
        return services;
    }
}

// ValIO.Generators.TypeScript.Axios/ServiceRegistration.cs
public static class AxiosServiceRegistration
{
    public static IServiceCollection AddAxiosGenerator(this IServiceCollection services)
    {
        services.AddSingleton<ITypeScriptNodeRenderer, AxiosNodeRenderer>();
        services.AddSingleton<ICodeGenerationStep, AxiosClientStep>();
        return services;
    }
}
```

---

## 9. IAstRenderer и IHttpAstVisitor — удаление

### IAstRenderer

```csharp
// ValIO.Core/Ast/Visitors/IAstRenderer.cs — УДАЛИТЬ ПОЛНОСТЬЮ

public interface IAstRenderer : IAstVisitor<string>
{
    // Пустой маркерный интерфейс — никакой ценности не добавляет.
    // IAstVisitor<string> — уже достаточный контракт.
    // TypeScriptAstRenderer, CSharpAstRenderer, CppAstRenderer не реализуют IAstRenderer напрямую.
}
```

**Почему удалить:**
- Это пустой маркерный интерфейс — не добавляет ни методов, ни семантики
- Все конкретные рендереры типизируются как `BaseAstVisitor<string>` или `IBaseAstVisitor<string>`
- После рефакторинга рендереры не реализуют `IAstVisitor<TResult>` напрямую (т.к. IAstVisitor содержит dispatch-методы VisitType/VisitStatement/etc., которые будут удалены)
- Правильный контракт: конкретный интерфейс языкового рендерера (`ITypeScriptAstRenderer`, `ICSharpAstRenderer`)

**Действие:** Удалить файл `ValIO.Core/Ast/Visitors/IAstRenderer.cs`. Проверить, что нигде не используется (скорее всего не используется в актуальном коде).

### ICodeRenderer

```csharp
// ValIO.Core/Interfaces/ICodeRenderer.cs — УДАЛИТЬ

public interface ICodeRenderer
{
    GenerationLanguage Language { get; }
    string RenderModel(IModelTemplateData data);   // TemplateData — legacy
    string RenderEnum(EnumTemplateData data);       // TemplateData — legacy
    string RenderClient(IClientTemplateData data);  // TemplateData — legacy
    string RenderOperation(IOperationTemplateData data); // TemplateData — legacy
}
```

**Почему удалить:** Весь интерфейс завязан на старую TemplateData-систему. В новой архитектуре рендеринг идёт через `AstNode.Accept(visitor)`, а не через `ICodeRenderer.RenderModel(templateData)`. Удаляется вместе со всей TemplateData-системой (см. `06_LEGACY_CLEANUP.md`).

### IHttpAstVisitor и HttpAstVisitorBase

```csharp
// ValIO.Core/Ast/Visitors/IHttpAstVisitor.cs
public interface IHttpAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    TResult VisitHttpMethod(HttpMethodNode node);
    TResult VisitRoute(RouteNode node);
    TResult VisitPathParameter(PathParameterNode node);
    TResult VisitQueryParameter(QueryParameterNode node);
    TResult VisitHeaderParameter(HeaderParameterNode node);
    TResult VisitRequestBody(RequestBodyNode node);
    TResult VisitResponseType(ResponseTypeNode node);
}

// ValIO.Core/Ast/Visitors/HttpAstVisitorBase.cs
public abstract class HttpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IHttpAstVisitor<TResult>
{
    // Реализует IHttpAstVisitor + делегирует неиспользуемые методы в VisitDefault
}
```

**Анализ:**

HTTP-методы visitor'а (`VisitHttpMethod`, `VisitRoute` и т.д.) уже продублированы в `IAstVisitor<TResult>`. Это значит, что один и тот же контракт существует в двух местах — `IHttpAstVisitor` и `IAstVisitor`. Это нарушение DRY.

**Решение:**

Вариант A — оставить `IHttpAstVisitor` как отдельный интерфейс, удалить его из `IAstVisitor`:
```csharp
// IAstVisitor больше не содержит HTTP методы
// HTTP рендереры (AxiosAstRenderer и т.д.) реализуют IHttpAstVisitor напрямую

public class AxiosAstRenderer : BaseAstVisitor<string>, IHttpAstVisitor<string>
{
    // Реализует HTTP методы через IHttpAstVisitor
    public string VisitHttpMethod(HttpMethodNode node) { ... }
    // ...
}
```

Вариант B — полностью убрать `IHttpAstVisitor`, вставить HTTP методы в `IBaseAstVisitor` как опциональные:
```csharp
// В BaseAstVisitor добавить virtual методы для HTTP нод с DefaultVisit
public virtual string VisitHttpMethod(HttpMethodNode node) => DefaultVisit(node);
// и т.д. — visitor обрабатывает их только если нужно
```

**Рекомендуемое решение (Вариант A):**

1. Убрать HTTP методы из `IAstVisitor<TResult>` — они там лишние
2. Оставить `IHttpAstVisitor<TResult>` как отдельный интерфейс
3. Убрать `HttpAstVisitorBase` — он нужен только потому что `BaseAstVisitor` объявляет HTTP методы как abstract (через `IAstVisitor`). После удаления HTTP методов из `IAstVisitor` `HttpAstVisitorBase` не нужен.
4. Конкретные HTTP рендереры (`AxiosAstRenderer`, `FetchAstRenderer`) реализуют `BaseAstVisitor<string>` + `IHttpAstVisitor<string>` и добавляют HTTP методы сами.

**Обновлённый `IAstVisitor` после рефакторинга:**

```csharp
// IAstVisitor объединяет только общие узлы, без HTTP и без dispatch-методов
public interface IAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // Общие для C# и TS: Enum и Interface
    TResult VisitEnumDeclaration(EnumDeclarationNode node);
    TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    // HTTP методы — УБРАНЫ отсюда, перенесены в IHttpAstVisitor
}
```

---

## 10. ToString() и отладка без Kind

### Текущий AstNode.ToString()
```csharp
public override string ToString() => $"{Kind}";  // → "SimpleType"
```

### Целевой AstNode.ToString()
```csharp
public override string ToString() => GetType().Name;  // → "SimpleTypeNode"
```

Для отладки достаточно имени CLR-типа. Если нужно более детальное представление — override в конкретных нодах:

```csharp
public sealed class SimpleTypeNode : TypeNode
{
    public string Name { get; }
    public override string ToString() => $"SimpleType({Name})";
}

public sealed class CallExpressionNode : ExpressionNode
{
    public ExpressionNode Callee { get; }
    public override string ToString() => $"Call({Callee})";
}

public sealed class MethodDeclarationNode : DeclarationNode
{
    // DeclarationNode уже переопределяет ToString() через GetType().Name + Name
}
```

**Правило:** Нода переопределяет `ToString()` только если имеет ключевое «отладочное» поле (имя, значение, тип). Иначе достаточно `GetType().Name`.

---

## 11. Карта файлов и изменений

### Файлы к удалению

| Файл | Причина |
|------|---------|
| `ValIO.Core/Ast/Enums/AstNodeKind.cs` | 130+ значений, полностью заменяется системой типов |
| `ValIO.Core/Ast/Visitors/IAstRenderer.cs` | Пустой маркерный интерфейс, не несёт ценности |
| `ValIO.Core/Interfaces/ICodeRenderer.cs` | Завязан на TemplateData, полностью легаси |
| `ValIO.Generators.TypeScript.Zod/Ast/Visitors/ZodAstVisitorBase.cs` | Весь проект — легаси-остаток, заменяется composition |

### Файлы к изменению

| Файл | Изменение |
|------|-----------|
| `ValIO.Core/Ast/Nodes/AstNode.cs` | Убрать `Kind`, исправить `ToString()` |
| `ValIO.Core/Ast/Nodes/TypeNode.cs` | **Удалить override Accept** → конкретные ноды обязаны реализовать; исправить ToString |
| `ValIO.Core/Ast/Nodes/StatementNode.cs` | **Удалить override Accept** |
| `ValIO.Core/Ast/Nodes/ExpressionNode.cs` | **Удалить override Accept** |
| `ValIO.Core/Ast/Nodes/DeclarationNode.cs` | **Удалить override Accept**; исправить ToString |
| `ValIO.Core/Ast/Visitors/IBaseAstVisitor.cs` | Убрать `VisitDeclaration`, `VisitType`, `VisitExpression`, `VisitStatement` |
| `ValIO.Core/Ast/Visitors/IAstVisitor.cs` | Убрать dispatch-методы; убрать HTTP методы (перенести в IHttpAstVisitor) |
| `ValIO.Core/Ast/Visitors/BaseAstVisitor.cs` | Убрать все switch-методы; все Visit → `virtual` с DefaultVisit |
| `ValIO.Core/Ast/Visitors/HttpAstVisitorBase.cs` | Удалить после удаления HTTP методов из IAstVisitor |
| Все `*Node.cs` конкретные | Убрать `public override AstNodeKind Kind => ...`; добавить `Accept` если отсутствует |
| `ValIO.Core/Ast/Utilities/AstNodeCloner.cs` | Переписать через Visitor |
| `ValIO.Generators.TypeScript/Ast/Renderers/TypeScriptAstRenderer.cs` | Добавить `ITypeScriptNodeRenderer` Chain of Responsibility |
| `ValIO.Generators.TypeScript/Axios/Ast/Renderers/AxiosAstRenderer.cs` | Заменить наследование на composition + IHttpAstVisitor |
| `ValIO.Generators.TypeScript/Zod/Ast/Renderers/ZodAstRenderer.cs` | Переписать как `ITypeScriptNodeRenderer` |
| `ValIO.Generators.TypeScript/TanStackQuery/Ast/Renderers/TanStackQueryAstRenderer.cs` | Переписать как `ITypeScriptNodeRenderer` |

### Файлы к созданию

| Файл | Назначение |
|------|-----------|
| `ValIO.Generators.TypeScript/Ast/Renderers/ITypeScriptNodeRenderer.cs` | Контракт Chain of Responsibility расширений |
| `ValIO.Generators.TypeScript/Zod/Ast/Renderers/ZodNodeRenderer.cs` | Zod-специфичный renderer как расширение |
| `ValIO.Generators.TypeScript/Axios/Ast/Renderers/AxiosNodeRenderer.cs` | Axios-специфичный renderer |
| `ValIO.Generators.TypeScript/TanStackQuery/Ast/Renderers/TanStackNodeRenderer.cs` | TanStack-специфичный renderer |
| `ValIO.Generators.TypeScript/Fetch/Ast/Renderers/FetchNodeRenderer.cs` | Fetch-специфичный renderer |

### Порядок выполнения

1. **Шаг 1 — Fix Accept в базовых нодах (ОБЯЗАТЕЛЕН ПЕРВЫМ):**
   - Удалить `Accept` из `TypeNode`, `StatementNode`, `ExpressionNode`, `DeclarationNode`
   - Компилировать → получить список сломанных нод → добавить `Accept` в каждую
   - Убедиться что компиляция проходит

2. **Шаг 2 — Убрать switch из BaseAstVisitor:**
   - Удалить `VisitType`, `VisitStatement`, `VisitExpression`, `VisitDeclaration` из `BaseAstVisitor`
   - Сделать все Visit методы `virtual` с `DefaultVisit` вместо `abstract`
   - Убедиться что компиляция проходит

3. **Шаг 3 — Убрать Kind из нод:**
   - Удалить `public override AstNodeKind Kind => ...` из каждой конкретной ноды
   - Удалить `public abstract AstNodeKind Kind { get; }` из `AstNode`
   - Обновить `ToString()` в `AstNode`, `TypeNode`, `DeclarationNode`

4. **Шаг 4 — Удалить AstNodeKind.cs:**
   - Убрать файл и все `using ValIO.Core.Ast.Enums`

5. **Шаг 5 — Рефакторинг IAstVisitor:**
   - Убрать dispatch-методы из `IAstVisitor`
   - Убрать HTTP методы из `IAstVisitor` (они уже в `IHttpAstVisitor`)
   - Удалить `IAstRenderer.cs`
   - Удалить `ICodeRenderer.cs`

6. **Шаг 6 — Рефакторинг рендереров:**
   - Добавить `ITypeScriptNodeRenderer` интерфейс
   - Переписать `ZodAstRenderer`, `AxiosAstRenderer`, `TanStackQueryAstRenderer` как расширения
   - Обновить `TypeScriptAstRenderer` для Chain of Responsibility

7. **Шаг 7 — HttpAstVisitorBase:**
   - Если HTTP методы больше не в `IAstVisitor`, `HttpAstVisitorBase` можно удалить или упростить

8. **Шаг 8 — AstNodeCloner:**
   - Переписать через Visitor без switch

---

## Итог

После рефакторинга:
- ❌ `AstNodeKind` — удалён (130+ значений, нарушающих OCP)
- ❌ `IAstRenderer` — удалён (пустой маркер)
- ❌ `ICodeRenderer` — удалён (TemplateData legacy)
- ❌ `HttpAstVisitorBase` — удалён (не нужен после исправления IAstVisitor)
- ✅ **Критический баг исправлен:** TypeNode/StatementNode/ExpressionNode/DeclarationNode не переопределяют Accept
- ✅ Чистый Visitor (double-dispatch через `Accept` в каждой конкретной ноде)
- ✅ `BaseAstVisitor` без switch — только `virtual` методы с DefaultVisit
- ✅ Один `TypeScriptAstRenderer` для всех TS стеков
- ✅ Стек-специфичные ноды обрабатываются через `ITypeScriptNodeRenderer` (Chain of Responsibility)
- ✅ Добавление нового стека = новый `ITypeScriptNodeRenderer` без изменения Core
