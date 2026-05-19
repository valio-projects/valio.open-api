using ValIO.Generators.Java.Ast.Nodes;

namespace ValIO.Generators.Java.Ast.Renderers;

public interface IJavaAstRenderer
{
    string Render(JavaSourceFileNode node);
}
