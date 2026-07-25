namespace LocalPlay.Models;

public sealed record NetworkAdapterOption(
    string Id,
    string Name,
    string IPv4Address,
    bool HasGateway,
    string Kind,
    bool IsAutomatic = false)
{
    public override string ToString() => IsAutomatic
        ? "Automatisch (empfohlen)"
        : $"{Name} · {IPv4Address}";
}
