using ValIO.Generators.Php.Ast.Nodes;

namespace ValIO.Generators.Php.Ast.Renderers;

public interface IPhpAstRenderer
{
    string Render(PhpSourceFileNode node);
}
