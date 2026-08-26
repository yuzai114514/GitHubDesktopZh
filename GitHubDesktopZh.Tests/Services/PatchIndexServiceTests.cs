using GitHubDesktopZh.Core.Models;
using GitHubDesktopZh.Core.Services;
using Xunit;

namespace GitHubDesktopZh.Tests.Services;

public class PatchIndexServiceTests
{
    [Fact]
    public void FindPatch_ExactMatch_ReturnsPatch()
    {
        var service = new PatchIndexService(string.Empty, string.Empty, string.Empty);
        var index = new PatchIndex
        {
            Patches = new[]
            {
                new PatchEntry { Version = "3.6.4", Url = "http://example.com", Sha256 = "abc123", Size = 12345 }
            }
        };

        var patch = service.FindPatch(index, "3.6.4");
        Assert.NotNull(patch);
        Assert.Equal("3.6.4", patch!.Version);
    }

    [Fact]
    public void FindPatch_CompatMatch_ReturnsPatch()
    {
        var service = new PatchIndexService(string.Empty, string.Empty, string.Empty);
        var index = new PatchIndex
        {
            Patches = new[]
            {
                new PatchEntry 
                { 
                    Version = "3.6.4", 
                    Url = "http://example.com", 
                    Sha256 = "abc123", 
                    Size = 12345,
                    Compat = new[] { "3.6.3", "3.6.2" }
                }
            }
        };

        var patch = service.FindPatch(index, "3.6.3");
        Assert.NotNull(patch);
    }

    [Fact]
    public void FindPatch_NoMatch_ReturnsNull()
    {
        var service = new PatchIndexService(string.Empty, string.Empty, string.Empty);
        var index = new PatchIndex
        {
            Patches = new[]
            {
                new PatchEntry { Version = "3.6.4", Url = "http://example.com", Sha256 = "abc123", Size = 12345 }
            }
        };

        var patch = service.FindPatch(index, "999.0.0");
        Assert.Null(patch);
    }
}
