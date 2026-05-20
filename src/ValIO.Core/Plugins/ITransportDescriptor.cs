namespace ValIO.Core.Plugins;

public interface ITransportDescriptor
{
    string Id { get; }
    string DisplayName { get; }
    string? Description { get; }
}
