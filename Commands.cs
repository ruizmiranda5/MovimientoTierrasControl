using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Comandos de arranque del plugin PSR. El resto de comandos PSR_*
    /// están definidos en los archivos PSR*Tools.cs.
    /// </summary>
    public class Commands
    {
        [CommandMethod("PSR_MENU")]
        public void ShowPsrMenu()
        {
            Application.ShowModelessDialog(new SelectionForm());
        }

        // Alias legacy para compatibilidad con versiones anteriores.
        [CommandMethod("SHOW_FORM")]
        public void ShowSelectionForm()
        {
            Application.ShowModelessDialog(new SelectionForm());
        }
    }
}
