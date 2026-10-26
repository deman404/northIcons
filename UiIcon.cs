using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace NorthIcons;

public sealed class UiIcon : FrameworkElement
{
    static readonly Dictionary<string, ImageSource> cache = [];
    public static readonly DependencyProperty NameKeyProperty = DependencyProperty.Register(nameof(NameKey), typeof(string), typeof(UiIcon), new FrameworkPropertyMetadata("apps", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(UiIcon), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public string NameKey { get => (string)GetValue(NameKeyProperty); set => SetValue(NameKeyProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public UiIcon() { Width = Height = 16; IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (!cache.TryGetValue(NameKey, out var image))
        {
            var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/northIcons;component/Assets/{NameKey}.svg"));
            if (resource == null) return;
            using var stream = resource.Stream;
            image = SvgPreview.Load(stream); cache[NameKey] = image;
        }
        context.PushOpacityMask(new ImageBrush(image) { Stretch = Stretch.Uniform });
        context.DrawRectangle(Foreground, null, new Rect(RenderSize));
        context.Pop();
    }
}
