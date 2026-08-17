namespace CineSched.Tests.Unit;

public sealed class ArchitectureTests
{
    [Fact]
    public void Core_DoesNotReferenceUiFrameworksOrUseHandlers()
    {
        var assembly = typeof(ProjectService).Assembly;
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToList();
        var types = assembly.GetTypes();

        Assert.DoesNotContain(references, name => name?.StartsWith("Uno", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(references, name => name?.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(types, type => type.Name.EndsWith("Handler", StringComparison.Ordinal));
        Assert.DoesNotContain(types, type => type.Namespace?.Contains(".Ports", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(types, type => type.Namespace?.Contains(".Adapters", StringComparison.Ordinal) == true);
    }
}
