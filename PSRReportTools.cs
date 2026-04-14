using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Generación de reportes HTML con gráficos para PSR:
    ///   • Diagrama de masas (Bruckner)
    ///   • Reporte ejecutivo de volúmenes
    ///   • Comparativa corte/relleno por tramo
    /// </summary>
    public class PSRReportTools
    {
        /// <summary>
        /// Lee un CSV de cubicación generado por PSR_CUBICACION_SECCIONES
        /// (o equivalente) y genera el diagrama de masas en PNG + HTML.
        /// </summary>
        [CommandMethod("PSR_DIAGRAMA_MASAS")]
        public void GenerarDiagramaMasas()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                var opts = new PromptStringOptions("\nRuta del CSV de cubicación: ");
                opts.AllowSpaces = true;
                var r = ed.GetString(opts);
                if (r.Status != PromptStatus.OK) return;
                string csv = r.StringResult.Trim('"');
                if (!File.Exists(csv))
                {
                    ed.WriteMessage("\n[PSR] CSV no encontrado.");
                    return;
                }

                var estaciones = new List<double>();
                var masas = new List<double>();
                bool header = true;
                foreach (string line in File.ReadAllLines(csv))
                {
                    if (header) { header = false; continue; }
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    string[] p = line.Split(',');
                    if (p.Length < 8) continue;
                    double est, m;
                    if (double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out est) &&
                        double.TryParse(p[7], NumberStyles.Float, CultureInfo.InvariantCulture, out m))
                    {
                        estaciones.Add(est);
                        masas.Add(m);
                    }
                }

                if (masas.Count == 0)
                {
                    ed.WriteMessage("\n[PSR] No se encontraron datos de masa acumulada.");
                    return;
                }

                PSRCivilHelpers.EnsureOutputDir();
                string png = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_masas_{1:yyyyMMdd-HHmm}.png",
                        PSRConfig.ProjectCode, DateTime.Now));
                string html = Path.ChangeExtension(png, ".html");

                ChartGenerator.GenerateMassDiagram(estaciones, masas,
                    "Diagrama de masas - " + PSRConfig.ProjectName, png);

                HtmlExporter.ExportHtmlWithChart(html, png,
                    "Diagrama de masas - " + PSRConfig.ProjectName,
                    string.Format("Puntos: {0} | Min: {1:N2} m³ | Max: {2:N2} m³",
                        masas.Count, MinOf(masas), MaxOf(masas)));

                ed.WriteMessage("\n[PSR] Diagrama de masas generado:\n  " + png + "\n  " + html);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_DIAGRAMA_MASAS: " + ex.Message);
            }
        }

        /// <summary>
        /// Genera un reporte ejecutivo HTML con los volúmenes del último
        /// CSV de cubicación que encuentre en la carpeta de salida PSR.
        /// </summary>
        [CommandMethod("PSR_REPORTE_EJECUTIVO")]
        public void ReporteEjecutivo()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                PSRCivilHelpers.EnsureOutputDir();
                string[] csvs = Directory.GetFiles(PSRConfig.CarpetaSalida,
                    PSRConfig.ProjectCode + "_cubicacion_*.csv");
                if (csvs.Length == 0)
                {
                    ed.WriteMessage("\n[PSR] No hay CSV de cubicación en " + PSRConfig.CarpetaSalida);
                    return;
                }
                Array.Sort(csvs);
                string ultimo = csvs[csvs.Length - 1];

                double vCutMax = 0, vFillMax = 0, masaMax = 0, masaMin = 0;
                int nFilas = 0;
                foreach (string line in File.ReadAllLines(ultimo))
                {
                    if (line.StartsWith("estacion") || line.StartsWith("#") ||
                        string.IsNullOrWhiteSpace(line)) continue;
                    string[] p = line.Split(',');
                    if (p.Length < 8) continue;
                    double vca, vfa, masa;
                    if (double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out vca) &&
                        double.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out vfa) &&
                        double.TryParse(p[7], NumberStyles.Float, CultureInfo.InvariantCulture, out masa))
                    {
                        vCutMax = vca; vFillMax = vfa;
                        if (masa > masaMax) masaMax = masa;
                        if (masa < masaMin) masaMin = masa;
                        nFilas++;
                    }
                }

                string html = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_reporte_{1:yyyyMMdd-HHmm}.html",
                        PSRConfig.ProjectCode, DateTime.Now));

                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"UTF-8\">");
                sb.Append("<title>").Append(PSRConfig.ProjectName).Append(" - Reporte</title>");
                sb.Append("<style>body{font-family:Arial;padding:20px}table{border-collapse:collapse}");
                sb.Append("td,th{border:1px solid #888;padding:6px 12px}th{background:#eee}</style></head><body>");
                sb.Append("<h1>").Append(PSRConfig.ProjectName).Append("</h1>");
                sb.Append("<h2>Reporte ejecutivo de movimiento de tierras</h2>");
                sb.AppendFormat("<p>Fuente: {0}<br>Secciones: {1}</p>", ultimo, nFilas);
                sb.Append("<table>");
                sb.Append("<tr><th>Concepto</th><th>Valor</th><th>Unidad</th></tr>");
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<tr><td>Volumen total de corte</td><td>{0:N2}</td><td>m³</td></tr>", vCutMax);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<tr><td>Volumen total de relleno</td><td>{0:N2}</td><td>m³</td></tr>", vFillMax);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<tr><td>Volumen neto</td><td>{0:N2}</td><td>m³</td></tr>", vCutMax - vFillMax);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<tr><td>Masa acumulada máxima</td><td>{0:N2}</td><td>m³</td></tr>", masaMax);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "<tr><td>Masa acumulada mínima</td><td>{0:N2}</td><td>m³</td></tr>", masaMin);
                sb.Append("</table></body></html>");

                File.WriteAllText(html, sb.ToString());
                ed.WriteMessage("\n[PSR] Reporte generado: " + html);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_REPORTE_EJECUTIVO: " + ex.Message);
            }
        }

        private static double MinOf(List<double> xs)
        {
            double m = double.MaxValue;
            foreach (double x in xs) if (x < m) m = x;
            return m;
        }

        private static double MaxOf(List<double> xs)
        {
            double m = double.MinValue;
            foreach (double x in xs) if (x > m) m = x;
            return m;
        }
    }
}
