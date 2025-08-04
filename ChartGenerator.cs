using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;

namespace MovimientoTierrasControl
{
    public static class ChartGenerator
    {
        public static void GenerateChart(List<double> datos, string titulo, string colorHex, string outputPath)
        {
            var chart = new Chart();
            chart.Size = new Size(600, 400);
            var chartArea = new ChartArea();
            chart.ChartAreas.Add(chartArea);

            var series = new Series
            {
                ChartType = SeriesChartType.Line,
                Color = ColorTranslator.FromHtml(colorHex)
            };

            for (int i = 0; i < datos.Count; i++)
                series.Points.AddXY(i + 1, datos[i]);

            chart.Series.Add(series);
            chart.Titles.Add(titulo);

            chart.SaveImage(outputPath, ChartImageFormat.Png);
        }
    }
}