namespace ValIO.Output.Abstractions;

/// <summary>
/// Creates an output sink for a given target. Returned sink must be disposed (preferably via <c>await using</c>).
/// </summary>
public interface IOutputSinkFactory
{
    IGenerationOutputSink Create(OutputTarget target);
}
