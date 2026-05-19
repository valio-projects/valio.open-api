using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

namespace ValIO.Generators.TypeScript.TanStackQuery.Solid.Builders;

/// <summary>
/// Билдер хуков на основе <c>@tanstack/solid-query</c>.
/// Использует <c>createQuery(() =&gt; ({ ... }))</c> / <c>createMutation(() =&gt; ({ ... }))</c>;
/// геттер-обёртка нужна для интеграции с Solid reactivity (сигналы).
/// </summary>
public sealed class TanStackSolidHookAstBuilder : TanStackFrameworkHookAstBuilderBase
{
    public TanStackSolidHookAstBuilder(
        INamingConverter namingConverter,
        Func<Core.IR.Types.IrType, TypeNode> typeBuilder,
        string clientSuffix)
        : base(namingConverter, typeBuilder, clientSuffix)
    {
    }

    protected override string PackageName => "@tanstack/solid-query";
    protected override string QueryFnName => "createQuery";
    protected override string MutationFnName => "createMutation";
    protected override bool WrapOptionsInGetter => true;
}
