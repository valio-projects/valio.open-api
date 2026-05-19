using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Generators.TypeScript.TanStackQuery.Ast.Builders;

namespace ValIO.Generators.TypeScript.TanStackQuery.Angular.Builders;

/// <summary>
/// Билдер хуков на основе <c>@tanstack/angular-query-experimental</c>.
/// Использует <c>injectQuery(() =&gt; ({ ... }))</c> / <c>injectMutation(() =&gt; ({ ... }))</c>
/// для signal-based reactivity внутри Angular DI-контекста.
/// </summary>
public sealed class TanStackAngularHookAstBuilder : TanStackFrameworkHookAstBuilderBase
{
    public TanStackAngularHookAstBuilder(
        INamingConverter namingConverter,
        Func<Core.IR.Types.IrType, TypeNode> typeBuilder,
        string clientSuffix)
        : base(namingConverter, typeBuilder, clientSuffix)
    {
    }

    protected override string PackageName => "@tanstack/angular-query-experimental";
    protected override string QueryFnName => "injectQuery";
    protected override string MutationFnName => "injectMutation";
    protected override bool WrapOptionsInGetter => true;
}
