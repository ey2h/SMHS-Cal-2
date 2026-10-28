using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 门窗中竖挺/中横挺杆件本体对拍：
/// V31 实机 door_body_traces.json（6063-T5 基线逐位）、
/// door_body_perturbQ235.json（材质扰动：设计值/弹性模量/挠度逐位联动）。
/// </summary>
public class DoorMullionEngineTests
{
    [Fact]
    public void 中竖挺_默认基线6063T5()
    {
        var r = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput());
        Assert.Equal(1.333, r.WkKpa);             // 0x137D40C
        Assert.Equal(0.4, r.Qk1);                 // 0x1376F86→round 0x1376FA2
        Assert.Equal(0.6, r.Qd1);                 // 0x1377078
        Assert.Equal(1.0, r.Qk2);                 // 0x1377873
        Assert.Equal(1.5, r.Qd2);                 // 0x1377949
        Assert.Equal(183000.0, r.M1);             // 0x1378593
        Assert.Equal(339375.0, r.M2);             // 0x137960C
        Assert.Equal(130.171, r.Sigma);           // 0x137A9E3（输出「130.171MPa」）
        Assert.Equal(390.0, r.V1);                // 0x1378A13
        Assert.Equal(637.5, r.V2);                // 0x1379A8C
        Assert.Equal(7.316, r.Tau);               // 0x137D098
        Assert.Equal(2.982, r.F1);                // 0x137B955
        Assert.Equal(5.362, r.F2);                // 0x137C6AF
        Assert.Equal(8.344, r.Defl);              // 输出「8.344mm」
        Assert.Equal(16.0, r.DeflLimit);          // 0x137C9B0
        Assert.False(r.SigmaOk);                  // 130.171 > 90 → 不满足(×)
        Assert.True(r.DeflOk);
        Assert.True(r.TauOk);
    }

    [Fact]
    public void 中竖挺_材质Q235扰动()
    {
        // door_body_perturbQ235.json：E=206000 后挠度 2.835（0x137B80C ÷206000）
        var r = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput
        {
            FMpa = 215.0,
            FvMpa = 125.0,
            EMpa = 206000.0,
        });
        Assert.Equal(130.171, r.Sigma);           // σ 不变（输出逐位）
        Assert.Equal(2.835, r.Defl);              // 8.344 → 2.835 联动
        Assert.Equal(7.316, r.Tau);               // τ 不变
        Assert.True(r.SigmaOk);                   // 130.171 ≤ 215 → 满足(√)
        Assert.True(r.TauOk);                     // 7.316 ≤ 125
    }

    [Fact]
    public void 中竖挺_输出书逐字()
    {
        var r = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput());
        string s = DoorMullionEngine.FormatMullionBook(r);
        Assert.Contains("计算杆件时风荷载标准值为:1.333KPa", s);
        Assert.Contains("中竖挺抗弯强度计算值:130.171MPa", s);
        Assert.Contains("中竖挺抗弯强度设计值:90MPa", s);
        Assert.Contains("中竖挺挠度计算值:8.344mm", s);
        Assert.Contains("中竖挺挠度限值:16mm", s);
        Assert.Contains("中竖挺抗剪强度计算值:7.316MPa", s);
        Assert.Contains("中竖挺抗剪强度设计值:55MPa", s);
        Assert.Contains("不满足要求(×)", s);
    }

    [Fact]
    public void 中横挺_默认基线6063T5()
    {
        var r = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput());
        Assert.Equal(1.333, r.WkKpa);             // 0x139A0CA
        Assert.Equal(0.433, r.Qk1);               // 0x1392040
        Assert.Equal(0.65, r.Qd1);                // 0x1392116
        Assert.Equal(1.0, r.Qk2);                 // 0x1392911
        Assert.Equal(1.5, r.Qd2);                 // 0x13929E7
        Assert.Equal(171369.792, r.M1);           // 0x139355D
        Assert.Equal(281250.0, r.M2);             // 0x1393D46
        Assert.Equal(110.368, r.Sigma);           // 0x1396435
        Assert.Equal(381.875, r.V1);              // 0x13939DD
        Assert.Equal(562.5, r.V2);                // 0x1393FE3
        Assert.Equal(9.876, r.TauHoriz);          // 0x13999AE
        Assert.Equal(2.653, r.F1);                // 0x139752A
        Assert.Equal(4.234, r.F2);                // 0x13979C1
        Assert.Equal(6.887, r.DeflHoriz);         // 输出「6.887mm」
        Assert.Equal(0.0, r.DeflVert);            // 0x139D5A 默认 0
        Assert.Equal(0.0, r.TauVert);
        Assert.Equal(15.0, r.DeflLimit);          // 0x1398585
        Assert.False(r.SigmaOk);                  // 110.368 > 90 → ×
        Assert.True(r.DeflHorizOk);
        Assert.True(r.TauHorizOk);
    }

    [Fact]
    public void 中横挺_材质Q235扰动()
    {
        var r = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
        {
            FMpa = 215.0,
            FvMpa = 125.0,
            EMpa = 206000.0,
        });
        Assert.Equal(110.368, r.Sigma);           // 不变
        Assert.Equal(2.341, r.DeflHoriz);         // 6.887 → 2.341 联动
        Assert.Equal(9.876, r.TauHoriz);          // 不变
        Assert.True(r.SigmaOk);                   // → 满足(√)
    }

    [Fact]
    public void 中横挺_输出书逐字()
    {
        var r = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput());
        string s = DoorMullionEngine.FormatTransomBook(r);
        Assert.Contains("中横挺抗弯强度计算值:110.368MPa", s);
        Assert.Contains("中横挺抗弯强度设计值:90MPa", s);
        Assert.Contains("中横挺水平方向挠度计算值:6.887mm", s);
        Assert.Contains("中横挺水平方向挠度限值:15mm", s);
        Assert.Contains("中横挺垂直方向挠度计算值:0mm", s);
        Assert.Contains("中横挺水平方向抗剪强度计算值:9.876MPa", s);
        Assert.Contains("中横挺垂直方向抗剪强度计算值:0MPa", s);
        Assert.Contains("中横挺垂直方向抗剪强度设计值:55MPa", s);
    }

    [Fact]
    public void 材料表_全10项遍历逐位()
    {
        // door_mullion_matrix.json：逐材质点击预演实采。
        // f/fv = 输出「设计值」行；E = 挠度链 0x137B80C Div 除数直读；
        // 挠度逐位联动（6061-T4 与 6063-T5 全等=90/55/70000 输出无 diff 实证）。
        var expect = new (string Name, double F, double Fv, double E, double DeflV, double DeflH)[]
        {
            ("6061-T4", 90.0, 55.0, 70000.0, 8.344, 6.887),
            ("6061-T6", 200.0, 115.0, 70000.0, 8.344, 6.887),
            ("6063-T5", 90.0, 55.0, 70000.0, 8.344, 6.887),
            ("6063-T6", 150.0, 85.0, 70000.0, 8.344, 6.887),
            ("6063A-T5", 135.0, 75.0, 70000.0, 8.344, 6.887),
            ("6063A-T6", 160.0, 90.0, 70000.0, 8.344, 6.887),
            ("6060-T66", 135.0, 75.0, 70000.0, 8.344, 6.887),
            ("塑钢(仅塑材部分)", 37.0, 18.5, 2200.0, 265.506, 219.134),
            ("Q235", 215.0, 125.0, 206000.0, 2.835, 2.341),
            ("玻纤增强聚氨酯", 500.0, 40.0, 40000.0, 14.603, 12.052),
        };
        foreach (var mm in expect)
        {
            Assert.Equal((mm.Name, mm.F, mm.Fv, mm.E),
                         Array.Find(DoorMullionEngine.Materials,
                                    x => x.Name == mm.Name));
            var rv = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput
            { FMpa = mm.F, FvMpa = mm.Fv, EMpa = mm.E });
            Assert.Equal(mm.DeflV, rv.Defl);          // 0x137C7BF Add 后 round3
            Assert.Equal(130.171, rv.Sigma);          // σ 与材质无关（输出逐位）
            var rh = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
            { FMpa = mm.F, FvMpa = mm.Fv, EMpa = mm.E });
            Assert.Equal(mm.DeflH, rh.DeflHoriz);
        }
    }

    [Fact]
    public void 挠度限值_三模式四值逐位()
    {
        // door_modes_probe2 / door_mullion_matrix 实采（L=1600 / L2=1500）：
        // 自动 → V=16、H=15（÷100）
        var a = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput());
        Assert.Equal(16.0, a.DeflLimit);
        var ah = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput());
        Assert.Equal(15.0, ah.DeflLimit);
        // 人工给定一致 150 → V=round3(1600/150)=10.667、H=round3(1500/150)=10
        var m1 = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput
        { LimitMode = DoorMullionEngine.DeflLimitMode.ManualSame, LimitDivisor = 150 });
        Assert.Equal(10.667, m1.DeflLimit);
        var m1h = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
        { LimitMode = DoorMullionEngine.DeflLimitMode.ManualSame, LimitDivisor = 150 });
        Assert.Equal(10.0, m1h.DeflLimit);
        Assert.Equal(10.0, m1h.DeflLimitVert);
        // 人工给定不一致 → H=10、V=round3(1500/500)=3（「垂直限值:3mm」逐位）
        var m2h = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
        { LimitMode = DoorMullionEngine.DeflLimitMode.ManualDiff, LimitDivisor = 150 });
        Assert.Equal(10.0, m2h.DeflLimit);
        Assert.Equal(3.0, m2h.DeflLimitVert);
        Assert.Contains("中横挺垂直方向挠度限值:3mm",
                        DoorMullionEngine.FormatTransomBook(m2h));
    }

    [Fact]
    public void 材料表_双证条目()
    {
        Assert.Contains(("6063-T5", 90.0, 55.0, 70000.0), DoorMullionEngine.Materials);
        Assert.Contains(("Q235", 215.0, 125.0, 206000.0), DoorMullionEngine.Materials);
    }

    [Fact]
    public void 地震与垂直自重_非0态_probe11逐位()
    {
        // door_probe11_hvert（先幕墙立柱预演写全局槽→上海地区 αmax=0.08、
        // 水平地震面荷载 Gk_area=0.0004（竖梃链）/0.0003（横梃垂直 qgk_area）；
        // 门窗线单独预演时这些槽=0（probe9 逐位），幕墙线预演后才非 0（probe10/11 逐位））。
        // ── 竖梃（上海 wk=0.001629；qd1=0.734+0.0335=0.768、qd2=1.833+0.084=1.917）──
        var mv = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput
        { WkRodNmm2 = 0.001629, SeismicAlphaMax = 0.08, GkAreaNmm2 = 0.0004 });
        Assert.Equal(0.768, mv.Qd1);
        Assert.Equal(1.917, mv.Qd2);
        Assert.Equal(166.449, mv.Sigma);
        Assert.Equal(10.199, mv.Defl);      // 挠度不含地震（3.646+6.553）
        Assert.Equal(9.356, mv.Tau);
        // ── 横梃（qd1=round6(0.794+0.028)=0.822、qd2=round6(1.833+0.063)=1.896；
        //    qgk_area=0.0003 → qk线=0.195、qd线=0.254 → σ=139.531+25.836=165.367）──
        var mt = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
        { WkRodNmm2 = 0.001629, SeismicAlphaMax = 0.08, GkAreaNmm2 = 0.0003, QgkAreaNmm2 = 0.0003 });
        Assert.Equal(0.822, mt.Qd1);
        Assert.Equal(1.896, mt.Qd2);
        Assert.Equal(165.367, mt.Sigma);
        Assert.Equal(8.416, mt.DeflHoriz);
        Assert.Equal(1.842, mt.DeflVert);   // 5·0.195·L⁴/(384·E·Ix2)
        Assert.Equal(12.486, mt.TauHoriz);
        Assert.Equal(1.883, mt.TauVert);    // 190.125·2962/(99690·3)
        Assert.False(mt.SigmaOk);           // 165.367 > 90 →「不满足要求(×)」逐位
        Assert.True(mt.DeflVertOk);
        Assert.Contains("中横挺垂直方向挠度计算值:1.842mm", DoorMullionEngine.FormatTransomBook(mt));
        Assert.Contains("中横挺垂直方向抗剪强度计算值:1.883MPa", DoorMullionEngine.FormatTransomBook(mt));
        // 默认（门窗线单独、槽 0）→ 垂直链保持 0（probe9 逐位不受上海地区影响）
        var z = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
        { WkRodNmm2 = 0.001629, SeismicAlphaMax = 0.08 });
        Assert.Equal(0.0, z.DeflVert);
        Assert.Equal(0.0, z.TauVert);
    }
}
