using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Axios.Models;

/// <summary>
/// Источники выражений для построения Axios config при использовании params interface.
/// </summary>
public sealed class AxiosConfigParamsSource : IEquatable<AxiosConfigParamsSource>
{
    /// <summary>Initializes the values carried by this model.</summary>
    /// <param name="RouteParamsSource">The source expression for route parameters.</param>
    /// <param name="QueryParamsSource">The source expression for query parameters.</param>
    /// <param name="BodySource">The source expression for the request body.</param>
    /// <param name="HeadersSource">The source expression for request headers.</param>
    public AxiosConfigParamsSource(
        ExpressionNode? RouteParamsSource,
        ExpressionNode? QueryParamsSource,
        ExpressionNode? BodySource,
        ExpressionNode? HeadersSource)
    {
        this.RouteParamsSource = RouteParamsSource;
        this.QueryParamsSource = QueryParamsSource;
        this.BodySource = BodySource;
        this.HeadersSource = HeadersSource;
    }

    /// <summary>Gets the source expression for route parameters.</summary>
    /// <value>The source expression for route parameters.</value>
    public ExpressionNode? RouteParamsSource { get; init; }

    /// <summary>Gets the source expression for query parameters.</summary>
    /// <value>The source expression for query parameters.</value>
    public ExpressionNode? QueryParamsSource { get; init; }

    /// <summary>Gets the source expression for the request body.</summary>
    /// <value>The source expression for the request body.</value>
    public ExpressionNode? BodySource { get; init; }

    /// <summary>Gets the source expression for request headers.</summary>
    /// <value>The source expression for request headers.</value>
    public ExpressionNode? HeadersSource { get; init; }

    /// <summary>Determines whether another value has the same stored members.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>True when all stored members are equal.</returns>
    public bool Equals(AxiosConfigParamsSource? other) => other is not null
        && EqualityComparer<ExpressionNode?>.Default.Equals(RouteParamsSource, other.RouteParamsSource)
        && EqualityComparer<ExpressionNode?>.Default.Equals(QueryParamsSource, other.QueryParamsSource)
        && EqualityComparer<ExpressionNode?>.Default.Equals(BodySource, other.BodySource)
        && EqualityComparer<ExpressionNode?>.Default.Equals(HeadersSource, other.HeadersSource);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AxiosConfigParamsSource other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(RouteParamsSource, QueryParamsSource, BodySource, HeadersSource);

    /// <summary>Compares two values for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when both are null or carry the same member values.</returns>
    public static bool operator ==(AxiosConfigParamsSource? left, AxiosConfigParamsSource? right) => Equals(left, right);

    /// <summary>Compares two values for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values differ.</returns>
    public static bool operator !=(AxiosConfigParamsSource? left, AxiosConfigParamsSource? right) => !Equals(left, right);
}
