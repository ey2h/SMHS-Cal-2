using Avalonia.Controls;
using Avalonia.Interactivity;
using GlassManagement.Desktop.ViewModels;
using GlassManagement.Desktop.Views.Calc;

namespace GlassManagement.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel VM => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>打开 V35 结构计算界面（1:1 复刻浏览）。</summary>
    private void OnOpenCalc(object? sender, RoutedEventArgs e)
    {
        var win = new CalcBrowserWindow();
        win.Show(this);
    }

    // Navigation
    private void OnPrevStep(object? sender, RoutedEventArgs e) => VM.PrevStep();
    private void OnNextStep(object? sender, RoutedEventArgs e) => VM.NextStep();
    private void OnGenerateReport(object? sender, RoutedEventArgs e) => VM.StatusMessage = "请在计算书页面选择类型生成";

    // Region
    private bool _regionsLoaded;
    private void OnRegionFilter(object? sender, TextChangedEventArgs e)
    {
        if (_regionsLoaded) VM.FilterRegionList();
    }
    private void EnsureRegionsLoaded()
    {
        if (!_regionsLoaded) { _ = VM.LoadAllRegions().ContinueWith(_ => _regionsLoaded = true); }
    }
    private void OnRegionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedIndex >= 0) VM.SelectRegion(lb.SelectedIndex);
    }

    // Step1 new handlers
    private void OnSearchRegion(object? sender, RoutedEventArgs e) { /* TODO: filter region table */ }
    private void OnOpenGravityDialog(object? sender, RoutedEventArgs e) { VM.AutoGravity = !VM.AutoGravity; }

    // Calculations
    private async void OnCalcProjectWindLoad(object? sender, RoutedEventArgs e) => await VM.CalculateProjectWindLoad();
    private async void OnTestConnection(object? sender, RoutedEventArgs e) { await VM.TestConnection(); EnsureRegionsLoaded(); }
    private async void OnCalcGlass(object? sender, RoutedEventArgs e) => await VM.CalculateGlass();
    private async void OnCalcAluminumRafter(object? sender, RoutedEventArgs e) => await VM.CalculateAluminumRafter();
    private async void OnCalcStonePanel(object? sender, RoutedEventArgs e) => await VM.CalculateStonePanel();
    private async void OnCalcConnection(object? sender, RoutedEventArgs e) => await VM.CalculateConnection();
    private async void OnCalcWeld(object? sender, RoutedEventArgs e) => await VM.CalculateWeld();

    // Glass config dialogs
    private async void OnOpenGlassConfig(object? sender, RoutedEventArgs e)
    {
        var win = new GlassConfigWindow();
        var result = await win.ShowDialog<bool?>(this);
        if (result == true && win.DataContext is GlassConfigViewModel configVm)
        {
            VM.PanelCategoryIndex = 0;
            if (configVm.IsSingleMode)
            {
                VM.OuterIsLaminated = false;
                VM.OuterThicknessIndex = configVm.SingleThicknessIndex;
                VM.OuterTemperedIndex = configVm.SingleTemperedIndex;
            }
            else
            {
                VM.OuterIsLaminated = true;
                VM.OuterLamSheetCount = configVm.LamSheetCountIndex;
                for (int i = 0; i < System.Math.Min(configVm.LamSheets.Count, VM.OuterLamSheets.Count); i++)
                {
                    VM.OuterLamSheets[i].ThicknessIndex = configVm.LamSheets[i].ThicknessIndex;
                    VM.OuterLamSheets[i].TemperedIndex = configVm.LamSheets[i].TemperedIndex;
                }
                for (int i = 0; i < System.Math.Min(configVm.LamLayers.Count, VM.OuterLamLayers.Count); i++)
                {
                    VM.OuterLamLayers[i].FilmTypeIndex = configVm.LamLayers[i].FilmTypeIndex;
                    VM.OuterLamLayers[i].FilmThicknessIndex = configVm.LamLayers[i].FilmThicknessIndex;
                }
            }
            VM.StatusMessage = $"已应用玻璃配置: {configVm.ConfigResult}";
        }
    }

    private async void OnOpenIGUConfig(object? sender, RoutedEventArgs e)
    {
        var win = new IGUConfigWindow();
        var result = await win.ShowDialog<bool?>(this);
        if (result == true && win.DataContext is IGUConfigViewModel iguVm)
        {
            int sheetCount = iguVm.SheetCount;
            if (sheetCount == 2) VM.PanelCategoryIndex = 1;
            else if (sheetCount == 3) VM.PanelCategoryIndex = 2;
            VM.Gap1GasIndex = iguVm.GasTypeIndex;
            if (sheetCount == 3) VM.Gap2GasIndex = iguVm.GasTypeIndex;
            if (iguVm.Sheets.Count > 0)
            {
                var outer = iguVm.Sheets[0];
                VM.OuterIsLaminated = !string.IsNullOrWhiteSpace(outer.GlassConfigResult) && outer.GlassConfigResult.Contains("+");
                if (!VM.OuterIsLaminated) VM.OuterThicknessIndex = outer.ThicknessIndex;
            }
            if (iguVm.Gaps.Count > 0) VM.Gap1ThicknessIndex = iguVm.Gaps[0].ThicknessIndex;
            int innerIdx = sheetCount == 2 ? 1 : 2;
            if (iguVm.Sheets.Count > innerIdx)
            {
                var inner = iguVm.Sheets[innerIdx];
                VM.InnerIsLaminated = !string.IsNullOrWhiteSpace(inner.GlassConfigResult) && inner.GlassConfigResult.Contains("+");
                if (!VM.InnerIsLaminated) VM.InnerThicknessIndex = inner.ThicknessIndex;
            }
            if (sheetCount == 3 && iguVm.Sheets.Count > 1)
            {
                var mid = iguVm.Sheets[1];
                VM.MiddleIsLaminated = !string.IsNullOrWhiteSpace(mid.GlassConfigResult) && mid.GlassConfigResult.Contains("+");
                if (!VM.MiddleIsLaminated) VM.MiddleThicknessIndex = mid.ThicknessIndex;
            }
            if (sheetCount == 3 && iguVm.Gaps.Count > 1) VM.Gap2ThicknessIndex = iguVm.Gaps[1].ThicknessIndex;
            VM.StatusMessage = $"已应用中空配置: {iguVm.ConfigResult}";
        }
    }

    // Diagrams
    private async void OnLoadGlassDiagram(object? sender, RoutedEventArgs e) => await VM.LoadGlassDiagram();
    private async void OnLoadAluminumDiagram(object? sender, RoutedEventArgs e) => await VM.LoadAluminumDiagram();
    private async void OnLoadContourStress(object? sender, RoutedEventArgs e) => await VM.LoadContourStress();
    private async void OnLoadContourDeflection(object? sender, RoutedEventArgs e) => await VM.LoadContourDeflection();

    // Reports
    private async void OnReport_Glass(object? sender, RoutedEventArgs e) => await VM.GenerateReport("glass");
    private async void OnReport_InsulatingGlass(object? sender, RoutedEventArgs e) => await VM.GenerateReport("insulating-glass");
    private async void OnReport_AluRafter(object? sender, RoutedEventArgs e) => await VM.GenerateReport("aluminum-rafter");
    private async void OnReport_StoneBracket(object? sender, RoutedEventArgs e) => await VM.GenerateReport("stone-bracket");
    private async void OnReport_EmbedPart(object? sender, RoutedEventArgs e) => await VM.GenerateReport("embed-part");
    private async void OnReport_AluPlate(object? sender, RoutedEventArgs e) => await VM.GenerateReport("aluminum-plate");
    private async void OnReport_StonePanel(object? sender, RoutedEventArgs e) => await VM.GenerateReport("stone-panel");
    private async void OnReport_Silicone(object? sender, RoutedEventArgs e) => await VM.GenerateReport("silicone");
}
