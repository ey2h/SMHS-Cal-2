using System;
using System.Globalization;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Calc.Data;

/// <summary>
/// CW19 物理性能计算页的 RichText 输出文案（逐字复刻 V31 实机）。
/// 来源：2026-10-28 T12b/T12c 实机抓取 local-data/recon/v31runs/t12b_cw19_calc.json + t12c_insul.json。
/// 覆盖：139 幕墙抗风压 / 149 门窗抗风压 / 140 幕墙水密 / 150 门窗水密 / 137 空气声隔声。
/// 模板统一 LF，输出时转 CRLF（V31 RichText 为 CRLF）。
/// </summary>
public static class PhysicalPerformanceOutput
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const string WindResistanceTemplate = """
建筑{{subject}}抗风压性能等级的计算

工程所在地区：{{region}}地区
风荷载标准值为：{{wk}}KPa 

    {{subject}}抗风压性能是指该结构在与其相垂直的风压作用下，保持正常使用功能，不发生任何损坏的能力。
    {{subject}}抗风压性能指标应根据其所受的风荷载标准值Wk确定，其指标值不应该低于Wk，且不应小于1.0KPa，Wk的计算应符合GB50009的规定。
    幕墙和门窗的抗风压性能等级分级，应按《建筑幕墙、门窗通用技术条件》GB/T31433-2015表2进行
                                            表2  抗风压性能分级 
    ┌──────┬────┬────┬────┬────┬────┬────┬────┬────┬────┐
　　│   分级     │　1     │    2   │   3    │    4   │   5    │    6   │   7    │   8    │   9    │
　  ├──────┼────┼────┼────┼────┼────┼────┼────┼────┼────┤
　  │分级指标值  │1.0≤P3 │1.5≤P3 │2.0≤P3 │2.5≤P3 │3.0≤P3 │3.5≤P3 │4.0≤P3 │4.5≤P3 │5.0≤P3 │
    │  P3(KPa)   │  <1.5  │   <2.0 │  <2.5  │  <3.0  │  <3.5  │  <4.0  │  <4.5  │  <5.0  │        │
    ├──────┴────┴────┴────┴────┴────┴────┴────┴────┴────┤
    │ 注：第9级应在分级后同时注明具体分级指标值                                                            │
    └───────────────────────────────────────────────────┘

    按如上规定，在本例中：
        {{cmp}}
    所以抗风压性能为：{{grade}}级
""";

    private const string WallWaterTightnessTemplate = """
建筑幕墙水密性能指标计算
一、基本参数：

工程所在地区：{{region}}
计算高度为：{{height}}m
建筑物地面类型分类：{{terrain}}类地形
是否为热带风暴及台风袭击地区：{{typhoon}}
基本风压为：{{w0}}KPa

二、规范条文：

    幕墙和门窗的水密性能等级分级，应按《建筑幕墙、门窗通用技术条件》GB/T31433-2015表16进行！
    幕墙的水密性能以严重渗漏压力差值的一前级压力差值△P为分级依据，其分级指标应符合表17的规定。

                                表17  幕墙水密性能分级                    
    ┌───────┬────┬────┬─────┬─────┬─────┐
　　│   分级代号   │ 　1    │　 2    │　  3     │    4     │    5  　 │
　　├───┬───┼────┼────┼─────┼─────┼─────┤
　　│ 分级 │ 固定 │500≤△P│700≤△P│1000≤△P │1500≤△P │          │
　　│      │ 部分 │　<700  │ <1000  │  <1500   │  <2000   │△P≥2000 │
    │指标值├───┼────┼────┼─────┼─────┼─────┤
    │ △P　│可开启│250≤△P│350≤△P│ 500≤△P │ 700≤△P │          │
    │ (Pa) │ 部分 │  <350  │  <500  │   <700   │  <1000 　│△P≥1000 │
　　├───┴───┴────┴────┴─────┴─────┴─────┤

    JGJ102-2003的4.2.5条提出的水密性能设计计算方法如下（也可按5.1.2[《建筑幕墙》GB/T 21086-2007]计算）：
    1：受热带风暴和台风袭击的地区，水密性设计取值可以按下面公式计算，且固定部分取值不应小于1000Pa；
          P=1000μzμcW0
    上式中：
          P：水密性设计取值(Pa)；
          W0：基本风压(KPa)；
          μz：风压高度变化系数；
          μc：风力系数，可取1.2；
    2：其它地区，水密性可按上条的计算值的75%进行设计，且固定部分取值不应低于700Pa；
    3：可开启部分的水密性能等级宜与固定部分相同。
    说明：热带风暴级台风袭击地区是指GB50178-1993《建筑气候区划标准》图2.1.2中的ⅢA、ⅣA区。

三、工程计算：

1.风压高度变化系数的计算：
    根据不同场地类型,按《建筑结构荷载规范》条文说明部分8.2.1提供的公式计算：
　　　　A类场地：μz=1.284*(z/10)^0.24
　　　　B类场地：μz=1.000*(z/10)^0.30
　　　　C类场地：μz=0.544*(z/10)^0.44
　　　　D类场地：μz=0.262*(z/10)^0.60
    公式中的截断高度和梯度高度与计算阵风系数时相同，也就是：
        对A类场地：当z>300m时，取z=300m，当z<5m时，取z=5m；
        对B类场地：当z>350m时，取z=350m，当z<10m时，取z=10m；
        对C类场地：当z>450m时，取z=450m，当z<15m时，取z=15m；
        对D类场地：当z>550m时，取z=550m，当z<30m时，取z=30m；
对于{{terrain}}类地形，{{height}}m高度处风压高度变化系数：
    μz={{coef}}*(Z/10)^{{exp}}={{muz}}
2.水密性能计算：
    μc：风力系数，可取1.2；
    w0：基本风压值(MPa)，根据现行<<建筑结构荷载规范>>GB50009-2012附表E.5中数值采用，按重现期50年，取{{w0}}KPa；
    P=1000μzμcw0
     =1000×{{muz}}×1.2×{{w0}}
     ={{praw}}Pa
因为不是热带风暴和台风袭击区，所以P=0.75P={{pfinal}}Pa
    按如上计算结果分析，该幕墙的水密性性能等级为{{grade}}级；
    按规范要求，非热带风暴袭击地区水密性性能等级不应低于2级；
    因此本处结构实际水密性能应取：{{grade}}级！
""";

    private const string DoorWaterTightnessTemplate = """
建筑门窗水密性能指标计算
一、基本参数：

工程所在地区：{{region}}
计算高度为：{{height}}m
建筑物地面类型分类：{{terrain}}类地形
是否为热带风暴及台风袭击地区：{{typhoon}}
基本风压为：{{w0pa}}Pa

二、规范条文：

    幕墙和门窗的水密性能等级分级，应按《建筑幕墙、门窗通用技术条件》GB/T31433-2015表16进行！
    门窗的水密性能以严重渗漏压力差值的一前级压力差值△P为分级依据，其分级指标应符合表16的规定。

                               表16  门窗水密性能分级表                   
    ┌──────┬────┬────┬────┬────┬────┬────┐
　　│   分级代号 │ 　1    │　 2    │　  3   │    4   │   5    │   6    │
　　├──────┼────┼────┼────┼────┼────┼────┤
　　│ 分级指标值 │100≤△P│150≤△P│250≤△P│350≤△P│500≤△P│△P≥700│
　　│   △P（Pa）│　<150  │ <250   │  <350  │  <500  │ <700   │        │
　　├──────┴────┴────┴────┴────┴────┴────┤
  
    关于门窗水密性能的具体算法，相关规范的要求如下：
    对塑钢门窗：《塑料门窗工程技术规程》JGJ103-2008公式3.3.1-2为：
        P≥CμzW0
    式中：
        C：水密性能设计计算系数，受热带风暴和台风袭击的地区为0.5，其它地区取值为0.4；
        μz：风压高度变化系数，按《建筑结构荷载规范》GB50009取值；
        W0：基本风压，按《建筑结构荷载规范》GB50009取值；
    对铝合金门窗：《铝合金门窗工程技术规范》JGJ214-2010公式4.5.2给出的算法和JGJ103-2008公式3.3.1-2是一样的。
    说明：热带风暴级台风袭击地区是指GB50178-1993《建筑气候区划标准》图2.1.2中的ⅢA、ⅣA区。

三、工程计算：

1.风压高度变化系数的计算：
    μz：风压高度变化系数；
    根据不同场地类型,按《建筑结构荷载规范》条文说明部分8.2.1提供的公式计算：
        A类场地：μz=1.284*(z/10)^0.24
        B类场地：μz=1.000*(z/10)^0.30
        C类场地：μz=0.544*(z/10)^0.44
        D类场地：μz=0.262*(z/10)^0.60
    公式中的截断高度和梯度高度与计算阵风系数时相同，也就是：
        对A类场地：当z>300m时，取z=300m，当z<5m时，取z=5m；
        对B类场地：当z>350m时，取z=350m，当z<10m时，取z=10m；
        对C类场地：当z>450m时，取z=450m，当z<15m时，取z=15m；
        对D类场地：当z>550m时，取z=550m，当z<30m时，取z=30m；
对于{{terrain}}类地形，{{height}}m高度处风压高度变化系数：
    μz={{coef}}*(Z/10)^{{exp}}={{muz}}
2.水密性能分级指标的计算：
    P≥CμzW0
     ={{c}}×{{muz}}×{{w0pa}}
     ={{p}}Pa
        按实际计算，该门窗的水密性性能等级为{{grade}}级；
    此外，《铝合金门窗》GB8478-2020第5.6.2条规定：外门的水密性能值ΔP不应小于150Pa(不低于2级)，外窗的水密性能值ΔP不应小于250Pa(不低于3级)。
    请参看如上计算结果和介绍的规范要求，根据实际情况对门窗的水密性能进行定级！
""";

    private const string InsulationTemplate = """
*******************************************
    玻璃构件的空气声隔声量计算值:{{r}}dB
    组合墙结构的空气声隔声量计算值:{{rwall}}dB
-------------------------------------------
    按GB/T8485-2008《建筑外窗空气声隔声性能分级及其检测方法》,为:{{g8485}}级
    按GB/T50121-2005《建筑隔声评价标准》空气声隔声性能分级,为:{{g50121}}级
    按GB/T21086-2007《建筑幕墙》,为:{{g21086}}级
*******************************************
""";

    public static string WindResistance(bool isDoor, string region, double wk, int grade)
    {
        string wkText = wk.ToString("0.###", Inv);
        if (grade <= 0)
            return "wk = " + wkText + "KPa<1.0KPa      低于最低分级（规范要求 wk 不应小于 1.0KPa）";
        double lower = 1.0 + 0.5 * (grade - 1);
        string lowerText = lower.ToString("0.0", Inv);
        string cmp = grade >= 9
            ? "wk = " + wkText + "KPa≥" + lowerText + "KPa"
            : (1.0 + 0.5 * grade).ToString("0.0", Inv) + "KPa>wk = " + wkText + "KPa≥" + lowerText + "KPa";
        return WindResistanceTemplate
            .Replace("{{subject}}", isDoor ? "门窗" : "幕墙")
            .Replace("{{region}}", region)
            .Replace("{{wk}}", wkText)
            .Replace("{{cmp}}", cmp)
            .Replace("{{grade}}", grade.ToString(Inv))
            .Replace("\n", "\r\n");
    }

    public static string WallWaterTightness(string region, double height, TerrainCategory terrain, bool typhoon, double w0Kpa)
    {
        double muz = WindLoadEngine.MuZ(terrain, height);
        double pRawRaw = 1000.0 * muz * WaterTightnessEngine.MuC * w0Kpa;
        double pRaw = WindLoadEngine.VbaRound(pRawRaw, 1);
        double pFinalRaw = typhoon ? pRawRaw : pRawRaw * 0.75;
        double pFinal = WindLoadEngine.VbaRound(pFinalRaw, 1);
        int grade = WaterTightnessEngine.Grade(pFinal);
        return WallWaterTightnessTemplate
            .Replace("{{region}}", region)
            .Replace("{{height}}", height.ToString("0.###", Inv))
            .Replace("{{terrain}}", TerrainOf(terrain))
            .Replace("{{typhoon}}", typhoon ? "是" : "否")
            .Replace("{{w0}}", w0Kpa.ToString("0.###", Inv))
            .Replace("{{coef}}", CoefOf(terrain))
            .Replace("{{exp}}", ExpOf(terrain))
            .Replace("{{muz}}", muz.ToString("0.0000", Inv))
            .Replace("{{praw}}", pRaw.ToString("0.###", Inv))
            .Replace("{{pfinal}}", pFinal.ToString("0.###", Inv))
            .Replace("{{grade}}", grade.ToString(Inv))
            .Replace("\n", "\r\n");
    }

    public static string DoorWaterTightness(string region, double height, TerrainCategory terrain, bool typhoon, double w0Pa)
    {
        double muz = WindLoadEngine.MuZ(terrain, height);
        double c = typhoon ? 0.5 : 0.4;
        double p = WindLoadEngine.VbaRound(c * muz * w0Pa, 1);
        int grade = DoorWaterTightnessEngine.Grade(p);
        return DoorWaterTightnessTemplate
            .Replace("{{region}}", region)
            .Replace("{{height}}", height.ToString("0.###", Inv))
            .Replace("{{terrain}}", TerrainOf(terrain))
            .Replace("{{typhoon}}", typhoon ? "是" : "否")
            .Replace("{{w0pa}}", w0Pa.ToString("0.###", Inv))
            .Replace("{{coef}}", CoefOf(terrain))
            .Replace("{{exp}}", ExpOf(terrain))
            .Replace("{{muz}}", muz.ToString("0.0000", Inv))
            .Replace("{{c}}", c.ToString("0.0", Inv))
            .Replace("{{p}}", p.ToString("0.###", Inv))
            .Replace("{{grade}}", grade.ToString(Inv))
            .Replace("\n", "\r\n");
    }

    /// <summary>空气声隔声（137）：7 行模板。</summary>
    public static string Insulation(double r, double rWall, int g8485, int g50121, int g21086)
    {
        return InsulationTemplate
            .Replace("{{r}}", r.ToString("0.###", Inv))
            .Replace("{{rwall}}", rWall.ToString("0.###", Inv))
            .Replace("{{g8485}}", g8485.ToString(Inv))
            .Replace("{{g50121}}", g50121.ToString(Inv))
            .Replace("{{g21086}}", g21086.ToString(Inv))
            .Replace("\n", "\r\n");
    }

    private static string TerrainOf(TerrainCategory t) => t switch
    {
        TerrainCategory.A => "A", TerrainCategory.B => "B", TerrainCategory.C => "C", _ => "D"
    };

    private static string CoefOf(TerrainCategory t) => t switch
    {
        TerrainCategory.A => "1.284", TerrainCategory.B => "1.000", TerrainCategory.C => "0.544", _ => "0.262"
    };

    private static string ExpOf(TerrainCategory t) => t switch
    {
        TerrainCategory.A => "0.24", TerrainCategory.B => "0.30", TerrainCategory.C => "0.44", _ => "0.60"
    };
}
