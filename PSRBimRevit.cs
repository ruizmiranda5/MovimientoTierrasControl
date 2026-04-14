using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Intercambio de coordenadas compartidas con Revit / Infraworks.
    /// Genera un XML con el Base Point, Survey Point, rotación al
    /// Norte geodésico y sistema de coordenadas, que puede leerse
    /// manualmente desde Revit para alinear los modelos federados.
    /// </summary>
    public class PSRBimRevit
    {
        /// <summary>
        /// Exporta las coordenadas compartidas del DWG actual a XML
        /// (formato PSR). Requiere que el DWG esté georreferenciado
        /// (usar <c>PSR_BIM_GEOLOCALIZAR</c> antes).
        /// </summary>
        [CommandMethod("PSR_BIM_REVIT_COORDS")]
        public void ExportarCoordenadasCompartidas()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                GeoLocationData gld;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId gid = db.GeoDataObject;
                    if (gid.IsNull)
                    {
                        ed.WriteMessage("\n[PSR-BIM] El DWG no está georreferenciado. Ejecuta PSR_BIM_GEOLOCALIZAR.");
                        tr.Commit();
                        return;
                    }
                    gld = (GeoLocationData)tr.GetObject(gid, OpenMode.ForRead);

                    PSRCivilHelpers.EnsureOutputDir();
                    string path = Path.Combine(PSRConfig.CarpetaSalida,
                        string.Format("{0}_revit_shared_{1:yyyyMMdd-HHmm}.xml",
                            PSRConfig.ProjectCode, DateTime.Now));

                    var sb = new StringBuilder();
                    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                    sb.AppendLine("<PsrSharedCoordinates version=\"1.0\">");
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <Project>{0}</Project>\n", PSRConfig.ProjectName);
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <CoordinateSystem>{0}</CoordinateSystem>\n", gld.CoordinateSystem);
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <BasePoint x=\"{0:F6}\" y=\"{1:F6}\" z=\"{2:F6}\"/>\n",
                        gld.DesignPoint.X, gld.DesignPoint.Y, gld.DesignPoint.Z);
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <SurveyPoint latitude=\"{0:F10}\" longitude=\"{1:F10}\" elevation=\"{2:F4}\"/>\n",
                        gld.ReferencePoint.Y, gld.ReferencePoint.X, gld.ReferencePoint.Z);
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <NorthDirection radians=\"{0:F10}\"/>\n", gld.NorthDirection);
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <HorizontalUnits>{0}</HorizontalUnits>\n", gld.HorizontalUnits);
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "  <VerticalUnits>{0}</VerticalUnits>\n", gld.VerticalUnits);
                    sb.AppendLine("</PsrSharedCoordinates>");
                    File.WriteAllText(path, sb.ToString());
                    ed.WriteMessage("\n[PSR-BIM] Coordenadas compartidas exportadas: " + path);
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_REVIT_COORDS: " + ex.Message);
            }
        }
    }
}
