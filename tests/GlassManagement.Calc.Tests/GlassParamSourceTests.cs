using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 玻璃材料参数必须来自参数库，不能是源码里的字面量。
///
/// 背景：<c>参数库.mdb</c> 的 59 张表全是型材/板材截面表，<b>没有玻璃材料表</b>
/// （两处 mdb MD5 相同、GetSchema 全量导出、无「强度设计值」列）。
/// 故这些值落在参数库目录的 <c>玻璃材料（复刻补充）.csv</c>，由 ParamLibrary 运行时读取；
/// 内置快照只在找不到库目录时兜底。
///
/// 玻璃<b>厚度档 / 种类档 / 胶片档</b> 是 V31 窗体的程序常量，不属于参数库数据。
/// </summary>
public class GlassParamSourceTests
{
    [Fact]
    public void 玻璃材料表_确实从参数库目录读到()
    {
        Assert.True(ParamLibrary.GlassTableLoaded,
            "未读到 玻璃材料（复刻补充）.csv —— 说明走了内置快照兜底，需检查 ParamDir");
        Assert.Contains("玻璃材料", ParamLibrary.ParamDir + "/玻璃材料（复刻补充）.csv");
    }

    [Fact]
    public void 材料常数来自参数库()
    {
        Assert.Equal(2.56e-5, ParamLibrary.GlassDensity);
        Assert.Equal(72000.0, ParamLibrary.GlassElasticModulus);
        Assert.Equal(12.0, ParamLibrary.GlassStrengthBreakThickness);
        // 引擎侧不再持有字面量
        Assert.Equal(ParamLibrary.GlassDensity, GlassEngine.DensityNmm3);
        Assert.Equal(ParamLibrary.GlassElasticModulus, GlassEngine.E);
    }

    [Fact]
    public void 强度设计值按种类与厚度查表()
    {
        Assert.Equal(84.0, GlassAllowables.Bending("钢化玻璃", 6));    // 薄侧
        Assert.Equal(72.0, GlassAllowables.Bending("钢化玻璃", 15));   // 厚侧
        Assert.Equal(41.0, GlassAllowables.Bending("半钢化玻璃", 6));
        Assert.Equal(38.0, GlassAllowables.Bending("半钢化玻璃", 12));
        Assert.Equal(28.0, GlassAllowables.Bending("普通玻璃", 8));
        Assert.Equal(24.0, GlassAllowables.Bending("普通玻璃", 19));
        // 未列出的种类按普通玻璃档（与 V31 的 else 分支一致）
        Assert.Equal(28.0, GlassAllowables.Bending("不存在的种类", 8));
    }
}
