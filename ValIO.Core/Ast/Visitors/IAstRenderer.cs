namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Интерфейс рендерера AST (Visitor, возвращающий строку)
/// </summary>
public interface IAstRenderer : IAstVisitor<string>
{
}
