using System;
using System.IO;
using System.Text;

namespace MovimientoTierrasControl
{
    public static class HtmlExporter
    {
        public static void ExportHtmlWithChart(string outputPath, string chartImagePath,
            string titulo, string textoAdicional)
        {
            string base64Chart = Convert.ToBase64String(File.ReadAllBytes(chartImagePath));
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"es\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.Append("    <title>").Append(titulo).AppendLine("</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("      body{font-family:Arial,sans-serif;padding:20px;color:#222}");
            sb.AppendLine("      h1{color:#2c3e50;border-bottom:2px solid #3498db;padding-bottom:6px}");
            sb.AppendLine("      img{max-width:100%;border:1px solid #ccc;border-radius:4px}");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.Append("    <h1>").Append(titulo).AppendLine("</h1>");
            sb.Append("    <p>").Append(textoAdicional).AppendLine("</p>");
            sb.Append("    <img src=\"data:image/png;base64,")
              .Append(base64Chart)
              .AppendLine("\" alt=\"Gráfico\"/>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}
