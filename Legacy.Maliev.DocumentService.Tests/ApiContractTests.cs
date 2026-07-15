using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using Maliev.Aspire.ServiceDefaults.Authorization;

namespace Legacy.Maliev.DocumentService.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public void Api_PreservesFiveAuthenticatedLegacyPdfRoutes()
    {
        var assembly = Assembly.Load("Legacy.Maliev.DocumentService.Api");
        var controller = assembly.GetTypes().Single(type => type.Name == "PdfsController");

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("[controller]", controller.GetCustomAttribute<RouteAttribute>()?.Template);

        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => new
            {
                Method = method,
                Route = method.GetCustomAttributes<HttpMethodAttribute>().Single(),
            })
            .ToArray();

        Assert.Equal(5, actions.Length);
        Assert.Equal(
            new[] { "invoice", "orderlabel", "purchaseorder", "quotation", "receipt" },
            actions.Select(action => action.Route.Template).Order(StringComparer.Ordinal).ToArray());
        Assert.All(actions, action => Assert.Equal(new[] { "POST" }, action.Route.HttpMethods));
        Assert.All(actions, action => Assert.Contains(
            action.Method.GetCustomAttributes(),
            attribute => attribute.GetType().Name == "RequirePermissionAttribute"));
        Assert.All(actions, action => Assert.False(
            Assert.Single(action.Method.GetCustomAttributes<RequirePermissionAttribute>()).RequireLiveCheck));
    }

    [Fact]
    public void ProductionProjects_ContainNeitherITextNorPayPal()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}.dependencies{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}Legacy.Maliev.DocumentService.Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        var source = string.Join('\n', files.Select(File.ReadAllText));

        Assert.DoesNotContain("iText", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PayPal", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QuestPDF", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRendering_EmbedsNotoSansFamiliesAndContainsNoSarabun()
    {
        var root = FindRepositoryRoot();
        var rendering = Path.Combine(root, "Legacy.Maliev.DocumentService.Rendering");
        var source = string.Join('\n', Directory.EnumerateFiles(rendering, "*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

        Assert.Contains("Noto Sans", source, StringComparison.Ordinal);
        Assert.Contains("Noto Sans Thai", source, StringComparison.Ordinal);
        Assert.Contains("FontFamily(LatinFont, ThaiFont)", source, StringComparison.Ordinal);
        Assert.Contains("FontFamily(LatinBoldFont, ThaiBoldFont)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sarabun", source, StringComparison.OrdinalIgnoreCase);

        foreach (var font in new[]
        {
            "NotoSans-Regular.ttf",
            "NotoSans-Bold.ttf",
            "NotoSansThai-Regular.ttf",
            "NotoSansThai-Bold.ttf",
        })
        {
            Assert.True(File.Exists(Path.Combine(rendering, "Resources", "Fonts", font)), $"Embedded font is missing: {font}");
        }
    }

    [Fact]
    public void ContainerRestore_CopiesSharedBuildPropertiesBeforeProjectEvaluation()
    {
        var dockerfile = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Legacy.Maliev.DocumentService.Api",
            "Dockerfile"));
        var propertiesCopy = dockerfile.IndexOf("COPY Directory.Build.props .", StringComparison.Ordinal);
        var restore = dockerfile.IndexOf("RUN dotnet restore", StringComparison.Ordinal);

        Assert.True(propertiesCopy >= 0, "The container build must copy Directory.Build.props before restore.");
        Assert.True(propertiesCopy < restore, "Directory.Build.props must be available when MSBuild evaluates the API project.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Legacy.Maliev.DocumentService.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
