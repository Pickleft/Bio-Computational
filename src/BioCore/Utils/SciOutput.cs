using System.Text;

namespace BioComp.Core.Utils;

/// <summary>Minimal scientific output: CSV tables and SVG line plots, zero dependencies.</summary>
public static class SciOutput
{
    public static void WriteCsv(string path, string[] header, IEnumerable<object[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>One or more series on a single line chart. Series = (name, points[x,y]).</summary>
    public static void WriteSvg(string path, string title, string xLabel, string yLabel,
        float xMin, float xMax, (string Name, List<(float X, float Y)> Points)[] series)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        float yMin = float.MaxValue, yMax = float.MinValue;
        foreach (var s in series)
            foreach (var (_, y) in s.Points)
            {
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
        if (yMax - yMin < 1e-6f) { yMin -= 1; yMax += 1; }

        const int w = 640, h = 400, padL = 64, padR = 20, padT = 40, padB = 48;
        float Px(float x) => padL + (x - xMin) / (xMax - xMin) * (w - padL - padR);
        float Py(float y) => h - padB - (y - yMin) / (yMax - yMin) * (h - padT - padB);
        string[] colors = ["#46e0c0", "#e0a546", "#9d7ce0", "#e05a6a"];

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>");
        sb.AppendLine($"<rect width='{w}' height='{h}' fill='#0c111a'/>");
        sb.AppendLine($"<text x='{w / 2}' y='22' fill='#c9d6e8' font-family='Consolas' font-size='15' text-anchor='middle'>{Esc(title)}</text>");
        sb.AppendLine($"<line x1='{padL}' y1='{h - padB}' x2='{w - padR}' y2='{h - padB}' stroke='#33445c'/>");
        sb.AppendLine($"<line x1='{padL}' y1='{padT}' x2='{padL}' y2='{h - padB}' stroke='#33445c'/>");
        sb.AppendLine($"<text x='{w / 2}' y='{h - 10}' fill='#5c6c84' font-family='Consolas' font-size='12' text-anchor='middle'>{Esc(xLabel)}</text>");
        sb.AppendLine($"<text x='14' y='{h / 2}' fill='#5c6c84' font-family='Consolas' font-size='12' transform='rotate(-90 14 {h / 2})'>{Esc(yLabel)}</text>");
        sb.AppendLine($"<text x='{padL - 6}' y='{padT + 4}' fill='#5c6c84' font-family='Consolas' font-size='11' text-anchor='end'>{yMax:F2}</text>");
        sb.AppendLine($"<text x='{padL - 6}' y='{h - padB}' fill='#5c6c84' font-family='Consolas' font-size='11' text-anchor='end'>{yMin:F2}</text>");
        sb.AppendLine($"<text x='{padL}' y='{h - padB + 16}' fill='#5c6c84' font-family='Consolas' font-size='11'>{xMin:F0}</text>");
        sb.AppendLine($"<text x='{w - padR}' y='{h - padB + 16}' fill='#5c6c84' font-family='Consolas' font-size='11' text-anchor='end'>{xMax:F0}</text>");

        for (int i = 0; i < series.Length; i++)
        {
            var s = series[i];
            string pts = string.Join(" ", s.Points.Select(p => $"{Px(p.X):F1},{Py(p.Y):F1}"));
            sb.AppendLine($"<polyline points='{pts}' fill='none' stroke='{colors[i % colors.Length]}' stroke-width='2'/>");
            sb.AppendLine($"<text x='{padL + 8}' y='{padT + 14 + i * 16}' fill='{colors[i % colors.Length]}' font-family='Consolas' font-size='12'>{Esc(s.Name)}</text>");
        }
        sb.AppendLine("</svg>");
        File.WriteAllText(path, sb.ToString());
    }

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
