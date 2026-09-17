using System.CodeDom.Compiler;
using Microsoft.Maui.Controls;
using Microsoft.Maui.DevFlow.Agent.Core.SourceMapping;

namespace Microsoft.Maui.DevFlow.Tests;

// Reload replaces entries in the process-wide source-map registry, so it shares the registry's collection.
[Collection("XamlSourceMapRegistry")]
public class XamlReloadTests : IDisposable
{
    const string ClassName = "Microsoft.Maui.DevFlow.Tests.ReloadableTestView";

    public XamlReloadTests() => XamlSourceMapRegistry.Instance.Reset();

    public void Dispose() => XamlSourceMapRegistry.Instance.Reset();

    static string Xaml(string title, string extraChild = "") => $$"""
        <?xml version="1.0" encoding="utf-8" ?>
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                     x:Class="{{ClassName}}">
            <ContentView.Resources>
                <Color x:Key="Accent">#FF0000</Color>
            </ContentView.Resources>
            <VerticalStackLayout>
                <Label x:Name="Title" Text="{{title}}" />
                {{extraChild}}
            </VerticalStackLayout>
        </ContentView>
        """;

    [Fact]
    public async Task ReloadXaml_ReinflatesEveryLiveInstance_AndRepointsNamedFields()
    {
        var first = new ReloadableTestView { AutomationId = "reload-first" };
        var second = new ReloadableTestView { AutomationId = "reload-second" };
        using var harness = await DesignEditingTestHarness.CreateAsync(first, second);

        var result = await harness.Client.ReloadXamlAsync(Xaml("Version 2", "<Button Text=\"New\" />"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(ClassName, result.ClassName);
        Assert.Equal(2, result.Reloaded);
        foreach (var view in new[] { first, second })
        {
            var stack = Assert.IsType<VerticalStackLayout>(view.Content);
            Assert.Equal(2, stack.Children.Count);
            Assert.Same(stack.Children[0], view.Title);
            Assert.Equal("Version 2", view.Title!.Text);
        }
    }

    [Fact]
    public async Task ReloadXaml_CanRunRepeatedly_WithoutResourceOrNameCollisions()
    {
        var view = new ReloadableTestView();
        using var harness = await DesignEditingTestHarness.CreateAsync(view);

        Assert.True((await harness.Client.ReloadXamlAsync(Xaml("One"))).Success);
        var second = await harness.Client.ReloadXamlAsync(Xaml("Two"));

        Assert.True(second.Success, second.Error);
        Assert.Equal("Two", view.Title!.Text);
        Assert.Single(view.Resources);
    }

    [Fact]
    public async Task ReloadXaml_TargetsSingleElement_WhenElementIdGiven()
    {
        var target = new ReloadableTestView { AutomationId = "reload-target" };
        var other = new ReloadableTestView { AutomationId = "reload-other" };
        using var harness = await DesignEditingTestHarness.CreateAsync(target, other);

        var result = await harness.Client.ReloadXamlAsync(Xaml("Only me"), elementId: await harness.GetElementIdAsync("reload-target"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Reloaded);
        Assert.Equal("Only me", target.Title!.Text);
        Assert.Null(other.Content);
    }

    [Fact]
    public async Task ReloadXaml_ElementOfDifferentClass_IsRejected()
    {
        var label = new Label { AutomationId = "not-reloadable" };
        using var harness = await DesignEditingTestHarness.CreateAsync(label);

        var result = await harness.Client.ReloadXamlAsync(Xaml("x"), elementId: await harness.GetElementIdAsync("not-reloadable"));

        Assert.False(result.Success);
        Assert.Equal("class-mismatch", result.Reason);
    }

    [Theory]
    [InlineData("<ContentView xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"><Label></ContentView>")]
    [InlineData("<ContentView xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"><Label /></ContentView>")]
    public async Task ReloadXaml_InvalidOrClasslessDocument_Returns400(string xaml)
    {
        using var harness = await DesignEditingTestHarness.CreateAsync(new ReloadableTestView());

        var result = await harness.Client.ReloadXamlAsync(xaml);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("invalid-xaml", result.Reason);
    }

    [Fact]
    public async Task ReloadXaml_ParseErrorFromInflation_ReportsPosition()
    {
        var view = new ReloadableTestView();
        using var harness = await DesignEditingTestHarness.CreateAsync(view);

        var result = await harness.Client.ReloadXamlAsync(Xaml("x", "<NotARealControl />"));

        Assert.False(result.Success);
        Assert.Equal("invalid-xaml", result.Reason);
        Assert.NotNull(result.Details?.Line);
    }

    [Fact]
    public async Task ReloadXaml_RefreshesSourceMap_SoLocationsFollowTheNewText()
    {
        var view = new ReloadableTestView { AutomationId = "mapped-view" };
        using var harness = await DesignEditingTestHarness.CreateAsync(view);

        // two lines of padding push the Label down compared with the original document
        var xaml = Xaml("Mapped").Replace("<VerticalStackLayout>", "<VerticalStackLayout>\n\n", StringComparison.Ordinal);
        var result = await harness.Client.ReloadXamlAsync(xaml, sourceFile: "Views/ReloadableTestView.xaml");

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrEmpty(result.SourceHash));

        var map = XamlSourceMapRegistry.Instance.GetMap(ClassName);
        Assert.NotNull(map);
        Assert.Equal("Views/ReloadableTestView.xaml", map!.File);
        Assert.Equal(result.SourceHash, map.ContentHash);

        var tree = await harness.Client.GetTreeAsync();
        var title = Flatten(tree).First(e => e.Type == "Label");
        var expectedLine = xaml.Split('\n').Select((text, i) => (text, line: i + 1)).First(l => l.text.Contains("x:Name=\"Title\"")).line;
        Assert.Equal("Views/ReloadableTestView.xaml", title.SourceFile);
        Assert.Equal(expectedLine, title.SourceLine);
    }

    static IEnumerable<Driver.ElementInfo> Flatten(IEnumerable<Driver.ElementInfo> elements)
        => elements.SelectMany(e => new[] { e }.Concat(Flatten(e.Children ?? [])));
}

public class ReloadableTestView : ContentView
{
    // Stands in for a XAML-generated x:Name field; the reload assigns it through reflection.
#pragma warning disable CS0649
    [GeneratedCode("Microsoft.Maui.Controls.SourceGen", "1.0.0.0")]
    internal Label? Title;
#pragma warning restore CS0649
}
