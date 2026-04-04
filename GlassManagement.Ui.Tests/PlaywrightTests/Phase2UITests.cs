using System.Threading.Tasks;
using Xunit;
using Microsoft.Playwright;

namespace GlassManagement.Ui.Tests.PlaywrightTests
{
    public class Phase2UITests : IAsyncLifetime
    {
        IPlaywright _pw;
        IBrowser _browser;
        IBrowserContext _ctx;
        IPage _page;

        public async Task InitializeAsync()
        {
            _pw = await Playwright.CreateAsync();
            _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _ctx = await _browser.NewContextAsync();
            _page = await _ctx.NewPageAsync();
        }

        public async Task DisposeAsync()
        {
            await _page.CloseAsync();
            await _ctx.CloseAsync();
            await _browser.CloseAsync();
            _pw.Dispose();
        }

        [Fact]
        public async Task Phase2_MultiPanel_UI_Workflow()
        {
            // Assumes local server is running at http://localhost:5000
            string baseUrl = "http://localhost:5000";
            await _page.GotoAsync(baseUrl + "/phase2");
            // Wait for region selector to load
            await _page.Locator("select").First.OrThrow("Region select not found");
            // Add a second panel if UI has an Add Panel button
            var addBtn = _page.Locator("button:has-text('Add Panel')");
            if (await addBtn.CountAsync() > 0)
            {
                await addBtn.ClickAsync();
                await _page.FillAsync("input[placeholder='PanelId']", "p2-2");
                await _page.FillAsync("input[placeholder='Type']", "Single");
            }
            // Submit Phase 2
            var submitBtn = _page.Locator("button:has-text('提交阶段2')");
            await submitBtn.ClickAsync();

            // Wait for API response
            var response = await _page.WaitForResponseAsync(r => r.Url.Contains("/api/calculation/phase2"));
            Assert.NotNull(response);
            var json = await response.JsonAsync<object>();
            Assert.NotNull(json);
        }
    }
}
