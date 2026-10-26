using Microsoft.Win32;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text;
using System.Windows.Controls.Primitives;

namespace NorthIcons;

public partial class MainWindow : Window
{
    const int PageSize = 72;
    readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(180) };
    readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "northIcons", "settings.json");
    Settings settings = new();
    List<IconEntry> library = [];
    List<IconEntry> matches = [];
    List<IconEntry> displayed = [];
    Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);
    IconEntry? selected;
    string section = "all", topic = "All";
    int page, loadVersion;
    string? iconColor;
    DrawingImage? exportPreview;

    public MainWindow()
    {
        InitializeComponent();
        try { if (File.Exists(settingsFile)) settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFile)) ?? new(); } catch { }
        settings.Favorites ??= []; settings.Recent ??= [];
        settings.LibraryFolder = LibraryLocation.Resolve(settings.LibraryFolder);
        debounce.Tick += (_, _) => { debounce.Stop(); Filter(); };
        foreach (var color in new[] { "#172B38", "#087F8C", "#2563EB", "#7C3AED", "#DB2777", "#DC2626", "#EA580C", "#16A34A", "#FFFFFF" })
        {
            var swatch = new Button { Width = 22, Height = 24, Padding = new Thickness(3), Margin = new Thickness(0, 0, 2, 0), ToolTip = color,
                Content = new System.Windows.Shapes.Ellipse { Fill = (Brush)new BrushConverter().ConvertFromString(color)!, Stroke = new SolidColorBrush(Color.FromRgb(213, 222, 226)), StrokeThickness = 1, Width = 15, Height = 15 } };
            System.Windows.Automation.AutomationProperties.SetName(swatch, "Icon color " + color);
            swatch.Click += (_, _) => HexColor.Text = color;
            ColorSwatches.Children.Add(swatch);
        }
        foreach (var name in new[] { "All", "Arrows", "Interface", "Social", "Business", "Music", "Nature" })
        {
            var button = new Button { Content = name, Tag = name, Margin = new Thickness(0, 0, 7, 7), Padding = new Thickness(11, 6, 11, 6), FontSize = 11 };
            button.Click += (_, _) => { topic = name; Filter(); }; Topics.Children.Add(button);
        }
        ProjectLabel.Text = string.IsNullOrEmpty(settings.ProjectFolder) ? "Choose a folder once. Add icons\nwith a single click." : settings.ProjectFolder;
        PreviewKeyDown += Window_KeyDown;
        Loaded += async (_, _) => await LoadLibrary();
        Closing += (_, _) => SaveSettings();
    }

    async Task LoadLibrary()
    {
        int version = ++loadVersion;
        var folder = settings.LibraryFolder;
        Status.Text = Directory.Exists(folder) ? "Reading your library…" : "Unpacking icon library (first launch)…";
        try
        {
            var indexed = await Task.Run(() =>
            {
                LibraryLocation.EnsureAvailable(folder);
                var icons = Directory.EnumerateFiles(folder, "*.svg", SearchOption.TopDirectoryOnly)
                    .Select(path => new IconEntry { FilePath = path }).OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Key).ToList();
                var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var manifest = Path.Combine(folder, "sources.tsv");
                if (File.Exists(manifest)) foreach (var line in File.ReadLines(manifest))
                {
                    var pair = line.Split('\t', 2); if (pair.Length == 2) links[pair[0]] = pair[1];
                }
                var rank = links.Keys.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => x.index, StringComparer.OrdinalIgnoreCase);
                icons = icons.OrderBy(i => rank.GetValueOrDefault(i.FileName, int.MaxValue)).ThenBy(i => i.Title).ToList();
                return (icons, links);
            });
            if (version != loadVersion) return;
            library = indexed.icons; sources = indexed.links;
            LibraryCount.Text = $"{library.Count:N0} icons · on your device";
            Status.Text = "Your library is ready";
            Filter();
        }
        catch (Exception ex) { if (version != loadVersion) return; library = []; matches = []; ShowPage(); Status.Text = "Choose an SVG library folder"; MessageBox.Show(this, ex.Message, "Could not open library"); }
    }
    void Filter()
    {
        if (!IsInitialized) return;
        var words = Search.Text.ToLowerInvariant().Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<IconEntry> query = library;
        if (section == "favorites") query = query.Where(i => settings.Favorites.Contains(i.FileName));
        if (section == "recent") query = query.Where(i => settings.Recent.Contains(i.FileName)).OrderBy(i => settings.Recent.IndexOf(i.FileName));
        var topicTerms = topic switch
        {
            "Arrows" => new[] { "arrow", "angle", "caret", "chevron" },
            "Interface" => ["home", "menu", "search", "settings", "user", "check", "cross"],
            "Social" => ["instagram", "facebook", "twitter", "linkedin", "youtube", "tiktok", "social"],
            "Business" => ["business", "chart", "money", "bank", "briefcase", "office"],
            "Music" => ["music", "audio", "headphone", "guitar", "microphone", "piano"],
            "Nature" => ["tree", "flower", "leaf", "sun", "cloud", "mountain"], _ => []
        };
        matches = query.Where(i => words.All(w => i.SearchText.Contains(w)) && (topicTerms.Length == 0 || topicTerms.Any(t => i.SearchText.Contains(t)))).ToList();
        Heading.Text = section switch { "favorites" => "Your favorites." , "recent" => "Back to the good ones.", _ => "Find your next icon." };
        foreach (var button in new[] { AllNav, FavoritesNav, RecentNav }) button.Background = Brushes.Transparent;
        (section == "favorites" ? FavoritesNav : section == "recent" ? RecentNav : AllNav).Background = new SolidColorBrush(Color.FromRgb(231, 244, 243));
        foreach (Button button in Topics.Children) { bool active = (string)button.Tag == topic; button.Background = active ? new SolidColorBrush(Color.FromRgb(8, 127, 140)) : Brushes.White; button.Foreground = active ? Brushes.White : (Brush)FindResource("Muted"); }
        page = 0; ShowPage();
    }
    void ShowPage()
    {
        foreach (var icon in displayed) if (icon != selected) icon.ReleasePreview();
        displayed = matches.Skip(page * PageSize).Take(PageSize).ToList();
        IconGrid.ItemsSource = displayed;
        GridScroll.ScrollToTop();
        int pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);
        ResultCount.Text = $"{matches.Count:N0} icons" + (Search.Text.Length > 0 ? $" matching “{Search.Text}”" : " to make it yours");
        PageLabel.Text = $"{page + 1} / {pages:N0}";
        Previous.IsEnabled = page > 0; Next.IsEnabled = page + 1 < pages;
        EmptyState.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    void Select(IconEntry icon)
    {
        if (selected != null) selected.IsSelected = false;
        if (selected != null && !displayed.Contains(selected)) selected.ReleasePreview();
        selected = icon;
        icon.IsSelected = true;
        IconTitle.Text = icon.Title;
        IconMeta.Text = $"SVG vector · {new FileInfo(icon.FilePath).Length / 1024d:0.#} KB\n{icon.FileName}";
        IconActions.IsEnabled = AddProjectButton.IsEnabled = SourceButton.IsEnabled = true;
        FavoriteLabel.Text = settings.Favorites.Contains(icon.FileName) ? "Remove favorite" : "Add to favorites";
        UpdateColorPreview();
    }
    string CurrentSvg()
    {
        if (selected == null) throw new InvalidOperationException("Select an icon first.");
        var svg = File.ReadAllText(selected.FilePath);
        return iconColor == null ? svg : SvgColor.Apply(svg, iconColor);
    }
    void UpdateColorPreview()
    {
        if (selected == null) return;
        exportPreview = null;
        try
        {
            if (iconColor == null) exportPreview = selected.Preview;
            else { using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CurrentSvg())); exportPreview = SvgPreview.Load(stream); }
            PreviewWarning.Text = exportPreview == null ? selected.PreviewError ?? "Preview unavailable." : "";
        }
        catch { PreviewWarning.Text = "Preview unavailable for this SVG. You can still export the SVG."; }
        LargePreview.Source = exportPreview;
        PngButton.IsEnabled = exportPreview != null;
        var color = iconColor == null ? Colors.Black : (Color)ColorConverter.ConvertFromString(iconColor);
        PreviewSurface.Background = new SolidColorBrush(color.R * .299 + color.G * .587 + color.B * .114 > 210 ? Color.FromRgb(74, 88, 99) : Color.FromRgb(244, 248, 248));
        ColorNote.Text = iconColor == null ? "Original colors · copies and exports" : $"{iconColor} · copies and exports";
    }
    void Color_Changed(object sender, TextChangedEventArgs e)
    {
        if (ColorNote == null) return;
        if (SvgColor.TryNormalize(HexColor.Text, out var color))
        {
            iconColor = color; HexColor.Foreground = (Brush)FindResource("Ink");
            HexColor.ToolTip = "Custom icon color (#RGB or #RRGGBB)";
            UpdateColorPreview();
        }
        else if (!string.IsNullOrWhiteSpace(HexColor.Text))
        {
            HexColor.Foreground = Brushes.Firebrick;
            HexColor.ToolTip = "Enter a valid hex color, e.g. #087F8C. The last valid color remains active.";
        }
    }
    void ResetColor_Click(object sender, RoutedEventArgs e)
    {
        iconColor = null; HexColor.Clear(); HexColor.Foreground = (Brush)FindResource("Ink"); UpdateColorPreview();
    }
    void Remember()
    {
        if (selected == null) return;
        settings.Recent.Remove(selected.FileName); settings.Recent.Insert(0, selected.FileName);
        if (settings.Recent.Count > 150) settings.Recent.RemoveRange(150, settings.Recent.Count - 150);
        SaveSettings();
    }
    void SaveSettings()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!); File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings)); }
        catch { Status.Text = "Could not save preferences"; }
    }
    void Perform(Action action)
    {
        try { action(); } catch (Exception ex) { Status.Text = "Action could not complete"; MessageBox.Show(this, ex.Message, "northIcons"); }
    }
    void CopySvg_Click(object sender, RoutedEventArgs e) => Perform(() => { if (selected == null) return; Clipboard.SetText(CurrentSvg()); Remember(); Status.Text = "SVG copied — paste into your project"; });
    void CopyFile_Click(object sender, RoutedEventArgs e) => Perform(() =>
    {
        if (selected == null) return;
        var file = selected.FilePath;
        if (iconColor != null)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "northIcons", "Clipboard", iconColor.TrimStart('#'));
            Directory.CreateDirectory(folder); file = Path.Combine(folder, selected.FileName); File.WriteAllText(file, CurrentSvg());
        }
        Clipboard.SetFileDropList(new StringCollection { file }); Remember(); Status.Text = "File copied — paste into a folder";
    });
    void Export_Click(object sender, RoutedEventArgs e) => Perform(() =>
    {
        if (selected == null) return;
        var dialog = new SaveFileDialog { Filter = "SVG icon (*.svg)|*.svg", FileName = selected.FileName };
        if (dialog.ShowDialog(this) == true) { CopyTo(dialog.FileName); Remember(); Status.Text = "SVG exported"; }
    });
    void CopyTo(string target)
    {
        if (selected == null) return;
        if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(selected.FilePath), StringComparison.OrdinalIgnoreCase)) return;
        if (iconColor == null) File.Copy(selected.FilePath, target, true);
        else File.WriteAllText(target, CurrentSvg());
    }
    void ExportPng_Click(object sender, RoutedEventArgs e) => Perform(() =>
    {
        if (selected == null || exportPreview is not { } image) return;
        int size = int.Parse((string)((ComboBoxItem)PngSize.SelectedItem).Tag);
        var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = selected.Key + ".png" };
        if (dialog.ShowDialog(this) == true)
        {
            PngExport.Save(image, dialog.FileName, size);
            Remember(); Status.Text = $"Transparent PNG saved ({size} × {size})";
        }
    });
    void Project_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose your project's icon folder" };
        if (dialog.ShowDialog(this) == true) { settings.ProjectFolder = dialog.FolderName; ProjectLabel.Text = dialog.FolderName; SaveSettings(); }
    }
    void AddProject_Click(object sender, RoutedEventArgs e) => Perform(() =>
    {
        if (selected == null) return;
        if (string.IsNullOrEmpty(settings.ProjectFolder)) Project_Click(sender, e);
        if (string.IsNullOrEmpty(settings.ProjectFolder)) return;
        if (!Directory.Exists(settings.ProjectFolder)) throw new DirectoryNotFoundException("The project folder is unavailable. Choose it again.");
        var target = Path.Combine(settings.ProjectFolder, selected.FileName);
        if (File.Exists(target) && MessageBox.Show(this, "This file already exists. Replace it?", "northIcons", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        CopyTo(target); Remember(); Status.Text = "Added to your project";
    });
    void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (selected == null) return;
        if (!settings.Favorites.Remove(selected.FileName)) settings.Favorites.Add(selected.FileName);
        SaveSettings(); Select(selected); if (section == "favorites") Filter();
    }
    void Source_Click(object sender, RoutedEventArgs e) => Perform(() =>
    {
        if (selected == null) return;
        var url = sources.GetValueOrDefault(selected.FileName) ?? $"https://www.flaticon.com/free-icon-font/{selected.Key}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "www.flaticon.com") throw new InvalidDataException("Invalid source URL");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
    async void Library_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder containing SVG icons" };
        if (dialog.ShowDialog(this) == true) { settings.LibraryFolder = dialog.FolderName; SaveSettings(); await LoadLibrary(); }
    }
    async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadLibrary();
    void Icon_Click(object sender, RoutedEventArgs e) => Perform(() => Select((IconEntry)((Button)sender).Tag));
    void All_Click(object sender, RoutedEventArgs e) { section = "all"; Filter(); }
    void Favorites_Click(object sender, RoutedEventArgs e) { section = "favorites"; Filter(); }
    void Recent_Click(object sender, RoutedEventArgs e) { section = "recent"; Filter(); }
    void Search_Changed(object sender, TextChangedEventArgs e) { if (SearchHint == null) return; SearchHint.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; debounce.Stop(); debounce.Start(); }
    void Previous_Click(object sender, RoutedEventArgs e) { if (page > 0) { page--; ShowPage(); } }
    void Next_Click(object sender, RoutedEventArgs e) { if ((page + 1) * PageSize < matches.Count) { page++; ShowPage(); } }
    void Size_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (LargePreview != null) LargePreview.Width = LargePreview.Height = e.NewValue; }
    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Search.Focus(); Search.SelectAll(); e.Handled = true; }
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBoxBase) { CopySvg_Click(this, new RoutedEventArgs()); e.Handled = true; }
        if (e.Key == Key.Escape && Search.IsKeyboardFocusWithin) Search.Clear();
    }
    public sealed class Settings
    {
        public string LibraryFolder { get; set; } = LibraryLocation.Bundled;
        public string ProjectFolder { get; set; } = "";
        public HashSet<string> Favorites { get; set; } = [];
        public List<string> Recent { get; set; } = [];
    }
}
