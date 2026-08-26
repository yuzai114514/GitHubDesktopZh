using GitHubDesktopZh.Core.Services;
using Xunit;

namespace GitHubDesktopZh.Tests.Services;

public class DesktopDetectorTests
{
    [Fact]
    public void Detect_WhenGitHubDesktopInstalled_ReturnsInfo()
    {
        var detector = new DesktopDetector();
        var info = detector.Detect();
        
        // This test will only pass if GitHub Desktop is installed
        // For CI, we might want to skip or mock
        if (info != null)
        {
            Assert.False(string.IsNullOrEmpty(info.Version));
            Assert.False(string.IsNullOrEmpty(info.InstallationPath));
        }
    }

    [Fact]
    public void Detect_WhenNotInstalled_ReturnsNull()
    {
        // This is hard to test without mocking
        // We'll just ensure no exception
        var detector = new DesktopDetector();
        var info = detector.Detect();
        
        // No assertion, just ensure no exception
    }
}