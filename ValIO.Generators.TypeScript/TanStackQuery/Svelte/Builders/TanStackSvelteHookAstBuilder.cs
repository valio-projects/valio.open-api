using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

namespace ValIO.Generators.TypeScript.TanStackQuery.Svelte.Builders;

/// <summary>
/// Билдер хуков на основе <c>@tanstack/svelte-query</c> (Svelte 5 + runes).
/// Использует геттер-обёртку <c>createQuery(() =&gt; ({ ... }))</c> (Accessor pattern в v6+).
/// </summary>
public sealed class TanStackSvelteHookAstBuilder : TanStackFrameworkHookAstBuilderBase
{
    public TanStackSvelteHookAstBuilder(
        INamingConverter namingConverter,
        Func<Core.IR.Types.IrType, TypeNode> typeBuilder,
        string clientSuffix)
        : base(namingConverter, typeBuilder, clientSuffix)
    {
    }

    protected override string PackageName => "@tanstack/svelte-query";
    protected override string QueryFnName => "createQuery";
    protected override string MutationFnName => "createMutation";
    protected override bool WrapOptionsInGetter => true;
}
