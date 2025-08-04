using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System.Windows.Forms;

namespace MovimientoTierrasControl
{
    public class Commands
    {
        [CommandMethod("SHOW_FORM")]
        public void ShowSelectionForm()
        {
            Application.ShowModelessDialog(new SelectionForm());
        }
    }
}