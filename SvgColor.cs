using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NorthIcons;

public static class SvgColor
{
    public static bool TryNormalize(string value, out string hex)
    {
        hex = value.Trim().TrimStart('#');
        if (!Regex.IsMatch(hex, @"\A(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\z")) { hex = ""; return false; }
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        hex = "#" + hex.ToUpperInvariant(); return true;
    }
    public static string Apply(string svg, string color)
    {
        if (!TryNormalize(color, out var hex)) throw new ArgumentException("Use a hex color such as #087F8C.", nameof(color));
        using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var doc = XDocument.Load(reader);
        var root = doc.Root ?? throw new InvalidDataException("Empty SVG");
        string Paint(string value) => value.Trim().ToLowerInvariant() is "none" or "transparent" ? value : hex;
        string Style(string value) => Regex.Replace(value, @"(?<key>\b(?:fill|stroke|stop-color|color))\s*:\s*(?<paint>[^;}]+)",
            m => m.Groups["key"].Value + ":" + Paint(m.Groups["paint"].Value.Replace("!important", "", StringComparison.OrdinalIgnoreCase)) + (m.Groups["paint"].Value.Contains("!important", StringComparison.OrdinalIgnoreCase) ? " !important" : ""), RegexOptions.IgnoreCase);
        if (root.Attribute("fill") == null) root.SetAttributeValue("fill", hex);
        root.SetAttributeValue("color", hex);
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName is "fill" or "stroke" or "stop-color" or "color").ToList()) attribute.Value = Paint(attribute.Value);
            if (element.Attribute("style") is { } style) style.Value = Style(style.Value);
            if (element.Name.LocalName == "style") element.Value = Style(element.Value);
        }
        return doc.ToString(SaveOptions.DisableFormatting);
    }
}
