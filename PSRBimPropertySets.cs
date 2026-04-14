using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

#if AEC_PROPDATA
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
#endif

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Gestión de AEC Property Sets para flujo BIM → IFC. Los Property
    /// Sets de Civil 3D/AutoCAD Architecture se exportan como
    /// <c>IfcPropertySet</c> al hacer IFCEXPORT, convirtiéndose en la
    /// forma estándar de llevar metadatos PSR al modelo federado.
    ///
    /// Esta clase se compila solo cuando el símbolo <c>AEC_PROPDATA</c>
    /// está definido y la referencia <c>AecPropDataMgd.dll</c> está
    /// disponible. En proyectos sin esa DLL, la clase queda inerte
    /// pero sigue siendo compilable.
    /// </summary>
    public class PSRBimPropertySets
    {
        /// <summary>
        /// Crea (si no existe) una definición de Property Set llamada
        /// "PSR_BIM" con los campos Proyecto, Fase, Disciplina, LOD y
        /// Codigo, y la asocia al objeto seleccionado por el usuario.
        /// </summary>
        [CommandMethod("PSR_BIM_PSET")]
        public void CrearPsetPsr()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

#if AEC_PROPDATA
            try
            {
                var res = ed.GetEntity("\nSelecciona entidad para aplicar PSR_BIM PSet: ");
                if (res.Status != PromptStatus.OK) return;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DictionaryPropertySetDefinitions dict = new DictionaryPropertySetDefinitions(db);
                    ObjectId defId;
                    if (dict.Has("PSR_BIM", tr))
                    {
                        defId = dict.GetAt("PSR_BIM");
                    }
                    else
                    {
                        var def = new PropertySetDefinition();
                        def.SetToStandard(db);
                        def.SubSetDatabaseDefaults(db);
                        def.AlternateName = "PSR_BIM";
                        def.AppliesToAll = true;
                        AddManual(def, "Proyecto", PSRConfig.ProjectName);
                        AddManual(def, "Fase", "Movimiento de tierras");
                        AddManual(def, "Disciplina", "Topografía");
                        AddManual(def, "LOD", "LOD300");
                        AddManual(def, "Codigo", PSRConfig.ProjectCode);
                        defId = dict.AddNewRecord("PSR_BIM", def);
                        tr.AddNewlyCreatedDBObject(def, true);
                    }

                    DBObject ent = tr.GetObject(res.ObjectId, OpenMode.ForWrite);
                    ObjectId psId = PropertyDataServices.AddPropertySet(ent, defId);
                    ed.WriteMessage("\n[PSR-BIM] PSet PSR_BIM aplicado. ObjectId PSet: " + psId);
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_PSET: " + ex.Message);
            }
#else
            ed.WriteMessage("\n[PSR-BIM] PSR_BIM_PSET requiere compilar con /p:DefineConstants=AEC_PROPDATA y la referencia AecPropDataMgd.dll.");
#endif
        }

#if AEC_PROPDATA
        private static void AddManual(PropertySetDefinition def, string name, string defaultValue)
        {
            var p = new PropertyDefinition();
            p.SetToStandard(def.Database);
            p.SubSetDatabaseDefaults(def.Database);
            p.Name = name;
            p.DataType = Autodesk.Aec.PropertyData.DataType.Text;
            p.DefaultData = defaultValue;
            def.Definitions.Add(p);
        }
#endif
    }
}
