using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Importación y exportación de LandXML para intercambio con
    /// Trimble Business Center, Leica, Topcon, 12d y otros paquetes.
    /// </summary>
    public class PSRLandXmlTools
    {
        /// <summary>
        /// Exporta todo el proyecto (superficies, alineamientos, perfiles,
        /// parcelas, puntos) a LandXML en la carpeta de salida PSR.
        /// </summary>
        [CommandMethod("PSR_EXPORTAR_LANDXML")]
        public void ExportarLandXml()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                PSRCivilHelpers.EnsureOutputDir();
                string path = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_{1:yyyyMMdd-HHmm}.xml",
                        PSRConfig.ProjectCode, DateTime.Now));

                // LandXMLExport settings file por defecto de Civil 3D.
                // Si no se puede usar la API directa, se recurre al comando.
                try
                {
                    doc.SendStringToExecute(
                        string.Format("_.-LandXMLOut \"{0}\" _Yes ", path), true, false, true);
                    ed.WriteMessage("\n[PSR] Exportación LandXML iniciada: " + path);
                }
                catch (System.Exception inner)
                {
                    ed.WriteMessage("\n[PSR] LandXMLOut falló: " + inner.Message);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_EXPORTAR_LANDXML: " + ex.Message);
            }
        }

        /// <summary>
        /// Importa un LandXML pidiendo la ruta al usuario.
        /// </summary>
        [CommandMethod("PSR_IMPORTAR_LANDXML")]
        public void ImportarLandXml()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                var opts = new PromptStringOptions("\nRuta del LandXML: ");
                opts.AllowSpaces = true;
                var r = ed.GetString(opts);
                if (r.Status != PromptStatus.OK) return;
                string path = r.StringResult.Trim('"');
                if (!File.Exists(path))
                {
                    ed.WriteMessage("\n[PSR] Archivo no encontrado.");
                    return;
                }
                doc.SendStringToExecute(
                    string.Format("_.-LandXMLIn \"{0}\" ", path), true, false, true);
                ed.WriteMessage("\n[PSR] LandXML importado: " + path);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_IMPORTAR_LANDXML: " + ex.Message);
            }
        }
    }
}
