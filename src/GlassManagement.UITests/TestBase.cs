using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

public abstract class TestBase
{
    private static Process? _serverProcess;
    private static int _testClassCount;
    private static readonly object _lock = new();

    protected static IPlaywright PlaywrightInstance { get; private set; } = null!;
    protected static IBrowser Browser { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;

    protected const string BaseUrl = "http://localhost:5188";
    protected const string ScreenshotDir = "TestResults/screenshots";

    [OneTimeSetUp]
    public async Task OneTimeSetUpBase()
    {
        lock (_lock)
        {
            _testClassCount++;
            if (_serverProcess == null)
            {
                StartServer();
            }
        }

        await WaitForServer();

        if (PlaywrightInstance == null)
        {
            PlaywrightInstance = await Playwright.CreateAsync();
            Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
        }

        Directory.CreateDirectory(ScreenshotDir);
    }

    [SetUp]
    public async Task SetUpBase()
    {
        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "zh-CN",
        });
        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(30_000);
    }

    [TearDown]
    public async Task TearDownBase()
    {
        // Take screenshot on failure
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var name = TestContext.CurrentContext.Test.Name;
            await TakeScreenshot($"FAIL_{name}");
        }

        if (Context != null)
        {
            await Context.CloseAsync();
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownBase()
    {
        lock (_lock)
        {
            _testClassCount--;
            if (_testClassCount <= 0)
            {
                StopServer();
            }
        }

        if (_testClassCount <= 0 && Browser != null)
        {
            await Browser.CloseAsync();
            PlaywrightInstance?.Dispose();
            PlaywrightInstance = null!;
            Browser = null!;
        }
    }

    protected async Task TakeScreenshot(string name)
    {
        Directory.CreateDirectory(ScreenshotDir);
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(ScreenshotDir, $"{name}.png"),
            FullPage = true,
        });
    }

    protected async Task LoginAsAdmin()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("请输入用户名").FillAsync("admin");
        await Page.GetByPlaceholder("请输入密码").FillAsync("admin123");
        await Page.GetByRole(AriaRole.Button, new() { Name = "登 录" }).ClickAsync();
        await Page.WaitForURLAsync($"**/*");
        await Task.Delay(500);
    }

    protected async Task NavigateToWizardAndStart()
    {
        await Page.GotoAsync($"{BaseUrl}/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var startBtn = Page.GetByRole(AriaRole.Button, new() { Name = "开始新的计算会话" });
        if (await startBtn.IsVisibleAsync())
        {
            await startBtn.ClickAsync();
            await Task.Delay(1000);
        }
    }

    private static void StartServer()
    {
        var apiDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "GlassManagement.Api"));

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-build",
                WorkingDirectory = apiDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment = { ["ASPNETCORE_URLS"] = BaseUrl },
            }
        };
        _serverProcess.Start();
    }

    private static async Task WaitForServer(int timeoutSeconds = 60)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await http.GetAsync(BaseUrl);
                if (resp.StatusCode != HttpStatusCode.ServiceUnavailable)
                    return;
            }
            catch
            {
                // Server not ready yet
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Server at {BaseUrl} did not become available within {timeoutSeconds}s");
    }

    private static void StopServer()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill(entireProcessTree: true);
            _serverProcess.Dispose();
            _serverProcess = null;
        }
    }
}
