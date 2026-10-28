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
/// 「幕墙计算-横梁计算（单元式）」屏 ViewModel。
///
/// <para>
/// 与构件式横梁屏是两个**独立模板**（V31 同一窗体按第一页「幕墙结构」切换）：
/// 构件式输出 12 个量；单元式无中横 17 个量、有中横追加中横框 5 个量（共 22 个）。
/// </para>
///
/// <para>
/// 数据来源全部是参数库 / 共享会话：上框「单元横框一」、下框「单元横框二」、
/// 中横「单元横框三（中横框）」、自重「幕墙自重参数」、αmax「地区参数」；
/// 算式在 <see cref="UnitBeamEngine"/>。
/// </para>
///
/// <para>
/// 输出文案与分隔符长度 1:1 复刻 V31：前 3 个分隔符 35 星，抗剪段起 36 星，
/// 中横抗弯前 36 星、其后 35 星 —— 见 <c>local-data/recon/v31runs/py_u_mid.txt</c>。
/// </para>
/// </summary>
public partial class UnitBeamCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public UnitBeamCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        TopProfiles = ParamLibrary.UnitBeamTop;
        BottomProfiles = ParamLibrary.UnitBeamBottom;
        MidProfiles = ParamLibrary.UnitBeamMid;
        _topProfile = TopProfiles.FirstOrDefault();
        _bottomProfile = BottomProfiles.FirstOrDefault();
        _midProfile = MidProfiles.FirstOrDefault();

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
                case nameof(CalcSession.IgnoreSeismic):
                case nameof(CalcSession.TransomMid):
                case nameof(CalcSession.TransomNone):
                    Invalidate("上游参数已变化");
                    OnPropertyChanged(nameof(MaterialAllowText));
                    OnPropertyChanged(nameof(BendingText));
                    OnPropertyChanged(nameof(ShearText));
                    break;
            }
        };
    }

    // ---------------- 输入 ----------------

    /// <summary>上横框型材（参数库「单元横框一」）。</summary>
    public IReadOnlyList<BeamProfile> TopProfiles { get; }

    /// <summary>下横框型材（参数库「单元横框二」）。</summary>
    public IReadOnlyList<BeamProfile> BottomProfiles { get; }

    /// <summary>中横框型材（参数库「单元横框三（中横框）」）。</summary>
    public IReadOnlyList<BeamProfile> MidProfiles { get; }

    [ObservableProperty] private BeamProfile? _topProfile;
    [ObservableProperty] private BeamProfile? _bottomProfile;
    [ObservableProperty] private BeamProfile? _midProfile;

    /// <summary>可选材质牌号（与构件式共用 MaterialAllowables）。</summary>
    public IReadOnlyList<string> MaterialOptions => MaterialAllowables.Options;

    /// <summary>当前材质设计值摘要。</summary>
    public string MaterialAllowText
    {
        get
        {
            var a = MaterialAllowables.Get(Session.BeamMaterial);
            return $"f={a.Bending:0.###} MPa，fv={a.Shear:0.###} MPa";
        }
    }

    /// <summary>抗弯强度设计值文本（V31 的 f 框）。</summary>
    public string BendingText => MaterialAllowables.Bending(Session.BeamMaterial).ToString("0.###");

    /// <summary>抗剪强度设计值文本（V31 的 fv 框）。</summary>
    public string ShearText => MaterialAllowables.Shear(Session.BeamMaterial).ToString("0.###");

    /// <summary>材质单选组（6 个 RadioButton 共享 Session.BeamMaterial）。</summary>
    [RelayCommand]
    private void SetMaterial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        Session.BeamMaterial = name;
        OnPropertyChanged(nameof(MaterialAllowText));
        OnPropertyChanged(nameof(BendingText));
        OnPropertyChanged(nameof(ShearText));
        Invalidate("材质已切换");
    }

    /// <summary>不做地震作用（复刻版修正 V31 该开关对横梁不生效的问题）。</summary>
    public bool IgnoreSeismic
    {
        get => Session.IgnoreSeismic;
        set => Session.IgnoreSeismic = value;
    }

    /// <summary>第一页「有中横结构 / 无中横结构」决定是否追加中横框 5 个量（V31 由上游决定，本屏只执行）。</summary>
    public bool HasMidBeam => Session.TransomMid;

    public string StructureText => HasMidBeam ? "有中横（追加中横框 5 个量）" : "无中横";

    public string InputSummary =>
        $"结构形式：单元式（上横框 + 下横框）"
        + $"｜横框结构：{StructureText}"
        + $"｜跨度 B = max({Session.BeamLeftBayMm:0.###}, {Session.BeamRightBayMm:0.###}) = {Session.BeamSpanMm:0.###} mm"
        + $"｜上分格高 H = {Session.BeamUpperGridHeightMm:0.###} mm"
        + $"｜wk = {Session.WindStandardKpa:0.###} kPa"
        + $"｜g = {Session.DeadLoadGkTransom:0.###} N/m²｜αmax = {Session.AlphaMax:0.###}"
        + (Session.IgnoreSeismic ? "（不考虑地震）" : "");

    public string SectionSummary
    {
        get
        {
            var t = TopProfile;
            var b = BottomProfile;
            if (t is null || b is null) return "（未选型材）";
            return $"上框 {t.Code}：Ix={t.Ix:0.###} Iy={t.Iy:0.###} Wx={t.WxBending:0.###} Wy={t.WyBending:0.###}"
                 + $"　下框 {b.Code}：Ix={b.Ix:0.###} Iy={b.Iy:0.###} Wx={b.WxBending:0.###} Wy={b.WyBending:0.###}";
        }
    }

    public string ParamSourceNote => ParamLibrary.SourceNote;

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    public string PlaceholderText =>
        HasResults ? "" : "点「预演」按单元式上下横框分别计算（自重只进上框，风载上下各半）。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    /// <summary>
    /// ★ 门禁看 <see cref="CalcSession.SupportWindReady"/>，不写 `WindStandardKpa &gt; 0`
    /// （后者初值 1.0，门禁永远为真，等于放行假数据）。
    /// </summary>
    public bool PreviewEnabled => Session.SupportWindReady && TopProfile is not null && BottomProfile is not null
                                  && (!HasMidBeam || MidProfile is not null);

    public string PreviewBlockedReason
    {
        get
        {
            if (!Session.SupportWindReady) return CalcSession.UpstreamNotReadyText;
            if (TopProfile is null || BottomProfile is null) return "请先选择上/下横框型材";
            if (HasMidBeam && MidProfile is null) return "第一页为「有中横结构」，请先选择中横框型材";
            return "";
        }
    }

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        OnPropertyChanged(nameof(InputSummary));
        OnPropertyChanged(nameof(HasMidBeam));
        OnPropertyChanged(nameof(StructureText));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    partial void OnTopProfileChanged(BeamProfile? value)
    {
        OnPropertyChanged(nameof(SectionSummary));
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
    }

    partial void OnBottomProfileChanged(BeamProfile? value)
    {
        OnPropertyChanged(nameof(SectionSummary));
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
    }

    partial void OnMidProfileChanged(BeamProfile? value)
    {
        OnPropertyChanged(nameof(SectionSummary));
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        Invalidate("中横框型材已切换");
    }

    // ---------------- 命令 ----------------

    /// <summary>分隔符：V31 单元式模板前 3 个为 35 星，抗剪段起 36 星（实测 py_u_mid.txt）。</summary>
    private const string Sep35 = "***********************************";

    private const string Sep36 = "************************************";

    private const string Dash = "——————————————————";

    /// <summary>
    /// 「预演」：输出条目与顺序 1:1 对齐 V31 单元式模板
    /// （无中横 17 个数字；有中横再追加中横框 5 个）。
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
        var top = TopProfile!;
        var bottom = BottomProfile!;
        var mid = HasMidBeam ? MidProfile : null;
        var f = MaterialAllowables.Bending(Session.BeamMaterial);
        var fv = MaterialAllowables.Shear(Session.BeamMaterial);
        var g = HorizontalBeamEngine.GravityFromLibrary(Session.DeadLoadGkTransom);

        var r = UnitBeamEngine.Compute(Session.WindStandardKpa, Session.BeamSpanMm,
                                       Session.BeamUpperGridHeightMm, g, Session.AlphaMax,
                                       top, bottom, Session.IgnoreSeismic, mid);

        Results.Clear();
        Add(Sep35, ColorRule);
        Add($"风荷载标准值为:{Session.WindStandardKpa:0.###}KPa", ColorNormal);
        Add(Sep35, ColorRule);

        Add($"横梁单元上横框部分抗弯强度计算值:{r.SigmaTop:0.###}MPa", ColorNormal);
        Add($"选用铝型材的抗弯强度设计值为:{f:0.###}MPa", ColorNormal);
        Judge(r.SigmaTop <= f);
        Add(Dash, ColorRule);
        Add($"横梁单元下横框部分抗弯强度计算值:{r.SigmaBottom:0.###}MPa", ColorNormal);
        Add($"选用铝型材的抗弯强度设计值为:{f:0.###}MPa", ColorNormal);
        Judge(r.SigmaBottom <= f);
        Add(Sep35, ColorRule);

        Add($"组合材料的挠度水平向计算值:{r.DeflectionH:0.###}mm", ColorNormal);
        Add($"按规范的水平向挠度允许值:{r.LimitH:0.###}mm", ColorNormal);
        Judge(r.DeflectionH <= r.LimitH);
        Add(Dash, ColorRule);
        Add($"垂直向挠度计算值:{r.DeflectionV:0.###}mm", ColorNormal);
        Add($"按规范的垂直向挠度允许值:{r.LimitV:0.###}mm", ColorNormal);
        Judge(r.DeflectionV <= r.LimitV);
        Add(Sep36, ColorRule);

        Add($"横梁单元上横框部分抗剪强度计算值tx:{r.TauTopX:0.###}MPa", ColorNormal);
        Add($"选用铝型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
        Judge(r.TauTopX <= fv);
        Add(Dash, ColorRule);
        Add($"横梁单元上横框部分抗剪强度计算值ty:{r.TauTopY:0.###}MPa", ColorNormal);
        Add($"选用铝型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
        Judge(r.TauTopY <= fv);
        Add(Sep36, ColorRule);

        Add($"横梁单元下横框部分抗剪强度计算值tx:{r.TauBottomX:0.###}MPa", ColorNormal);
        Add($"选用铝型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
        Judge(r.TauBottomX <= fv);
        Add(Dash, ColorRule);
        Add($"横梁单元下横框部分抗剪强度计算值ty:{r.TauBottomY:0.###}MPa", ColorNormal);
        Add($"选用铝型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
        Judge(r.TauBottomY <= fv);
        Add(Sep36, ColorRule);

        // ---- 中横框追加段（仅「有中横结构」）：抗弯 → 挠度 → 抗剪 ----
        if (r.MidSigma is double mSigma)
        {
            Add($"横梁中横框部分抗弯强度计算值:{mSigma:0.###}MPa", ColorNormal);
            Add($"选用铝型材的抗弯强度设计值为:{f:0.###}MPa", ColorNormal);
            Judge(mSigma <= f);
            Add(Sep35, ColorRule);

            Add($"横梁中横框挠度水平向计算值:{r.MidDeflectionH!.Value:0.###}mm", ColorNormal);
            Add($"按规范的水平向挠度允许值:{r.LimitH:0.###}mm", ColorNormal);
            Judge(r.MidDeflectionH!.Value <= r.LimitH);
            Add(Dash, ColorRule);
            Add($"横梁中横框挠度垂直向计算值:{r.MidDeflectionV!.Value:0.###}mm", ColorNormal);
            Add($"按规范的垂直向挠度允许值:{r.LimitV:0.###}mm", ColorNormal);
            Judge(r.MidDeflectionV!.Value <= r.LimitV);
            Add(Sep36, ColorRule);

            Add($"横梁中横框部分抗剪强度计算值tx:{r.MidTauX!.Value:0.###}MPa", ColorNormal);
            Add($"选用铝型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
            Judge(r.MidTauX!.Value <= fv);
            Add(Dash, ColorRule);
            Add($"横梁中横框部分抗剪强度计算值ty:{r.MidTauY!.Value:0.###}MPa", ColorNormal);
            Add($"选用铝型材的抗剪强度设计值为:{fv:0.###}MPa", ColorNormal);
            Judge(r.MidTauY!.Value <= fv);
            Add(Sep36, ColorRule);
        }

        Add($"[取证] M_y/2={r.My / 2:0.###}｜M_x={r.Mx:0.###}｜V_h/2={r.Vh2:0.###}｜V_v={r.Vv:0.###}"
            + $"｜u={r.U:0.0000}"
            + (r.MidMx is double mmx ? $"｜中横 M_x={mmx:0.###}" : ""), ColorRule, traceOnly: true);

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = false;
        StatusText = $"预演完成：上框 {top.Code} / 下框 {bottom.Code}"
                   + (HasMidBeam && MidProfile is not null ? $" / 中横 {MidProfile.Code}" : "")
                   + (Session.IgnoreSeismic ? "（不考虑地震）" : "")
                   + $"，{Session.WindStandardSource}";
    }

    [RelayCommand] private void Prev() => _wizard.GoColumnCalc();
    [RelayCommand] private void Next() => _wizard.StepFromBeamNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    /// <summary>切回构件式横梁屏（V31 由第一页「幕墙结构」决定，这里提供界面内回退）。</summary>
    [RelayCommand]
    private void SwitchToFrame()
    {
        Session.WallStructureIsUnit = false;
        _wizard.GoBeamCalc();
    }

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);

    /// <summary>
    /// 是否输出「[取证] …」诊断行。<b>默认关闭</b>：V31 的结果区没有这一行，
    /// 打开它就破坏了「输出逐行一致」，只在排查时临时开。
    /// </summary>
    public bool ShowTrace { get; set; }

    private void Add(string text, IBrush color, bool traceOnly = false)
    {
        if (traceOnly && !ShowTrace) return;
        Results.Add(new PreviewLine(text, color));
    }
}
