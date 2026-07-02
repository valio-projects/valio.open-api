using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// A mutable, per-group view of an operation's <c>params</c> argument as AST expressions, used by the transport
/// client builders (fetch/axios/ky/got/ofetch/undici/angular/nextjs) when assembling the request.
/// </summary>
/// <remarks>
/// <para>
/// This centralizes the group-source expressions (<c>params.route</c>, <c>params.query</c>, <c>params.headers</c>,
/// <c>params.body</c>/<c>params.form</c> — or the flat <c>params</c> for route/query/headers when they are the sole
/// group) plus the "may be undefined" flags that drive optional chaining. It is intentionally <b>mutable</b>:
/// after emitting runtime validation the transports rebind a source to its validated local
/// (e.g. <c>sources.RouteSource = Id("parsedRoute")</c>) so subsequent URL / query / body building references the
/// parsed value. The initial shape is computed from <see cref="TypeScriptParameterModel"/> so it can never drift
/// from the emitted params interface.
/// </para>
/// <para>
/// The request body is always reached via <c>params.body</c> / <c>params.form</c> even in flat mode
/// (see <see cref="TypeScriptParameterModel.BodyObject"/>); route/query/header groups flatten to top-level
/// <c>params</c> when they are the only group.
/// </para>
/// </remarks>
/// <seealso cref="TypeScriptParameterModel"/>
/// <seealso cref="TypeScriptRequestPath"/>
public sealed class TypeScriptParameterSources
{
    /// <summary>The route object source (<c>params.route</c> / <c>params</c>), or <see langword="null"/> when absent.</summary>
    public ExpressionNode? RouteSource { get; set; }

    /// <summary>The query object source (<c>params.query</c> / <c>params</c>), or <see langword="null"/> when absent.</summary>
    public ExpressionNode? QuerySource { get; set; }

    /// <summary>Whether <see cref="QuerySource"/> may be <c>undefined</c> at runtime (drives optional chaining).</summary>
    public bool QuerySourceMayBeUndefined { get; set; }

    /// <summary>The headers object source (<c>params.headers</c> / <c>params</c>), or <see langword="null"/> when absent.</summary>
    public ExpressionNode? HeadersSource { get; set; }

    /// <summary>Whether <see cref="HeadersSource"/> may be <c>undefined</c> at runtime (drives optional chaining).</summary>
    public bool HeadersSourceMayBeUndefined { get; set; }

    /// <summary>The body/form object source (<c>params.body</c> / <c>params.form</c>), or <see langword="null"/> when absent.</summary>
    public ExpressionNode? BodySource { get; set; }

    /// <summary>Whether <see cref="BodySource"/> may be <c>undefined</c> at runtime (optional request body).</summary>
    public bool BodySourceMayBeUndefined { get; set; }

    /// <summary>
    /// Computes the initial parameter sources for <paramref name="operation"/> from a
    /// <see cref="TypeScriptParameterModel"/>, matching the emitted params interface exactly.
    /// </summary>
    /// <param name="operation">The IR operation.</param>
    /// <returns>The mutable per-group source expressions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    public static TypeScriptParameterSources Create(IrOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var model = TypeScriptParameterModel.Create(operation);
        return new TypeScriptParameterSources
        {
            RouteSource = model.RouteObject(),
            QuerySource = model.QueryObject(),
            QuerySourceMayBeUndefined = model.QueryMayBeUndefined,
            HeadersSource = model.HeadersObject(),
            HeadersSourceMayBeUndefined = model.HeadersMayBeUndefined,
            BodySource = model.BodyObject(),
            BodySourceMayBeUndefined = model.BodyMayBeUndefined,
        };
    }
}
