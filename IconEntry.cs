using System.IO;
using System.Windows.Media;
using System.ComponentModel;

namespace NorthIcons;

public sealed class IconEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    bool isSelected;
    public bool IsSelected { get => isSelected; set { isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public string Key => Path.GetFileNameWithoutExtension(FilePath);
    public string Slug => Key.LastIndexOf('_') is var index && index > 0 ? Key[..index] : Key;
    public string Title => Slug.Replace('-', ' ');
    public string SearchText => Title.ToLowerInvariant();
    DrawingImage? preview;
    bool attempted;
    public string? PreviewError { get; private set; }
    public DrawingImage? Preview
    {
        get
        {
            if (!attempted)
            {
                attempted = true;
                try { preview = SvgPreview.Load(FilePath); }
                catch (Exception) { PreviewError = "Preview unavailable for this SVG. You can still copy or export the original file."; }
            }
            return preview;
        }
    }
    public void ReleasePreview() { preview = null; attempted = false; PreviewError = null; }
}
