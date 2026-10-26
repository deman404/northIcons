using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace NorthIcons;

public static class SvgPreview
{
    public static DrawingImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }
    public static DrawingImage Load(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Empty SVG");
        foreach (var style in root.Descendants().Where(e => e.Name.LocalName == "style").ToList())
        {
            foreach (Match rule in Regex.Matches(style.Value, @"\.([a-zA-Z0-9_-]+)\s*\{([^}]+)\}"))
                foreach (var element in root.Descendants().Where(e => ((string?)e.Attribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(rule.Groups[1].Value)))
                    element.SetAttributeValue("style", rule.Groups[2].Value + ";" + ((string?)element.Attribute("style") ?? ""));
        }
        var group = Read(root, Brushes.Black, null);
        var box = Numbers((string?)root.Attribute("viewBox") ?? "0 0 24 24");
        if (box.Length != 4 || box[2] <= 0 || box[3] <= 0) throw new InvalidDataException("Invalid viewBox");
        var drawing = new DrawingGroup();
        // Transparent viewBox preserves the original SVG proportions and whitespace.
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(box[0], box[1], box[2], box[3]))));
        drawing.Children.Add(group);
        drawing.ClipGeometry = new RectangleGeometry(new Rect(box[0], box[1], box[2], box[3]));
        drawing.Freeze();
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
    static DrawingGroup Read(XElement node, Brush? inherited, Pen? inheritedPen)
    {
        string? Attr(string key) => (string?)node.Attribute(key) ?? ((string?)node.Attribute("style"))?.Split(';').Select(x => x.Split(':', 2)).FirstOrDefault(x => x.Length == 2 && x[0].Trim() == key)?[1].Trim();
        var fill = Attr("fill") is { } color ? Paint(color) : inherited;
        var stroke = Attr("stroke") is { } strokeColor ? Paint(strokeColor) : inheritedPen?.Brush;
        var pen = stroke == null ? null : new Pen(stroke, Number(Attr("stroke-width"), inheritedPen?.Thickness ?? 1)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var result = new DrawingGroup { Opacity = Math.Clamp(Number(Attr("opacity"), 1), 0, 1) };
        if (node.Name.LocalName is "defs" or "style") return result;
        if (Attr("display") == "none" || Attr("visibility") == "hidden") return result;
        double N(string attr, double fallback = 0) => Number(Attr(attr), fallback);
        Geometry? shape = node.Name.LocalName switch
        {
            "path" when Attr("d") is { Length: > 0 } d => Geometry.Parse("F1 " + d),
            "rect" => new RectangleGeometry(new Rect(N("x"), N("y"), Math.Max(0, N("width")), Math.Max(0, N("height"))), N("rx"), N("ry", N("rx"))),
            "circle" => new EllipseGeometry(new Point(N("cx"), N("cy")), N("r"), N("r")),
            "ellipse" => new EllipseGeometry(new Point(N("cx"), N("cy")), N("rx"), N("ry")),
            "line" => new LineGeometry(new Point(N("x1"), N("y1")), new Point(N("x2"), N("y2"))),
            "polygon" or "polyline" => Points(Attr("points") ?? "", node.Name.LocalName == "polygon"),
            "svg" or "g" or "title" or "desc" or "metadata" => null,
            _ => throw new NotSupportedException("SVG contains an unsupported preview feature: " + node.Name.LocalName)
        };
        if (shape != null)
        {
            if (Attr("fill-rule") == "evenodd") { var path = PathGeometry.CreateFromGeometry(shape); path.FillRule = FillRule.EvenOdd; shape = path; }
            result.Children.Add(new GeometryDrawing(fill, pen, shape));
        }
        if (node.Name.LocalName is "svg" or "g") foreach (var child in node.Elements()) result.Children.Add(Read(child, fill, pen));
        if (Attr("transform") is { Length: > 0 } transform) result.Transform = TransformSvg(transform);
        return result;
    }
    static Geometry Points(string value, bool closed)
    {
        var numbers = Numbers(value); if (numbers.Length < 4 || numbers.Length % 2 != 0) throw new InvalidDataException("Invalid SVG points");
        return Geometry.Parse("F1 M" + string.Join(" ", numbers.Select(x => x.ToString(CultureInfo.InvariantCulture))) + (closed ? " Z" : ""));
    }
    static Transform TransformSvg(string value)
    {
        var transforms = new TransformGroup();
        foreach (Match match in Regex.Matches(value, @"([a-zA-Z]+)\s*\(([^)]+)\)"))
        {
            var n = Numbers(match.Groups[2].Value);
            transforms.Children.Insert(0, match.Groups[1].Value switch
            {
                "translate" => new TranslateTransform(n[0], n.Length > 1 ? n[1] : 0),
                "scale" => new ScaleTransform(n[0], n.Length > 1 ? n[1] : n[0]),
                "rotate" => new RotateTransform(n[0], n.Length > 2 ? n[1] : 0, n.Length > 2 ? n[2] : 0),
                "matrix" when n.Length == 6 => new MatrixTransform(new Matrix(n[0], n[1], n[2], n[3], n[4], n[5])),
                "skewX" => new SkewTransform(n[0], 0), "skewY" => new SkewTransform(0, n[0]),
                _ => throw new NotSupportedException("Unsupported SVG transform")
            });
        }
        return transforms;
    }
    static Brush? Paint(string value)
    {
        if (value == "none") return null;
        if (value is "currentColor" or "inherit") return Brushes.Black;
        if (Regex.IsMatch(value, @"^rgba?\(", RegexOptions.IgnoreCase))
        {
            var parts = value[(value.IndexOf('(') + 1)..value.LastIndexOf(')')].Split(',');
            byte Channel(string s) => (byte)Math.Clamp(Math.Round(Number(s.Trim().TrimEnd('%'), 0) * (s.Contains('%') ? 2.55 : 1)), 0, 255);
            if (parts.Length is 3 or 4) return new SolidColorBrush(Color.FromArgb(parts.Length == 4 ? (byte)Math.Clamp(Number(parts[3], 1) * 255, 0, 255) : (byte)255, Channel(parts[0]), Channel(parts[1]), Channel(parts[2])));
        }
        return (Brush?)new BrushConverter().ConvertFromInvariantString(value) ?? Brushes.Black;
    }
    static double Number(string? value, double fallback) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : fallback;
    static double[] Numbers(string value) => Regex.Matches(value, @"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
}
