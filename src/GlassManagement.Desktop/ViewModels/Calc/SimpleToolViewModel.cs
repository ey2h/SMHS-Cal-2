using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「独立工具」屏的通用基类：一行字段列表 + 一个计算按钮 + 一个输出框。
///
/// 为什么这样抽象：<c>其它计算</c> 菜单下 15 个工具（焊缝/螺栓群/锚板/植筋/钢销/螺纹/自攻/
/// 排烟/开启扇/百叶/米重/热应力/热伸长/多点锁/转角立柱）的引擎早已完成并有单元测试守护，
/// 缺的只是界面。各工具的差别只有「字段清单」和「调哪个引擎」，
/// 因此统一用本基类 + <see cref="Views.Calc.SimpleToolView"/> 承载，避免 15 份重复布局。
///
/// 约束：子类只允许在构造函数里 <see cref="F"/> 追加字段，并实现 <see cref="DoCalc"/>。
/// </summary>
public abstract partial class SimpleToolViewModel : ObservableObject
{
    /// <summary>窗体标题（与 V31 菜单项文案一致）。</summary>
    public abstract string Title { get; }

    /// <summary>底部说明区文字（公式出处 / 取值约定）。</summary>
    public abstract string Hint { get; }

    /// <summary>输入字段，按界面从上到下顺序。</summary>
    public ObservableCollection<ToolField> Fields { get; } = new();

    [ObservableProperty] private string _output = "";

    /// <summary>追加一个输入字段并返回句柄（子类用它取值）。</summary>
    protected ToolField F(string label, string text, string unit = "")
    {
        var f = new ToolField(label, text, unit);
        Fields.Add(f);
        return f;
    }

    /// <summary>追加一个下拉字段并返回句柄（selected 指定默认选中项，缺省取第一项）。</summary>
    protected ToolField FD(string label, string[] options, string unit = "", string? selected = null)
    {
        var pick = selected is not null && Array.IndexOf(options, selected) >= 0
            ? selected
            : (options.Length > 0 ? options[0] : "");
        var f = new ToolField(label, pick, unit, options);
        Fields.Add(f);
        return f;
    }

    /// <summary>执行计算并返回要显示在输出框里的文本。</summary>
    protected abstract string DoCalc();

    [RelayCommand]
    private void Calc()
    {
        try
        {
            Output = DoCalc();
        }
        catch (Exception ex)
        {
            Output = "计算失败：" + ex.Message;
        }
    }

    /// <summary>输出框分隔线，与 V31 输出格式一致。</summary>
    protected const string Sep = "****************************************";

    /// <summary>统一的数值显示：去掉多余尾零但保留有效位。</summary>
    protected static string N(double v, int digits = 3)
        => Math.Round(v, digits).ToString("0." + new string('#', digits));
}
