using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace GlassManagement.SectionTool;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SectionCalculationService _calcService = new();
    private readonly DatabaseService _dbService = new();

    public MainViewModel()
    {
        OuterPoints = new ObservableCollection<PointRow>();
        Holes = new ObservableCollection<HoleGroup>();
        DatabaseFiles = new ObservableCollection<string>();
        BrowseProfiles = new ObservableCollection<ProfileRecord>();
        Categories = new ObservableCollection<string>(ProfileCategories.All);

        CalculateCommand = new RelayCommand(Calculate);
        ImportCommand = new RelayCommand(ImportToDatabase);
        AddOuterPointCommand = new RelayCommand(() => OuterPoints.Add(new PointRow()));
        RemoveOuterPointCommand = new RelayCommand(RemoveLastOuterPoint);
        AddHoleCommand = new RelayCommand(() =>
        {
            var h = new HoleGroup { Name = $"内孔 {Holes.Count + 1}" };
            h.Points.Add(new PointRow());
            Holes.Add(h);
        });
        RemoveHoleCommand = new RelayCommand(RemoveLastHole);
        AddHolePointCommand = new RelayCommand<HoleGroup>(h => h?.Points.Add(new PointRow()));
        RemoveHolePointCommand = new RelayCommand<HoleGroup>(h =>
        {
            if (h?.Points.Count > 0) h.Points.RemoveAt(h.Points.Count - 1);
        });
        RefreshProfilesCommand = new RelayCommand(RefreshBrowseProfiles);
        DeleteSelectedProfileCommand = new RelayCommand(DeleteSelectedProfile);

        ScanDatabaseFiles();
    }

    // ===== Section type =====
    private string _sectionType = "Normal";
    public string SectionType
    {
        get => _sectionType;
        set { _sectionType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBreakHeat)); }
    }
    public bool IsBreakHeat => SectionType != "Normal";

    // ===== Points =====
    public ObservableCollection<PointRow> OuterPoints { get; }
    public ObservableCollection<HoleGroup> Holes { get; }

    // ===== Break heat params =====
    private double _compositeElasticity = 80;
    public double CompositeElasticity
    {
        get => _compositeElasticity;
        set { _compositeElasticity = value; OnPropertyChanged(); }
    }

    private double _spanLength;
    public double SpanLength
    {
        get => _spanLength;
        set { _spanLength = value; OnPropertyChanged(); }
    }

    private string _side1Text = "";
    public string Side1Text
    {
        get => _side1Text;
        set { _side1Text = value; OnPropertyChanged(); }
    }

    private string _side2Text = "";
    public string Side2Text
    {
        get => _side2Text;
        set { _side2Text = value; OnPropertyChanged(); }
    }

    // ===== Database =====
    public ObservableCollection<string> DatabaseFiles { get; }

    private string _selectedDatabase = "";
    public string SelectedDatabase
    {
        get => _selectedDatabase;
        set { _selectedDatabase = value; OnPropertyChanged(); RefreshBrowseProfiles(); }
    }

    private string _profileName = "";
    public string ProfileName
    {
        get => _profileName;
        set { _profileName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Categories { get; }

    private string _selectedCategory = "立柱";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; OnPropertyChanged(); RefreshBrowseProfiles(); }
    }

    private double _wallThickness;
    public double WallThickness
    {
        get => _wallThickness;
        set { _wallThickness = value; OnPropertyChanged(); }
    }

    private double _shearWebWidth;
    public double ShearWebWidth
    {
        get => _shearWebWidth;
        set { _shearWebWidth = value; OnPropertyChanged(); }
    }

    // ===== Results =====
    private SectionResult? _result;
    public SectionResult? Result
    {
        get => _result;
        set { _result = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultText)); }
    }

    private string _unitMode = "mm";
    public string UnitMode
    {
        get => _unitMode;
        set { _unitMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultText)); }
    }

    public string ResultText
    {
        get
        {
            if (Result == null) return "尚未计算";
            var r = Result;
            bool cm = UnitMode == "cm";
            double aDiv = cm ? 100 : 1;
            double iDiv = cm ? 10000 : 1;
            double wDiv = cm ? 1000 : 1;
            double sDiv = cm ? 1000 : 1;
            string aUnit = cm ? "cm²" : "mm²";
            string iUnit = cm ? "cm⁴" : "mm⁴";
            string wUnit = cm ? "cm³" : "mm³";
            string lUnit = "mm";

            return $"面积 A = {r.Area / aDiv:F4} {aUnit}\n" +
                   $"惯性矩 Ix = {r.Ix / iDiv:F4} {iUnit}\n" +
                   $"惯性矩 Iy = {r.Iy / iDiv:F4} {iUnit}\n" +
                   $"抗弯矩 Wx1 = {r.Wx1 / wDiv:F4} {wUnit}\n" +
                   $"抗弯矩 Wx2 = {r.Wx2 / wDiv:F4} {wUnit}\n" +
                   $"抗弯矩 Wy1 = {r.Wy1 / wDiv:F4} {wUnit}\n" +
                   $"抗弯矩 Wy2 = {r.Wy2 / wDiv:F4} {wUnit}\n" +
                   $"面积矩 Sx = {r.Sx / sDiv:F4} {wUnit}\n" +
                   $"面积矩 Sy = {r.Sy / sDiv:F4} {wUnit}\n" +
                   $"质心 Cx = {r.Cx:F4} {lUnit}\n" +
                   $"质心 Cy = {r.Cy:F4} {lUnit}\n" +
                   $"Xmin = {r.Xmin:F4} {lUnit}\n" +
                   $"Xmax = {r.Xmax:F4} {lUnit}\n" +
                   $"Ymin = {r.Ymin:F4} {lUnit}\n" +
                   $"Ymax = {r.Ymax:F4} {lUnit}";
        }
    }

    // ===== Preview geometry (for binding) =====
    private List<PointD>? _previewOuter;
    public List<PointD>? PreviewOuter
    {
        get => _previewOuter;
        set { _previewOuter = value; OnPropertyChanged(); }
    }

    private List<List<PointD>>? _previewHoles;
    public List<List<PointD>>? PreviewHoles
    {
        get => _previewHoles;
        set { _previewHoles = value; OnPropertyChanged(); }
    }

    // ===== Browse profiles =====
    public ObservableCollection<ProfileRecord> BrowseProfiles { get; }

    private ProfileRecord? _selectedBrowseProfile;
    public ProfileRecord? SelectedBrowseProfile
    {
        get => _selectedBrowseProfile;
        set { _selectedBrowseProfile = value; OnPropertyChanged(); }
    }

    private string _browseCategory = "立柱";
    public string BrowseCategory
    {
        get => _browseCategory;
        set { _browseCategory = value; OnPropertyChanged(); RefreshBrowseProfiles(); }
    }

    private bool _isBrowseExpanded;
    public bool IsBrowseExpanded
    {
        get => _isBrowseExpanded;
        set { _isBrowseExpanded = value; OnPropertyChanged(); if (value) RefreshBrowseProfiles(); }
    }

    // ===== Status =====
    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    // ===== Commands =====
    public ICommand CalculateCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand AddOuterPointCommand { get; }
    public ICommand RemoveOuterPointCommand { get; }
    public ICommand AddHoleCommand { get; }
    public ICommand RemoveHoleCommand { get; }
    public ICommand AddHolePointCommand { get; }
    public ICommand RemoveHolePointCommand { get; }
    public ICommand RefreshProfilesCommand { get; }
    public ICommand DeleteSelectedProfileCommand { get; }

    // ===== Methods =====

    public void ScanDatabaseFiles()
    {
        DatabaseFiles.Clear();
        var paths = new List<string>
        {
            Environment.CurrentDirectory,
            Path.Combine(Environment.CurrentDirectory, "Data"),
            Path.Combine(Environment.CurrentDirectory, "..", "Data"),
        };
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrEmpty(desktop)) paths.Add(desktop);

        var found = new HashSet<string>();
        foreach (var dir in paths)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.GetFiles(dir, "*.db"))
            {
                var full = Path.GetFullPath(f);
                if (found.Add(full))
                    DatabaseFiles.Add(full);
            }
        }

        if (DatabaseFiles.Count > 0 && string.IsNullOrEmpty(SelectedDatabase))
            SelectedDatabase = DatabaseFiles[0];
    }

    public async void BrowseDatabase()
    {
        var window = GetMainWindow();
        if (window == null) return;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择数据库文件",
            FileTypeFilter = new[] { new FilePickerFileType("SQLite数据库") { Patterns = new[] { "*.db" } } },
            AllowMultiple = false,
        });
        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            if (!DatabaseFiles.Contains(path))
                DatabaseFiles.Add(path);
            SelectedDatabase = path;
        }
    }

    public async void CreateNewDatabase()
    {
        var window = GetMainWindow();
        if (window == null) return;
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "新建数据库",
            DefaultExtension = "db",
            SuggestedFileName = "型材参数库.db",
            FileTypeChoices = new[] { new FilePickerFileType("SQLite数据库") { Patterns = new[] { "*.db" } } },
        });
        if (file != null)
        {
            var path = file.Path.LocalPath;
            _dbService.CreateDatabase(path);
            if (!DatabaseFiles.Contains(path))
                DatabaseFiles.Add(path);
            SelectedDatabase = path;
            StatusText = $"✅ 已创建数据库: {Path.GetFileName(path)}";
        }
    }

    public void PasteFromClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        OuterPoints.Clear();
        var lines = text.Split('\n', '\r');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var parts = trimmed.Split(new[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && double.TryParse(parts[0], out var x) && double.TryParse(parts[1], out var y))
                OuterPoints.Add(new PointRow { X = x, Y = y });
        }
    }

    private void Calculate()
    {
        try
        {
            var outer = OuterPoints.Select(p => new PointD { X = p.X, Y = p.Y }).ToList();
            if (outer.Count < 3) { StatusText = "❌ 外轮廓至少需要3个点"; return; }

            var holes = Holes.Select(h => h.Points.Select(p => new PointD { X = p.X, Y = p.Y }).ToList()).Where(h => h.Count >= 3).ToList();

            if (IsBreakHeat)
            {
                var side1 = ParsePoints(Side1Text);
                var side2 = ParsePoints(Side2Text);
                if (side1.Count < 3 || side2.Count < 3) { StatusText = "❌ 断热侧面至少需要3个点"; return; }
                Result = _calcService.CalculateBreakHeatSection(side1, side2, outer, CompositeElasticity, SpanLength);
            }
            else
            {
                Result = _calcService.CalculateSection(outer, holes.Count > 0 ? holes : null);
            }

            PreviewOuter = outer;
            PreviewHoles = holes;
            StatusText = "✅ 计算完成";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ {ex.Message}";
        }
    }

    private void ImportToDatabase()
    {
        try
        {
            if (Result == null) { StatusText = "❌ 请先计算截面特性"; return; }
            if (string.IsNullOrWhiteSpace(SelectedDatabase)) { StatusText = "❌ 请选择数据库文件"; return; }
            if (string.IsNullOrWhiteSpace(ProfileName)) { StatusText = "❌ 请输入型材名称"; return; }
            if (string.IsNullOrWhiteSpace(SelectedCategory)) { StatusText = "❌ 请选择型材分类"; return; }

            var r = Result;
            // mm → cm conversion for database storage
            var record = new ProfileRecord
            {
                名称 = ProfileName,
                Ix = r.Ix / 10000.0,   // mm⁴ → cm⁴
                Iy = r.Iy / 10000.0,
                Wx1 = r.Wx1 / 1000.0,  // mm³ → cm³
                Wx2 = r.Wx2 / 1000.0,
                Wy1 = r.Wy1 / 1000.0,
                Wy2 = r.Wy2 / 1000.0,
                Sx = r.Sx / 1000.0,
                Sy = r.Sy / 1000.0,
                A = r.Area / 100.0,     // mm² → cm²
                壁厚 = WallThickness,
                腹板宽度 = ShearWebWidth,
            };

            _dbService.ImportProfile(SelectedDatabase, SelectedCategory, record);
            StatusText = $"✅ 已导入 [{ProfileName}] 到 [{SelectedCategory}]";
            RefreshBrowseProfiles();
        }
        catch (Exception ex)
        {
            StatusText = $"❌ 导入失败: {ex.Message}";
        }
    }

    private void RefreshBrowseProfiles()
    {
        BrowseProfiles.Clear();
        if (string.IsNullOrWhiteSpace(SelectedDatabase) || !File.Exists(SelectedDatabase)) return;
        var cat = string.IsNullOrWhiteSpace(BrowseCategory) ? SelectedCategory : BrowseCategory;
        if (string.IsNullOrWhiteSpace(cat)) return;
        try
        {
            foreach (var p in _dbService.GetProfiles(SelectedDatabase, cat))
                BrowseProfiles.Add(p);
        }
        catch { /* ignore */ }
    }

    private void DeleteSelectedProfile()
    {
        if (SelectedBrowseProfile == null) { StatusText = "❌ 请选择要删除的型材"; return; }
        if (string.IsNullOrWhiteSpace(SelectedDatabase)) return;
        var cat = string.IsNullOrWhiteSpace(BrowseCategory) ? SelectedCategory : BrowseCategory;
        try
        {
            _dbService.DeleteProfile(SelectedDatabase, cat, SelectedBrowseProfile.Id);
            StatusText = $"✅ 已删除 [{SelectedBrowseProfile.名称}]";
            RefreshBrowseProfiles();
        }
        catch (Exception ex) { StatusText = $"❌ 删除失败: {ex.Message}"; }
    }

    private void RemoveLastOuterPoint()
    {
        if (OuterPoints.Count > 0) OuterPoints.RemoveAt(OuterPoints.Count - 1);
    }

    private void RemoveLastHole()
    {
        if (Holes.Count > 0) Holes.RemoveAt(Holes.Count - 1);
    }

    private static List<PointD> ParsePoints(string text)
    {
        var list = new List<PointD>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var line in text.Split('\n', '\r'))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            var parts = t.Split(new[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && double.TryParse(parts[0], out var x) && double.TryParse(parts[1], out var y))
                list.Add(new PointD { X = x, Y = y });
        }
        return list;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    public RelayCommand(Action<T?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute((T?)parameter);
}
