
namespace MpvNet;

/// <summary>
/// Represents a named textual value used by configuration and mpv option lists.
/// </summary>
public class NamedValue
{
    public string Name { get; set; }
    public string Value { get; set; }

    public NamedValue(string name, string value)
    {
        Name = name;
        Value = value;
    }
}

/// <summary>
/// Compatibility model retained for existing configuration, extension and
/// command-line contracts. New APIs should prefer <see cref="NamedValue"/>.
/// </summary>
public class StringPair : NamedValue
{
    public StringPair(string name, string value) : base(name, value)
    {
    }
}
