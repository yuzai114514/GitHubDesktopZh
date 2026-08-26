using GitHubDesktopZh.Core.Services;
using Xunit;
using System.IO;

namespace GitHubDesktopZh.Tests.Services;

public class DownloadServiceTests
{
    [Fact]
    public async Task ComputeSha256Async_ReturnsCorrectHash()
    {
        var service = new DownloadService();
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test content");
            var hash = await service.ComputeSha256Async(tempFile);
            Assert.False(string.IsNullOrEmpty(hash));
            Assert.Equal(64, hash.Length);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}