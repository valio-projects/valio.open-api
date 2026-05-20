using ValIO.Generators.Python.Ast.Nodes;

namespace ValIO.Generators.Python.Ast.Renderers;

/// <summary>
/// Контракт рендерера Python AST → исходный текст.
/// </summary>
public interface IPythonAstRenderer
{
    /// <summary>Рендерит модуль (один .py файл).</summary>
    string Render(PythonModuleNode module);
}
