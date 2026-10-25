using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「幕墙计算-横梁计算」屏 ViewModel（严格 MVVM：View 只做绑定，全部业务在此）。
///
/// <para>
/// **数据来源（全部来自参数库 / 共享会话，View 不接触任何数值）**：
/// <list type="bullet">
/// <item>跨度 B、自重高度 H、wk → <see cref="CalcSession"/>（第一页算出后传入，不在此重算风荷载）</item>
/// <item>自重面荷载 g → 参数库「幕墙自重参数」计算横梁时自重（<see cref="CalcSession.DeadLoadGkTransom"/>）</item>
/// <item>αmax → 参数库「地区参数」表 αmax 列（<see cref="CalcSession.AlphaMax"/>）</item>
/// <item>型材截面 → 参数库「横框」表（<see cref="ParamLibrary.BeamProfiles"/>）</item>
/// <item>f / fv → 材质牌号设计值（<see cref="MaterialAllowables"/>）</item>
/// <item>全部算式 → <see cref="HorizontalBeamEngine"/>（每个系数带 V31 地址，见该类注释）</item>
/// </list>
/// </para>
/// </summary>
public partial class BeamCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public BeamCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        Profiles = ParamLibrary.BeamProfiles;
        _selectedProfile = Profiles.FirstOrDefault();
        Session.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CalcSession.WindStandardKpa):
                case nameof(CalcSession.BeamLeftBayMm):
                case nameof(CalcSession.BeamRightBayMm):
                case nameof(CalcSession.BeamUpperGridHeightMm):
                case nameof(CalcSession.BeamMaterial):
                case nameof(CalcSession.RegionName):
                    Invalidate("上游参数已变化");
                    OnPropertyChanged(nameof(MaterialAllowText));
                    break;
            }
        };
    }

    // ---------------- 输入（绑定到共享会话） ----------------

    /// <summary>型材表（参数库「横框」）。</summary>
    public IReadOnlyList<BeamProfile> Profiles { get; }

    [ObservableProperty] private BeamProfile? _selectedProfile;

    /// <summary>型材检索关键字（V31「←请在此输入待查找型材代号或其关键字」）。</summary>
    [ObservableProperty] private string _profileKeyword = "";

    /// <summary>可选材质牌号（参数库材料表；默认 6063-T5 → f=90 / fv=55，与 V31 横梁页默认一致）。</summary>
    public IReadOnlyList<string> MaterialOptions => MaterialAllowables.Options;

    /// <summary>
    /// 自重按两点集中力（V31 横梁页「玻璃自重为集中力」）。
    /// 只有玻璃幕墙可用；石材类的「按集中力模型」是双向集中力，属另一套模板（未复刻）。
    /// </summary>
    [ObservableProperty] private bool _concentratedGravity;

    /// <summary>受力点到横梁与立柱连接点的距离 a（mm）。V31 默认 150。</summary>
    [ObservableProperty] private double _pointDistanceMm = 150;

    /// <summary>集中力参数是否可编辑（仅在勾选「玻璃自重为集中力」时启用，与 V31 en=False 行为对齐）。</summary>
    public bool PointDistanceEnabled => ConcentratedGravity;

    partial void OnConcentratedGravityChanged(bool value)
    {
        OnPropertyChanged(nameof(PointDistanceEnabled));
        Invalidate("自重模型已切换");
    }

    partial void OnPointDistanceMmChanged(double value) => Invalidate("受力点距离已改");

    /// <summary>当前材质的设计值摘要（f / fv）。</summary>
    public string MaterialAllowText
    {
        get
        {
            var a = MaterialAllowables.Get(Session.BeamMaterial);
            return $"f={a.Bending:0.###} MPa，fv={a.Shear:0.###} MPa";
        }
    }

    /// <summary>预演是否可用（无 wk 时禁用，并给出原因 —— 与立柱屏同一套做法）。</summary>
    public bool PreviewEnabled => Session.WindStandardKpa > 0 && SelectedProfile is not null;

    /// <summary>不能用预演时的原因（空 = 可用）。</summary>
    public string PreviewBlockedReason
    {
        get
        {
            if (Session.WindStandardKpa <= 0) return "上游未算出风荷载标准值，请先在第一页点「计算荷载」";
            if (SelectedProfile is null) return "请先在型材表中选择一行";
            return "";
        }
    }

    /// <summary>状态栏是否处于告警态（参数失效后尚未重新预演）。</summary>
    [ObservableProperty] private bool _statusIsWarning;

    /// <summary>载入参数来源说明（参数库路径 / 内置快照）。</summary>
    public string ParamSourceNote => ParamLibrary.SourceNote;

    /// <summary>输入摘要（界面顶部）。</summary>
    public string InputSummary =>
        $"跨度 B = max({Session.BeamLeftBayMm:0.###}, {Session.BeamRightBayMm:0.###}) = {Session.BeamSpanMm:0.###} mm"
        + $"｜自重高度 H = {Session.BeamUpperGridHeightMm:0.###} mm（上分格，不平均）"
        + $"｜wk = {Session.WindStandardKpa:0.###} kPa"
        + $"｜g = {Session.DeadLoadGkTransom:0.###} N/m²｜αmax = {Session.AlphaMax:0.###}";

    /// <summary>截面参数摘要（界面中部，选中型材后刷新）。</summary>
    public string SectionSummary
    {
        get
        {
            var p = SelectedProfile;
            if (p is null) return "（未选型材）";
            return $"Ix={p.Ix:0.###} mm⁴｜Iy={p.Iy:0.###} mm⁴｜"
                 + $"Wx={p.WxBending:0.###}（min of {p.Wx1:0.###}/{p.Wx2:0.###}）｜"
                 + $"Wy={p.WyBending:0.###}（min of {p.Wy1:0.###}/{p.Wy2:0.###}）｜"
                 + $"Sx={p.Sx:0.###}｜Sy={p.Sy:0.###}｜tx={p.Tx:0.###}｜ty={p.Ty:0.###} mm";
        }
    }

    // ---------------- 预演结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));
    private static readonly IBrush ColorNote = new SolidColorBrush(Color.Parse("#6B6B6B"));

    public string PlaceholderText =>
        HasResults ? "" : "点「预演」按参数库型材与第一页传入的 wk / 分格尺寸计算横梁。";

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(InputSummary));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    [ObservableProperty] private string _statusText = "";

    partial void OnSelectedProfileChanged(BeamProfile? value)
    {
        OnPropertyChanged(nameof(SectionSummary));
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
    }

    // ---------------- 命令 ----------------

    /// <summary>
    /// 「预演」：按 V31 输出的 12 个量逐条生成（顺序 1:1 对齐 <c>v31runs/py_beam1.txt</c>）。
    /// </summary>
    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled)
        {
            StatusIsWarning = true;
            StatusText = PreviewBlockedReason;
            return;
        }
        var p = SelectedProfile!;

        var f = MaterialAllowables.Bending(Session.BeamMaterial);
        var fv = MaterialAllowables.Shear(Session.BeamMaterial);
        var g = HorizontalBeamEngine.GravityFromLibrary(Session.DeadLoadGkTransom);

        var r = HorizontalBeamEngine.Compute(
            Session.WindStandardKpa, Session.BeamSpanMm, Session.BeamUpperGridHeightMm,
            g, Session.AlphaMax, f, p,
            concentratedGravity: ConcentratedGravity,
            pointDistanceMm: PointDistanceMm);

        Results.Clear();
        Add("************************************", ColorRule);
        Add($"风荷载标准值为:{Session.WindStandardKpa:0.###}KPa", ColorNormal);
        Add("************************************", ColorRule);

        Add($"抗弯矩预选Wx:{r.WxRequired:0.###}mm3", ColorNormal);
        Add($"选用型材的抗弯矩Wx:{p.WxBending:0.###}mm3", ColorNormal);
        Add(r.WxRequired <= p.WxBending ? "满足要求(√)" : "不满足要求(×)",
            r.WxRequired <= p.WxBending ? ColorOk : ColorFail);
        Add("——————————————————", ColorRule);

        Add($"抗弯矩预选Wy:{r.WyRequired:0.###}mm3", ColorNormal);
        Add($"选用型材的抗弯矩Wy:{p.WyBending:0.###}mm3", ColorNormal);
        Add(r.WyRequired <= p.WyBending ? "满足要求(√)" : "不满足要求(×)",
            r.WyRequired <= p.WyBending ? ColorOk : ColorFail);
        Add("************************************", ColorRule);

        Add($"惯性矩Ix预选:{r.IxRequired:0.###}mm4", ColorNormal);
        Add($"选用型材的惯性矩Ix:{p.Ix:0.###}mm4", ColorNormal);
        Add($"实际垂直向挠度计算值为：{r.DeflectionV:0.###}mm", ColorNormal);
        Add($"规范要求的垂直向挠度值为：{r.LimitV:0.###}mm", ColorNormal);
        Add(r.DeflectionV <= r.LimitV ? "满足要求(√)" : "不满足要求(×)",
            r.DeflectionV <= r.LimitV ? ColorOk : ColorFail);
        Add("——————————————————", ColorRule);

        Add($"惯性矩Iy预选:{r.IyRequired:0.###}mm4", ColorNormal);
        Add($"选用型材的惯性矩Iy:{p.Iy:0.###}mm4", ColorNormal);
        Add($"实际水平向挠度计算值为：{r.DeflectionH:0.###}mm", ColorNormal);
        Add($"规范要求的水平向挠度值为：{r.LimitH:0.###}mm", ColorNormal);
        Add(r.DeflectionH <= r.LimitH ? "满足要求(√)" : "不满足要求(×)",
            r.DeflectionH <= r.LimitH ? ColorOk : ColorFail);
        Add("************************************", ColorRule);

        Add($"抗弯强度计算值:{r.Sigma:0.###}MPa", ColorNormal);
        Add($"选用型材的抗弯强度设计值为:{f:0.###}MPa", ColorNormal);
        Add(r.Sigma <= f ? "满足要求(√)" : "不满足要求(×)", r.Sigma <= f ? ColorOk : ColorFail);
        Add("——————————————————", ColorRule);

        Add($"抗剪强度计算值tx:{r.TauH:0.###}MPa", ColorNormal);
        Add($"选用型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
        Add(r.TauH <= fv ? "满足要求(√)" : "不满足要求(×)", r.TauH <= fv ? ColorOk : ColorFail);
        Add($"抗剪强度计算值ty:{r.TauV:0.###}MPa", ColorNormal);
        Add(r.TauV <= fv ? "满足要求(√)" : "不满足要求(×)", r.TauV <= fv ? ColorOk : ColorFail);

        // 取证备注（不参与判定，便于与 V31 对拍）
        Add("", ColorNote);
        Add($"[取证] r=min(B,H)={Math.Min(Session.BeamSpanMm, Session.BeamUpperGridHeightMm):0.###}，"
            + $"u=r/(2B)={r.U:0.0000}，弯矩系数=(3−(r/B)²)/24={(r.My / (r.Qy * Session.BeamSpanMm * Session.BeamSpanMm)):0.00000}",
            ColorNote);

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = false;
        StatusText = $"预演完成：型材 {p.Code}"
                   + (ConcentratedGravity ? $"（自重点集中力，a={PointDistanceMm:0.###}mm）" : "")
                   + $"，{Session.WindStandardSource}，参数库 {ParamSourceNote}";
    }

    /// <summary>型材检索（V31「快速查找型材」按钮）。</summary>
    [RelayCommand]
    private void FindProfile()
    {
        var hit = ParamLibrary.FindBeamProfile(ProfileKeyword);
        if (hit is null) return;
        SelectedProfile = hit;
        StatusText = $"定位型材：{hit.Code}";
    }

    [RelayCommand] private void Prev() => _wizard.GoColumnCalc();
    [RelayCommand] private void Next() => _wizard.StepFromBeamNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
