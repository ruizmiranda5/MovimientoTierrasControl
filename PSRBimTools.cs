using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Herramientas BIM principales para el flujo PSR:
    ///   • Exportación a IFC 4.3 (infrastructure / roads)
    ///   • Exportación a Navisworks (.nwc)
    ///   • Adjuntado de nubes de puntos (.rcp / .rcs)
    ///   • Georreferenciación del DWG (GeoLocationData WGS84)
    ///   • Listado y recarga de referencias externas (XRefs)
    ///
    /// Muchas salidas usan comandos AutoCAD/Civil 3D a través de
    /// <c>SendStringToExecute</c> porque la API .NET pública no expone
    /// aún un punto de entrada estable para IFC/NWC.
    /// </summary>
    public class PSRBimTools
    {
        /// <summary>
        /// Exporta el modelo completo a IFC. En Civil 3D 2024 usa el
        /// comando <c>IFCEXPORT</c> con formato IFC 4.3 (infraestructuras).
        /// </summary>
        [CommandMethod("PSR_BIM_EXPORTAR_IFC")]
        public void ExportarIfc()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                PSRCivilHelpers.EnsureOutputDir();
                string path = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_{1:yyyyMMdd-HHmm}.ifc",
                        PSRConfig.ProjectCode, DateTime.Now));
                if (File.Exists(path)) File.Delete(path);

                // IFCEXPORT abre el diálogo; -IFCEXPORT es la versión
                // de línea de comando (disponible con el add-on IFC).
                doc.SendStringToExecute(
                    string.Format("_.-IFCEXPORT \"{0}\" IFC4X3 ", path), true, false, true);
                ed.WriteMessage("\n[PSR-BIM] IFC solicitado: " + path);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_EXPORTAR_IFC: " + ex.Message);
            }
        }

        /// <summary>
        /// Exporta a Navisworks Cache (.nwc) usando el comando nativo
        /// <c>NWCOUT</c> del exporter de Navisworks para AutoCAD.
        /// </summary>
        [CommandMethod("PSR_BIM_EXPORTAR_NWC")]
        public void ExportarNwc()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            try
            {
                PSRCivilHelpers.EnsureOutputDir();
                string path = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_{1:yyyyMMdd-HHmm}.nwc",
                        PSRConfig.ProjectCode, DateTime.Now));
                if (File.Exists(path)) File.Delete(path);
                doc.SendStringToExecute(
                    string.Format("_.NWCOUT \"{0}\" ", path), true, false, true);
                ed.WriteMessage("\n[PSR-BIM] NWC solicitado: " + path);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_EXPORTAR_NWC: " + ex.Message);
            }
        }

        /// <summary>
        /// Adjunta una nube de puntos Recap (.rcp o .rcs) al dibujo
        /// actual en el punto 0,0,0. Útil para flujos scan-to-BIM
        /// desde estaciones totales robóticas o escáneres.
        /// </summary>
        [CommandMethod("PSR_BIM_NUBE_PUNTOS")]
        public void AdjuntarNubePuntos()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                var opts = new PromptStringOptions("\nRuta .rcp / .rcs: ");
                opts.AllowSpaces = true;
                var r = ed.GetString(opts);
                if (r.Status != PromptStatus.OK) return;
                string path = r.StringResult.Trim('"');
                if (!File.Exists(path))
                {
                    ed.WriteMessage("\n[PSR-BIM] Archivo no encontrado.");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId defId = PointCloudDefEx.CreatePointCloudDefEx(db,
                        Path.GetFileNameWithoutExtension(path), path);

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    PointCloudEx pc = new PointCloudEx();
                    pc.SetDatabaseDefaults();
                    pc.PointCloudDefExId = defId;
                    pc.Location = Point3d.Origin;
                    ms.AppendEntity(pc);
                    tr.AddNewlyCreatedDBObject(pc, true);
                    tr.Commit();
                }
                ed.WriteMessage("\n[PSR-BIM] Nube de puntos adjuntada: " + path);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_NUBE_PUNTOS: " + ex.Message);
            }
        }

        /// <summary>
        /// Establece la georreferenciación del dibujo a partir de una
        /// latitud/longitud WGS84 que apunta al punto (0,0,0) del modelo.
        /// Necesario para colaboración BIM con Revit / Navisworks /
        /// Infraworks cuando se trabaja en coordenadas UTM.
        /// </summary>
        [CommandMethod("PSR_BIM_GEOLOCALIZAR")]
        public void Georeferenciar()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                var pLat = ed.GetDouble("\nLatitud WGS84 (°dec): ");
                if (pLat.Status != PromptStatus.OK) return;
                var pLon = ed.GetDouble("\nLongitud WGS84 (°dec): ");
                if (pLon.Status != PromptStatus.OK) return;
                var pZ = ed.GetDouble("\nElevación geoide (m): ");
                if (pZ.Status != PromptStatus.OK) return;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    GeoLocationData gld = new GeoLocationData();
                    gld.SetDatabase(db);
                    gld.DesignPoint = Point3d.Origin;
                    gld.ReferencePoint = new Point3d(pLon.Value, pLat.Value, pZ.Value);
                    gld.HorizontalUnits = UnitsValue.Meters;
                    gld.VerticalUnits = UnitsValue.Meters;
                    gld.CoordinateSystem = "WGS84.UTM-" + PSRGeodetic.UtmZone(pLon.Value) + "S";
                    gld.PostToDb();
                    tr.Commit();
                }
                ed.WriteMessage("\n[PSR-BIM] DWG georreferenciado en "
                    + pLat.Value + " / " + pLon.Value);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_GEOLOCALIZAR: " + ex.Message);
            }
        }

        /// <summary>
        /// Lista todas las referencias externas (DWG / DGN / PDF / IMG
        /// / NWC) con su ruta, estado y si están cargadas.
        /// </summary>
        [CommandMethod("PSR_BIM_LISTAR_XREFS")]
        public void ListarXrefs()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    XrefGraph xg = db.GetHostDwgXrefGraph(true);
                    int n = xg.NumNodes;
                    ed.WriteMessage("\n[PSR-BIM] XRefs encontradas: " + (n - 1));
                    for (int i = 0; i < n; i++)
                    {
                        XrefGraphNode nd = xg.GetXrefNode(i);
                        if (nd == null || nd.XrefStatus == XrefStatus.NotAnXref) continue;
                        ed.WriteMessage(string.Format(
                            "\n  • {0} | {1} | {2}",
                            nd.Name, nd.XrefStatus,
                            nd.Database != null ? nd.Database.Filename : ""));
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_LISTAR_XREFS: " + ex.Message);
            }
        }
    }
}
