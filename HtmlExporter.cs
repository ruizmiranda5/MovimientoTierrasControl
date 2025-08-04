using System;
using System.Collections.Generic;
using System.IO;

namespace MovimientoTierrasControl
{
    public static class HtmlExporter
    {
        public static void ExportHtmlWithChart(string outputPath, string chartImagePath, string titulo, string textoAdicional)
        {
            string base64Chart = Convert.ToBase64String(File.ReadAllBytes(chartImagePath));
            string html = $@"
<!DOCTYPE html>
<html lang=\"es\">
<head>
    <meta charset=\"UTF-8\">
    <title>{titulo}</title>
</head>
<body>
    <h1>{titulo}</h1>
    <p>{textoAdicional}</p>
    <img src=\"data:image/png;base64,{base64Chart}\" alt=\"Gráfico\" style=\"width:600px; height:auto; border:1px solid #ccc;\"/>
</body>
</html>";
            File.WriteAllText(outputPath, html);
        }
    }
}