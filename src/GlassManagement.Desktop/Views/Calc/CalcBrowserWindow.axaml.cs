using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>Browse the 34 reconstructed Calc V35 forms.</summary>
public partial class CalcBrowserWindow : Window
{
    private readonly ContentControl _host;
    private readonly TextBlock _info;

    public CalcBrowserWindow()
    {
        InitializeComponent();

        var list = this.FindControl<ListBox>("FormList")!;
        _host = this.FindControl<ContentControl>("Host")!;
        _info = this.FindControl<TextBlock>("InfoText")!;

        list.ItemsSource = CalcFormCatalog.All
            .Select((e, i) => $"{i + 1:00}  {e.Title}")
            .ToList();

        list.SelectionChanged += (_, _) =>
        {
            var idx = list.SelectedIndex;
            if (idx < 0 || idx >= CalcFormCatalog.All.Count) return;
            var e = CalcFormCatalog.All[idx];
            try
            {
                _host.Content = e.Create();
                _info.Text = $"{e.Width}×{e.Height}px · 源控件 {e.SourceControls}";
            }
            catch (System.Exception ex)
            {
                _info.Text = "加载失败: " + ex.Message;
            }
        };

        list.SelectedIndex = CalcFormCatalog.All
            .Select((e, i) => (e.Title, i))
            .Where(t => t.Title.Contains("玻璃计算"))
            .Select(t => t.i)
            .DefaultIfEmpty(0)
            .First();
    }

    /// <summary>
    /// 屏内「下一步」跳转：按标题关键字在目录里找条目并装载到 Host。
    /// 与 CalcMainWindow.ShowByTitle 行为一致（同一份 CalcFormCatalog）。
    /// </summary>
    public bool ShowByTitle(string titleKeyword)
    {
        var e = CalcFormCatalog.All.FirstOrDefault(
            x => x.Title.Contains(titleKeyword, System.StringComparison.Ordinal));
        if (e is null) { _info.Text = $"目录里没有「{titleKeyword}」"; return false; }
        try
        {
            _host.Content = e.Create();
            _info.Text = $"下一步 → {e.Title} · {e.Width}×{e.Height}px";
            return true;
        }
        catch (System.Exception ex)
        {
            _info.Text = "加载失败: " + ex.Message;
            return false;
        }
    }
}
