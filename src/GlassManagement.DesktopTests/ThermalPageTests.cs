using System.Linq;
using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// B7 热工两屏（CW26 ThermalGlassCalcView / CW27 ThermalWallCalcView）逐字对拍。
///
/// <para><b>实机基线</b>（b7_probe6_cw26.json / b7_probe6_cw27.json RichText 全文，
/// 城市=上海「上海,3A,3,0.5,-3.0」）：CW26 四段 K=5.464/5.961(×)、SHGC=0.731/0.725(×)、
/// τ=0.75·窗墙比0.32(√)、tsi=5.008/露点1.917(√)；CW27 K=0.283(√)、tsi=17.379/露点0.188(√)。</para>
///
/// <para>★ V31 行为复刻：未选城市预演=「请选择城市」0 输出（b7_probe4 弹框实采）；
/// 框材质断热→5.174/0.712/0.709（probe10 九项链）、湿度60→露点12.003（probe9）。</para>
/// </summary>
[TestFixture]
public class ThermalPageTests
{
    private const string Sep = "********************************************";

    [Test]
    public void CW26_未选城市预演_V31提示请选择城市且无输出()
    {
        var (session, wiz) = FirstPage.Run();
        var vm = new ThermalGlassCalcViewModel(session, wiz);
        vm.PreviewCommand.Execute(null);
        Assert.That(vm.StatusText, Is.EqualTo("请选择城市"));
        Assert.That(vm.Results, Is.Empty);
    }

    [Test]
    public void CW26_上海基线四段逐字()
    {
        var (session, wiz) = FirstPage.Run();
        var vm = new ThermalGlassCalcViewModel(session, wiz);
        vm.ChooseCityByName("上海");
        vm.PreviewCommand.Execute(null);
        var raw = string.Join("  ", vm.Results.Select(r => r.Text))
            .Replace("  " + Sep, Sep);
        // b7_probe6_cw26 RichText 全文逐字
        Assert.That(raw, Does.Contain("玻璃自身K值为:5.464W/(m2·K)"));
        Assert.That(raw, Does.Contain("门窗系统冬季K值为:5.961W/(m2·K)"));
        Assert.That(raw, Does.Contain("按要求，此处K值不应大于:2.2W/(m2·K)      不满足要求(×)"));
        Assert.That(raw, Does.Contain("计算单元的太阳得热系数夏季计算值为:0.731"));
        Assert.That(raw, Does.Contain("计算单元的太阳得热系数冬季计算值为:0.725"));
        Assert.That(raw, Does.Contain("按规范，太阳得热系数夏季不应大于:0.35"));
        Assert.That(raw, Does.Contain("玻璃可见光透射比为:0.75      窗墙比为:0.32"));
        Assert.That(raw, Does.Contain("室内表面的计算温度为:5.008℃      空气的露点温度为:1.917℃      满足要求(√)"));
    }

    [Test]
    public void CW26_框材质断热与湿度60扰动逐位()
    {
        var (session, wiz) = FirstPage.Run();
        var vm = new ThermalGlassCalcViewModel(session, wiz);
        vm.ChooseCityByName("上海");
        vm.FrameMaterialIndex = 1;   // 断热铝合金型材 Kf=3.318（probe10 运行期直读）
        vm.PreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("门窗系统冬季K值为:5.174"));
        Assert.That(s, Does.Contain("计算单元的太阳得热系数夏季计算值为:0.712"));
        Assert.That(s, Does.Contain("计算单元的太阳得热系数冬季计算值为:0.709"));

        vm.FrameMaterialIndex = 0;   // 回普通铝
        vm.HumidityIndex = 1;        // 60 → φ=0.6（probe9：round3(0.6·23.389)=14.033 → 露点 12.003）
        vm.PreviewCommand.Execute(null);
        s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("空气的露点温度为:12.003℃"));
        Assert.That(s, Does.Contain("室内表面的计算温度为:5.008℃"));  // tsi 不随湿度变（probe8e）
    }

    [Test]
    public void CW27_上海基线两段逐字()
    {
        var (session, wiz) = FirstPage.Run();
        var vm = new ThermalWallCalcViewModel(session, wiz);
        vm.ChooseCityByName("上海");
        vm.PreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        // b7_probe6_cw27 RichText 全文逐字
        Assert.That(s, Does.Contain("幕墙的冬季K值为:0.283W/(m2·K)"));
        Assert.That(s, Does.Contain("按要求，幕墙的K值不应大于:0.8W/(m2·K)      满足要求(√)"));
        Assert.That(s, Does.Contain("幕墙室内表面的计算温度为:17.379℃      空气的露点温度为:0.188℃      满足要求(√)"));
    }

    [Test]
    public void CW27_非采暖与湿度60扰动逐位()
    {
        var (session, wiz) = FirstPage.Run();
        var vm = new ThermalWallCalcViewModel(session, wiz);
        vm.ChooseCityByName("上海");
        vm.HeatingIndex = 1;   // 非采暖 → t_i=12（probe12：14.03/4.209/−5.013/11.566）
        vm.PreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("幕墙室内表面的计算温度为:11.566℃"));
        Assert.That(s, Does.Contain("空气的露点温度为:-5.013℃"));

        vm.HeatingIndex = 0;
        vm.HumidityIndex = 1;  // 60 → 露点 10.127（probe12）
        vm.PreviewCommand.Execute(null);
        s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("空气的露点温度为:10.127℃"));
    }

    [Test]
    public void 城市表来源_inx五元组与省序逐位()
    {
        // sh.inx 原文「上海,3A,3,0.5,-3.0」= 条目文本（b7_probe4/5 实采）
        var sh = GlassManagement.Calc.Data.ThermalCityTable
            .Of("上海").Single();
        Assert.That(sh.ItemText, Is.EqualTo("上海,3A,3,0.5,-3.0"));
        Assert.That(GlassManagement.Calc.Data.ThermalCityTable
            .Of("北京").Single().ItemText, Is.EqualTo("北京,2B,55,-7.0,-11.8"));
        // V31 省 ListBox head 前 15 项逐位（probe4 city_log n=31）
        var head = GlassManagement.Calc.Data.ThermalCityTable.Provinces.Take(15).ToArray();
        Assert.That(head, Is.EqualTo(new[] { "北京", "天津", "上海", "重庆", "黑龙江", "吉林",
            "辽宁", "内蒙古", "山东", "河北", "河南", "山西", "陕西", "甘肃", "宁夏" }));
    }

    [Test]
    public void CW27_层表默认值等于参数库热工数值行()
    {
        var (session, wiz) = FirstPage.Run();
        var vm = new ThermalWallCalcViewModel(session, wiz);
        Assert.That(vm.Layers.Select(l => l.Name), Is.EqualTo(new[]
            { "千丝板", "钢筋混凝土", "聚苯乙烯泡沫塑料" }));
        Assert.That(vm.Layers.Select(l => l.ThkText), Is.EqualTo(new[] { "10", "240", "100" }));
        Assert.That(vm.Layers.Select(l => l.LambdaText), Is.EqualTo(new[] { "0.3", "1.74", "0.033" }));
    }
}
