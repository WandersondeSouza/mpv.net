using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Xunit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ArchitectureTests
{
    static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(MpvNet.AppClass).Assembly)
        .Build();

    [Fact]
    public void NativeCodeDoesNotDependOnApplicationHelpers()
    {
        var nativeTypes = Types().That().ResideInNamespace("MpvNet.Native");
        var applicationHelperTypes = Types().That().ResideInNamespace("MpvNet.Help");

        ArchUnitNET.Fluent.IArchRule rule = nativeTypes.Should().NotDependOnAny(applicationHelperTypes)
            .Because("native interop must remain independent from application helpers");
        rule.Check(Architecture);
    }

    [Fact]
    public void ExtensionCodeRemainsOutsideTheCoreAssembly()
    {
        var extensionTypes = Types().That().ResideInNamespace("MpvNet.Extensions");
        var coreIntegrationTypes = Types().That().ResideInNamespace("MpvNet.Integration.Mpv");

        ArchUnitNET.Fluent.IArchRule rule = extensionTypes.Should().NotDependOnAny(coreIntegrationTypes)
            .Because("extension helpers must not depend on the player integration layer");
        rule.Check(Architecture);
    }
}
