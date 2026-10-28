using GlassManagement.Calc.Data;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 螺栓参数必须来自参数库（`螺栓1..7.csv` 一直存在，但此前 src 下零代码加载）。
///
/// 7 张表实测只有 3 种不同内容：
///   螺栓1=2=3：6 数据列，48 行，含 M5
///   螺栓4=5=6：7 数据列（多「孔径」），48 行
///   螺栓7    ：6 数据列，36 行，M8 起
/// </summary>
public class BoltParamSourceTests
{
    [Fact]
    public void 螺栓表确实从参数库读到()
    {
        Assert.True(ParamLibrary.BoltTableLoaded,
            "未读到 螺栓1.csv —— 走了内置快照，检查 ParamDir");
        Assert.True(ParamLibrary.Bolts.Count > 20, "行数异常");
    }

    [Fact]
    public void 按型号查_含有效直径与剪拉应力()
    {
        var m12 = ParamLibrary.FindBolt("M12");
        Assert.NotNull(m12);
        Assert.Equal("M12", m12!.Model);
        Assert.Equal(12.0, m12.NominalMm);
        Assert.True(m12.EffectiveMm > 9 && m12.EffectiveMm < 12, $"有效直径异常: {m12.EffectiveMm}");
        Assert.True(m12.ShearMpa > 0 && m12.TensionMpa > 0);
    }

    [Fact]
    public void 只给数字也能查到_自动补M()
    {
        var a = ParamLibrary.FindBolt("12");
        var b = ParamLibrary.FindBolt("M12");
        Assert.NotNull(a);
        Assert.Equal(b!.Model, a!.Model);
        Assert.Equal(b.EffectiveMm, a.EffectiveMm);
    }

    [Fact]
    public void 同型号多行_按材质或用途筛()
    {
        // 每个型号至少两行（碳钢/不锈钢 或 热轧/冷弯）
        var carbon = ParamLibrary.FindBolt("M12", material: "碳钢");
        var steel = ParamLibrary.FindBolt("M12", material: "不锈钢");
        Assert.NotNull(carbon);
        Assert.NotNull(steel);
        Assert.Contains("碳钢", carbon!.Material);
        Assert.Contains("不锈钢", steel!.Material);
        // 两者许用值不同（否则筛了等于没筛）
        Assert.NotEqual(carbon.ShearMpa, steel.ShearMpa);
    }

    [Fact]
    public void 不存在的型号返回null()
    {
        Assert.Null(ParamLibrary.FindBolt("M999"));
    }

    [Fact]
    public void 七列版表有孔径列_六列版没有()
    {
        var six = ParamLibrary.FindBolt("M12", fileName: "螺栓1.csv");
        var seven = ParamLibrary.FindBolt("M12", fileName: "螺栓4.csv");
        Assert.NotNull(six);
        Assert.NotNull(seven);
        Assert.Equal(0.0, six!.HoleMm);                 // 螺栓1 无「孔径」列
        Assert.True(seven!.HoleMm > 0, "螺栓4 应有孔径");
        // 除孔径外，核心参数应一致
        Assert.Equal(six.EffectiveMm, seven.EffectiveMm);
        Assert.Equal(six.TensionMpa, seven.TensionMpa);
    }
}
