using System.Globalization;
using System.Text;

namespace Visary.Core.Export;

/// <summary>Writes a PDF with one full bleed JPEG per page. Used for PDF export of rendered slides.</summary>
public static class PdfWriter
{
    public sealed record Page(byte[] Jpeg, int PixelWidth, int PixelHeight);

    /// <param name="pageWidth">Page size in points (1/72 inch).</param>
    public static void Write(Stream output, IReadOnlyList<Page> pages, double pageWidth, double pageHeight, string title)
    {
        if (pages.Count == 0) throw new ArgumentException("A PDF needs at least one page.", nameof(pages));
        var offsets = new List<long>();
        var pos = 0L;
        void Raw(byte[] b)
        {
            output.Write(b);
            pos += b.Length;
        }
        void Text(string s) => Raw(Encoding.ASCII.GetBytes(s));
        void Obj(int n, string body)
        {
            offsets.Add(pos);
            Text($"{n} 0 obj\n{body}\nendobj\n");
        }
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        Text("%PDF-1.4\n%âãÏÓ\n".Normalize());
        // Objects: 1 catalog, 2 pages, 3 info, then per page: page, image, content.
        var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{4 + i * 3} 0 R"));
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        Obj(3, $"<< /Title ({Escape(title)}) /Producer (Visary PRSNT) >>");
        for (var i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            int page = 4 + i * 3, image = page + 1, content = page + 2;
            Obj(page, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pageWidth)} {F(pageHeight)}] /Resources << /XObject << /Im0 {image} 0 R >> >> /Contents {content} 0 R >>");
            offsets.Add(pos);
            Text($"{image} 0 obj\n<< /Type /XObject /Subtype /Image /Width {p.PixelWidth} /Height {p.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {p.Jpeg.Length} >>\nstream\n");
            Raw(p.Jpeg);
            Text("\nendstream\nendobj\n");
            var draw = $"q {F(pageWidth)} 0 0 {F(pageHeight)} 0 0 cm /Im0 Do Q";
            Obj(content, $"<< /Length {draw.Length} >>\nstream\n{draw}\nendstream");
        }
        var xref = pos;
        var sb = new StringBuilder();
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 3 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Text(sb.ToString());
    }

    static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (ch is '(' or ')' or '\\') sb.Append('\\').Append(ch);
            else if (ch < 32 || ch > 126) sb.Append('?');
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}
