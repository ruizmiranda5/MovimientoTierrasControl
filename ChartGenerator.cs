using System.Collections.Generic;
using System.Drawing;
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

        /// <summary>
        /// Genera un diagrama de masas (Bruckner) a partir de estaciones
        /// y masa acumulada (m³). Zonas positivas se rellenan en verde
        /// (corte sobrante) y zonas negativas en rojo (relleno necesario).
        /// </summary>
        public static void GenerateMassDiagram(List<double> estaciones, List<double> masas,
            string titulo, string outputPath)
        {
            var chart = new Chart { Size = new Size(1000, 500) };
            var area = new ChartArea("masas");
            area.AxisX.Title = "Estación (m)";
            area.AxisY.Title = "Masa acumulada (m³)";
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;
            chart.ChartAreas.Add(area);

            var sPos = new Series("corte")
            {
                ChartType = SeriesChartType.Area,
                Color = Color.FromArgb(120, 46, 204, 113),
                BorderColor = Color.FromArgb(39, 174, 96),
                BorderWidth = 2
            };
            var sNeg = new Series("relleno")
            {
                ChartType = SeriesChartType.Area,
                Color = Color.FromArgb(120, 231, 76, 60),
                BorderColor = Color.FromArgb(192, 57, 43),
                BorderWidth = 2
            };

            int n = System.Math.Min(estaciones.Count, masas.Count);
            for (int i = 0; i < n; i++)
            {
                double est = estaciones[i];
                double m = masas[i];
                sPos.Points.AddXY(est, m >= 0 ? m : 0);
                sNeg.Points.AddXY(est, m < 0 ? m : 0);
            }

            chart.Series.Add(sPos);
            chart.Series.Add(sNeg);
            chart.Titles.Add(titulo);
            chart.Legends.Add(new Legend("leg") { Docking = Docking.Bottom });

            chart.SaveImage(outputPath, ChartImageFormat.Png);
        }
    }
}
