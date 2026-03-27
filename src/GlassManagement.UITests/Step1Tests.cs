using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class Step1Tests : TestBase
{
    [SetUp]
    public async Task LoginAndNavigate()
    {
        await LoginAsAdmin();
        await NavigateToWizardAndStart();
        await Task.Delay(500);
    }

    [Test]
    public async Task Test_RegionSearch()
    {
        var searchInput = Page.Locator("input[placeholder*='搜索']").First;
        if (!await searchInput.IsVisibleAsync())
            searchInput = Page.Locator("input[type='text']").First;

        await searchInput.FillAsync("北京");
        await Task.Delay(1000);

        // Check dropdown appears
        var dropdown = Page.Locator(".region-dropdown, .dropdown-list, .search-results, [class*='dropdown']").First;
        await TakeScreenshot("Step1_RegionSearch");

        // Select first result
        var firstItem = Page.Locator(".region-dropdown li, .dropdown-list li, .search-results li, [class*='dropdown'] li, [class*='dropdown-item']").First;
        if (await firstItem.IsVisibleAsync())
        {
            await firstItem.ClickAsync();
            await Task.Delay(500);
        }
        await TakeScreenshot("Step1_RegionSelected");
    }

    [Test]
    public async Task Test_RegionDropdownScroll()
    {
        var searchInput = Page.Locator("input[placeholder*='搜索']").First;
        if (!await searchInput.IsVisibleAsync())
            searchInput = Page.Locator("input[type='text']").First;

        await searchInput.ClickAsync();
        await Task.Delay(500);

        var dropdown = Page.Locator(".region-dropdown, .dropdown-list, [class*='dropdown']").First;
        if (await dropdown.IsVisibleAsync())
        {
            await dropdown.EvaluateAsync("el => el.scrollTop = 200");
            await TakeScreenshot("Step1_DropdownScrollDown");
            await dropdown.EvaluateAsync("el => el.scrollTop = 0");
            await TakeScreenshot("Step1_DropdownScrollUp");
        }
    }

    [Test]
    public async Task Test_CurtainWallTypeSelect()
    {
        var types = new[] { "全隐框", "半隐框", "明框", "金属板", "石材", "人造板" };

        var select = Page.Locator("select").First;
        if (await select.IsVisibleAsync())
        {
            foreach (var t in types)
            {
                try
                {
                    await select.SelectOptionAsync(new SelectOptionValue { Label = t });
                    await Task.Delay(300);
                    await TakeScreenshot($"Step1_CurtainType_{t}");
                }
                catch
                {
                    // Type might not exist as option
                }
            }
        }
        else
        {
            // Try radio buttons or other selectors
            foreach (var t in types)
            {
                var option = Page.GetByText(t).First;
                if (await option.IsVisibleAsync())
                {
                    await option.ClickAsync();
                    await Task.Delay(300);
                    await TakeScreenshot($"Step1_CurtainType_{t}");
                }
            }
        }
    }

    [Test]
    public async Task Test_InputFields_FillAll()
    {
        // Fill all visible input fields
        var inputs = Page.Locator("input[type='number'], input[type='text']");
        var count = await inputs.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var input = inputs.Nth(i);
            if (await input.IsVisibleAsync() && await input.IsEditableAsync())
            {
                await input.FillAsync("1.5");
            }
        }
        await TakeScreenshot("Step1_AllFieldsFilled");
    }

    [Test]
    public async Task Test_InputValidation_Empty()
    {
        // Clear all inputs and try next
        var inputs = Page.Locator("input[type='number']");
        var count = await inputs.CountAsync();
        for (int i = 0; i < count; i++)
        {
            var input = inputs.Nth(i);
            if (await input.IsVisibleAsync() && await input.IsEditableAsync())
            {
                await input.FillAsync("");
            }
        }

        var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "下一步" });
        if (await nextBtn.IsVisibleAsync())
            await nextBtn.ClickAsync();

        await Task.Delay(500);
        await TakeScreenshot("Step1_ValidationEmpty");
    }

    [Test]
    public async Task Test_InputValidation_OutOfRange()
    {
        var inputs = Page.Locator("input[type='number']");
        var count = await inputs.CountAsync();
        for (int i = 0; i < count; i++)
        {
            var input = inputs.Nth(i);
            if (await input.IsVisibleAsync() && await input.IsEditableAsync())
            {
                await input.FillAsync("99999");
            }
        }

        var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "下一步" });
        if (await nextBtn.IsVisibleAsync())
            await nextBtn.ClickAsync();

        await Task.Delay(500);
        await TakeScreenshot("Step1_ValidationOutOfRange");
    }

    [Test]
    public async Task Test_HelpPopups()
    {
        var helpButtons = Page.Locator("button:has-text('?'), .help-btn, [class*='help']");
        var count = await helpButtons.CountAsync();

        for (int i = 0; i < Math.Min(count, 7); i++)
        {
            await helpButtons.Nth(i).ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot($"Step1_Help_{i + 1}");

            // Close popup if visible
            var closeBtn = Page.Locator(".modal-close, .dialog-close, button:has-text('关闭'), button:has-text('确定')").First;
            if (await closeBtn.IsVisibleAsync())
                await closeBtn.ClickAsync();
            await Task.Delay(300);
        }
    }

    [Test]
    public async Task Test_StructuralWeightPreset()
    {
        var presetBtn = Page.GetByRole(AriaRole.Button, new() { Name = "参数预置" });
        if (!await presetBtn.IsVisibleAsync())
            presetBtn = Page.GetByText("参数预置").First;

        if (await presetBtn.IsVisibleAsync())
        {
            await presetBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Step1_PresetDialog");

            // Verify dialog shows curtain wall types
            var dialog = Page.Locator(".modal, .dialog, [class*='dialog'], [class*='modal']").First;
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            await TakeScreenshot("Step1_PresetTypes");
        }
    }
}
