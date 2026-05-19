namespace ValIO.Generators.CSharp.Options;

public sealed class CSharpGenerationOptions
{
    public string Namespace { get; set; } = "Generated";
    public bool NullableReferenceTypes { get; set; } = false;
}
