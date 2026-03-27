using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class LoginTests : TestBase
{
    [Test]
    public async Task Test_AdminLogin_Success()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await TakeScreenshot("Login_Initial");

        await Page.GetByPlaceholder("请输入用户名").FillAsync("admin");
        await Page.GetByPlaceholder("请输入密码").FillAsync("admin123");
        await Page.GetByRole(AriaRole.Button, new() { Name = "登 录" }).ClickAsync();

        // Should navigate away from login
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });
        await TakeScreenshot("Login_AdminSuccess");

        Assert.That(Page.Url, Does.Not.Contain("/login"));
    }

    [Test]
    public async Task Test_TokenLogin_Success()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        // Switch to token tab
        await Page.GetByText("API Token").ClickAsync();
        await Task.Delay(300);
        await TakeScreenshot("Login_TokenTab");

        // We need a valid token - first login as admin to get one
        // For now, test that the token input is visible
        var tokenInput = Page.GetByPlaceholder("请输入 API Token");
        await Assertions.Expect(tokenInput).ToBeVisibleAsync();
        await TakeScreenshot("Login_TokenInput");
    }

    [Test]
    public async Task Test_InvalidPassword_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        await Page.GetByPlaceholder("请输入用户名").FillAsync("admin");
        await Page.GetByPlaceholder("请输入密码").FillAsync("wrongpassword");
        await Page.GetByRole(AriaRole.Button, new() { Name = "登 录" }).ClickAsync();
        await Task.Delay(1000);

        // Should show error message
        var error = Page.Locator(".login-error");
        await Assertions.Expect(error).ToBeVisibleAsync();
        await TakeScreenshot("Login_InvalidPassword");
    }

    [Test]
    public async Task Test_ExpiredUser_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        await Page.GetByPlaceholder("请输入用户名").FillAsync("王五");
        await Page.GetByPlaceholder("请输入密码").FillAsync("test123");
        await Page.GetByRole(AriaRole.Button, new() { Name = "登 录" }).ClickAsync();
        await Task.Delay(1000);

        var error = Page.Locator(".login-error");
        await Assertions.Expect(error).ToBeVisibleAsync();
        await TakeScreenshot("Login_ExpiredUser");
    }

    [Test]
    public async Task Test_DisabledUser_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        await Page.GetByPlaceholder("请输入用户名").FillAsync("赵六");
        await Page.GetByPlaceholder("请输入密码").FillAsync("test123");
        await Page.GetByRole(AriaRole.Button, new() { Name = "登 录" }).ClickAsync();
        await Task.Delay(1000);

        var error = Page.Locator(".login-error");
        await Assertions.Expect(error).ToBeVisibleAsync();
        await TakeScreenshot("Login_DisabledUser");
    }

    [Test]
    public async Task Test_LoginTabSwitch()
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        // Default should be password tab
        var passwordTab = Page.GetByText("账号密码");
        var tokenTab = Page.GetByText("API Token");

        await Assertions.Expect(passwordTab).ToBeVisibleAsync();
        await Assertions.Expect(tokenTab).ToBeVisibleAsync();

        // Switch to token
        await tokenTab.ClickAsync();
        await Task.Delay(300);
        var tokenInput = Page.GetByPlaceholder("请输入 API Token");
        await Assertions.Expect(tokenInput).ToBeVisibleAsync();
        await TakeScreenshot("Login_TabToken");

        // Switch back to password
        await passwordTab.ClickAsync();
        await Task.Delay(300);
        var usernameInput = Page.GetByPlaceholder("请输入用户名");
        await Assertions.Expect(usernameInput).ToBeVisibleAsync();
        await TakeScreenshot("Login_TabPassword");
    }
}
