using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 测试夹具：**一切下游屏的用例都必须先走这一步**。
///
/// <para>铁律（用户强调）：所有参数都是从第一个面板（基本参数输入）一路传到后面的，
/// 测试里也<b>不许</b>手工写 <c>s.WindStandardKpa = 1.285</c> 去伪造第一页结论。</para>
///
/// <para>这里的做法：真的 new 一个 <see cref="BasicParamsViewModel"/> 然后调
/// <see cref="BasicParamsViewModel.Recalculate"/>（= 界面上点「预演/下一步」走的那条链），
/// 于是 wk 的支承口径 / 面板口径、分格尺寸系数、门禁位 <c>WindStandardResolved</c>
/// 全部由第一页算出，下游屏拿到的才是真数据。</para>
///
/// <para>注意 <see cref="CalcSession.Default"/> 是静态单例：<c>CalcWizardViewModel</c>
/// 每个实例都持有它，所以同一用例内不要建两个向导再分别断言。</para>
/// </summary>
internal static class FirstPage
{
    /// <summary>V31 启动默认参数下的支承口径 wk（C 类 / 北京 0.45kPa / 标高 100 / 50 年 / 墙面区 1.0）。</summary>
    public const double ExpectedSupportWk = 1.285;

    /// <summary>同参数下的面板口径 wk（μsl=1.0 不折减 ψ）。</summary>
    public const double ExpectedPanelWk = 1.37;

    /// <summary>建向导并让第一页真算一遍，返回 (会话, 向导)。</summary>
    public static (CalcSession Session, CalcWizardViewModel Wizard) Run()
    {
        var wiz = new CalcWizardViewModel();
        var basic = new BasicParamsViewModel(wiz.Session, wiz);
        basic.Recalculate();          // ← 真跑第一页计算链，不是赋值
        return (wiz.Session, wiz);
    }
}
