using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 门窗连接件计算引擎对拍（V31 实机全选项组合遍历，逐位 0 误差）：
/// door_shuting_var.json（基线）、door_shuting_wall30.json（t_wall=3.0 插值）、
/// door_shuting_perturb.json（扰动归因）、door_conn_matrix.json（材质11case/铆钉/片数/模式）、
/// door_conn_probe4a.json（铆钉类型×规格 25格 + 膨胀5格 + 卡槽/不计算/有钢附框 + 外片10mm）。
/// </summary>
public class DoorConnectorEngineTests
{
    private static DoorConnectorEngine.FastenerCheck Base =>
        DoorConnectorEngine.Compute(new DoorConnectorEngine.Input()).FrameSide!;

    [Fact]
    public void 门窗连接件_默认基线()
    {
        var r = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input());
        Assert.Equal(10275.0, r.WindShearN);        // 0x1404A27 round3
        Assert.Equal(0.0, r.SeismicShearN);         // 0x1404CB7
        Assert.Equal(10275.0, r.TotalShearN);       // 0x1404DC0
        Assert.Equal(535.156, Base.PointForceN);    // 0x1405028
        Assert.Equal(972.0, Base.WallCapacityN);    // min(f37=1059.6, f24=972)
        Assert.Equal(400.0, Base.FastenerCapacityN);
        Assert.True(Base.WallOk);                   // 输出：满足(√)
        Assert.False(Base.FastenerOk);              // 输出：不满足(×)
        Assert.Equal(535.156, r.StructPointForceN);
        Assert.Equal(4551.0, r.StructCapacityN);    // 0x140807F：3.7·1.5·4.0·205
        Assert.True(r.StructOk);
    }

    [Fact]
    public void 门窗连接件_材质11case逐位()
    {
        // door_conn_matrix.json 实采（薄壁件设计值 = 表 fa → min(f37,f24) 逐位）
        var expect = new[] { 972.0, 2160.0, 972.0, 1620.0, 1458.0, 1728.0,
                             399.6, 2214.0, 3348.0, 1458.0, 432.0 };
        for (int i = 0; i < 11; i++)
        {
            var f = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
            {
                WallMaterialIndex = i,
            }).FrameSide!;
            Assert.Equal(expect[i], f.WallCapacityN);
        }
        Assert.Equal(new[] { 90.0, 200, 90, 150, 135, 160, 37, 205, 310, 135, 40 },
                     DoorConnectorEngine.MaterialShearMpa);
    }

    [Fact]
    public void 门窗连接件_铆钉类型规格25格逐位()
    {
        // door_conn_probe4a.json A 矩阵：设计值=f(薄壁,d钉)+承载力表(类型,d钉)
        // 薄壁件设计值仅随 d钉 变（同 d钉 各类型同值）：
        var wallByD = new[] { 972.0, 1036.8, 1223.52, 1340.299, 1367.937 };
        double[] dOpen = { 3.0, 3.2, 4.0, 4.8, 5.0 };
        double[] dZi = { 3.5, 3.9, 4.2, 4.8, 5.5 };
        double[] cap1012 = { 400, 500, 850, 1200, 1400 };
        double[] cap1115 = { 550, 750, 1250, 1850, 2150 };
        double[] capC1115 = { 930, 1100, 1600, 2200, 2420 };
        double[] capZi = { 1346, 1671, 1938, 2532, 3324 };
        double[][] caps = { cap1012, cap1115, capC1115, capZi, capZi };
        for (int t = 0; t < 5; t++)
        {
            double[] ds = t <= 2 ? dOpen : dZi;
            for (int s = 0; s < 5; s++)
            {
                var f = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
                {
                    FrameFastenerType = t,
                    FrameScrewDiameter = ds[s],
                }).FrameSide!;
                Assert.Equal(caps[t][s], f.FastenerCapacityN);
                double wallExpect = t <= 2 ? wallByD[s] : wallByD[s];
                // 自攻 3.5/3.9/4.2 的薄壁件值独立实采：1134/1208.129/1253.735
                double[] ziWall = { 1134.0, 1208.129, 1253.735, 1340.299, 1434.705 };
                Assert.Equal(t <= 2 ? wallByD[s] : ziWall[s], f.WallCapacityN);
                _ = wallExpect;
            }
        }
    }

    [Fact]
    public void 门窗连接件_薄壁件设计值min规则与插值()
    {
        // min 规则反证：d=4.0 时 f37=1223.52 < f24=1440 → 输出 1223.52（旧「取f2」不成立）
        var f = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        {
            FrameScrewDiameter = 4.0,
        }).FrameSide!;
        Assert.Equal(1223.52, f.WallCapacityN);

        // door_shuting_wall30.json：t_wall 1.5→3.0，r=2 →
        // round3(1059.6+(972−1059.6)×1÷1.5)=1001.2（0x1406213）逐位
        var r2 = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        {
            WallThicknessMm = 3.0,
        }).FrameSide!;
        Assert.Equal(1001.2, r2.WallCapacityN);
    }

    [Fact]
    public void 门窗连接件_片数除数扰动()
    {
        // matrix paramA：连接片数 1→2 边框侧计算值 535.156→267.578；1→3→178.385
        Assert.Equal(267.578, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { PlateCountFrameSide = 2 }).FrameSide!.PointForceN);
        Assert.Equal(178.385, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { PlateCountFrameSide = 3 }).FrameSide!.PointForceN);
        // paramB：结构侧 1→2/3 仅改结构侧计算值
        var b2 = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { PlateCountStructSide = 2 });
        Assert.Equal(267.578, b2.StructPointForceN);
        Assert.Equal(535.156, b2.FrameSide!.PointForceN);
        Assert.Equal(178.385, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { PlateCountStructSide = 3 }).StructPointForceN);
        Assert.Equal(133.789, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { PlateCountStructSide = 4 }).StructPointForceN);
    }

    [Fact]
    public void 门窗连接件_结构侧钉径与膨胀螺钉()
    {
        // specB 918,522：4.2→4778.55、5.5→6257.625（3.7·t·d2·205 逐位）
        Assert.Equal(4778.55, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { StructScrewDiameter = 4.2 }).StructCapacityN);
        Assert.Equal(6257.625, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { StructScrewDiameter = 5.5 }).StructCapacityN);
        // 膨胀螺钉：结构侧常数 3956.4（probe 实采）
        Assert.Equal(3956.4, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { ExpansionMode = true }).StructCapacityN);
    }

    [Fact]
    public void 门窗连接件_连接点数扰动()
    {
        // perturb.json：TB 24→987 → 12843.75/987 → round3 13.013
        Assert.Equal(13.013, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { ConnectorCount = 987 }).FrameSide!.PointForceN);
        // 连接片厚 987 → 3.7·987·4·205 = 2994558（0x1408063）
        Assert.Equal(2994558.0, DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { PlateThicknessMm = 987 }).StructCapacityN);
    }

    [Fact]
    public void 门窗连接件_模式开关三态()
    {
        // 不计算连接件：无任何输出（probe4a var=0 实证）
        var n = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { CalcConnector = false });
        Assert.False(n.Calculated);
        Assert.Equal("", DoorConnectorEngine.FormatBook(n, 1.37));

        // 卡槽连接：边框块跳过（kacao 输出仅剩玻璃行+结构块）
        var k = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { ScrewConnection = false });
        Assert.Null(k.FrameSide);
        Assert.Equal(535.156, k.StructPointForceN);
        Assert.Equal(4551.0, k.StructCapacityN);
        Assert.DoesNotContain("门窗边框", DoorConnectorEngine.FormatBook(k, 1.37));
        Assert.Contains("连接片的抗剪强度设计值:4551N", DoorConnectorEngine.FormatBook(k, 1.37));
    }

    [Fact]
    public void 门窗连接件_有钢附框两块四值逐位()
    {
        // probe4a framed 实采：A 设计值 2596.28（钉固定自攻自钻3.5：1134→1346恒定）、
        // B 设计值 1139.83（钉径随附框规格变 3.5..5.5：1232.795/1301.343/1435.858/1589.136）
        var r = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { HasSteelFrame = true });
        var a = r.SteelFrameToWallA!;
        Assert.Equal(2596.28, a.WallCapacityN);
        Assert.Equal(1346.0, a.FastenerCapacityN);
        Assert.True(a.WallOk);
        Assert.True(a.FastenerOk);   // 535.156 ≤ 1346
        var b = r.SteelFrameToWallB!;
        Assert.Equal(1139.83, b.WallCapacityN);
        Assert.Equal(1346.0, b.FastenerCapacityN);
        // B 设计值随附框钉径（framed_v1..v4 逐位）
        double[] bExp = { 1232.795, 1301.343, 1435.858, 1589.136 };
        double[] ds = { 3.9, 4.2, 4.8, 5.5 };
        for (int i = 0; i < 4; i++)
        {
            var ri = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
            { HasSteelFrame = true, SteelFrameScrewDiameter = ds[i] });
            Assert.Equal(bExp[i], ri.SteelFrameToWallB!.WallCapacityN);
            // A 恒定（附框规格不影响 A 逐位实证）
            Assert.Equal(2596.28, ri.SteelFrameToWallA!.WallCapacityN);
        }
        // 附框类型=自攻自钻（framed_t1）：B 文案标签变、数值不变
        Assert.Contains("自攻自钻螺钉", DoorConnectorEngine.FormatBook(r, 1.37));
    }

    [Fact]
    public void 门窗连接件_外片加厚地震链不变输出()
    {
        // probe4a t10_conn：wk 仍 1.37（玻璃预演 wk 与玻璃厚无关），
        // Gk_area=0（门窗线单独，幕墙线未预演）时输出与基线一致（probe4a/6/8 实证）。
        var r = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { SeismicAlphaMax = 0.0, SeismicAreaNmm2 = 0.0 });
        Assert.Equal(0.0, r.SeismicShearN);
        Assert.Equal(10275.0, r.TotalShearN);
    }

    [Fact]
    public void 门窗连接件_地震非0态_probe10逐位()
    {
        // probe10（先幕墙立柱预演写 Gk_area=0.0004，再门窗；上海 αmax=0.08）：
        // wk=0.001674（上海 550 风压）→ 风切 round3(1.5·0.001674·2500·2000)=12555；
        // 地震 round3(7·0.08·0.0004·2500·2000·0.5)=560；V=13115 →
        // 单点 round3(round3(1.25·13115/24)/1)=683.073（V31 输出逐位）。
        var r = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input
        { WkNmm2 = 0.001674, SeismicAlphaMax = 0.08, SeismicAreaNmm2 = 0.0004 });
        Assert.Equal(12555.0, r.WindShearN);
        Assert.Equal(560.0, r.SeismicShearN);
        Assert.Equal(13115.0, r.TotalShearN);
        Assert.Equal(683.073, r.FrameSide!.PointForceN);
    }

    [Fact]
    public void 门窗连接件_输出书含V31原文关键串()
    {
        var r = DoorConnectorEngine.Compute(new DoorConnectorEngine.Input());
        var s = DoorConnectorEngine.FormatBook(r, 1.37);
        Assert.Contains("玻璃部分风荷载标准值为:1.37KPa", s);
        Assert.Contains("被连接的薄壁件抗剪强度计算值:535.156N", s);
        Assert.Contains("被连接的薄壁件抗剪强度设计值:972N", s);
        // V31 原文：快固件两行带前缀，且边框块前缀**不带「门窗」**二字（probe4a base 逐位）
        Assert.Contains("连接片与边框连接处单个抽芯铆钉(开口10/12级)的最大抗剪承载力设计值:400N", s);
        Assert.Contains("连接片与边框连接处单个抽芯铆钉(开口10/12级)的实际受力为:535.156N", s);
        Assert.Contains("连接片与建筑结构连接处:", s);
        Assert.Contains("连接片的抗剪强度设计值:4551N", s);
        Assert.Contains("不满足要求(×)", s);   // 铆钉项
        Assert.Contains("满足要求(√)", s);
    }
}
