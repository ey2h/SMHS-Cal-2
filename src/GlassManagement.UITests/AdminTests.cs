using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class AdminTests : TestBase
{
    [SetUp]
    public async Task LoginAndGoAdmin()
    {
        await LoginAsAdmin();
        await Page.GotoAsync($"{BaseUrl}/admin");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(500);
    }

    [Test]
    public async Task Test_AdminPage_UserList()
    {
        await TakeScreenshot("Admin_UserList");

        // Verify user list table/cards are visible
        var userItems = Page.Locator("table tr, .user-card, .user-item, [class*='user']");
        var count = await userItems.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Should display users");
        await TakeScreenshot("Admin_UserListLoaded");
    }

    [Test]
    public async Task Test_AdminPage_AddUser()
    {
        var addBtn = Page.GetByRole(AriaRole.Button, new() { Name = "添加" });
        if (!await addBtn.IsVisibleAsync())
            addBtn = Page.Locator("button:has-text('新增'), button:has-text('添加用户'), button:has-text('新建')").First;

        if (await addBtn.IsVisibleAsync())
        {
            await addBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Admin_AddUserDialog");

            // Fill form if dialog appeared
            var usernameInput = Page.GetByPlaceholder("用户名");
            if (!await usernameInput.IsVisibleAsync())
                usernameInput = Page.Locator("input[name='username'], input[placeholder*='用户']").First;

            if (await usernameInput.IsVisibleAsync())
            {
                await usernameInput.FillAsync("测试用户UI");
                var passwordInput = Page.Locator("input[type='password'], input[name='password'], input[placeholder*='密码']").First;
                if (await passwordInput.IsVisibleAsync())
                    await passwordInput.FillAsync("test12345");

                await TakeScreenshot("Admin_AddUserFilled");
            }
        }
    }

    [Test]
    public async Task Test_AdminPage_EditUser()
    {
        // Click edit button on first user
        var editBtn = Page.Locator("button:has-text('编辑'), button:has-text('修改'), .edit-btn").First;
        if (await editBtn.IsVisibleAsync())
        {
            await editBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Admin_EditUser");
        }
    }

    [Test]
    public async Task Test_AdminPage_SetLicense()
    {
        // Look for license/authorization settings
        var licenseBtn = Page.Locator("button:has-text('授权'), button:has-text('许可'), button:has-text('时限')").First;
        if (await licenseBtn.IsVisibleAsync())
        {
            await licenseBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Admin_SetLicense");
        }
        else
        {
            await TakeScreenshot("Admin_LicenseNotFound");
        }
    }

    [Test]
    public async Task Test_AdminPage_ToggleUser()
    {
        var toggleBtn = Page.Locator("button:has-text('禁用'), button:has-text('启用'), .toggle-btn, [class*='toggle']").First;
        if (await toggleBtn.IsVisibleAsync())
        {
            await toggleBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Admin_ToggleUser");
        }
    }

    [Test]
    public async Task Test_AdminPage_RegenerateToken()
    {
        var tokenBtn = Page.Locator("button:has-text('Token'), button:has-text('重新生成'), button:has-text('刷新Token')").First;
        if (await tokenBtn.IsVisibleAsync())
        {
            await tokenBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Admin_RegenerateToken");
        }
    }

    [Test]
    public async Task Test_AdminPage_UsageStats()
    {
        var statsBtn = Page.Locator("button:has-text('统计'), button:has-text('使用'), [class*='stats']").First;
        if (await statsBtn.IsVisibleAsync())
        {
            await statsBtn.ClickAsync();
            await Task.Delay(500);
        }
        await TakeScreenshot("Admin_UsageStats");
    }
}
