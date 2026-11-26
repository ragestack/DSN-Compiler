// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Execution;

namespace DSN.Viewer;

internal sealed partial class ExecutionPanel
{
    private void RefreshImplementations(string? preferred = null)
    {
        var available = ExecutionImplementation.Available(assetsRoot);
        var selected = available.Contains(preferred ?? activeImplementation, StringComparer.Ordinal)
            ? preferred ?? activeImplementation : available.FirstOrDefault();
        updatingImplementation = true;
        try
        {
            implementationChoice.Items.Clear();
            foreach (var id in available) implementationChoice.Items.Add(id);
            implementationChoice.SelectedItem = selected;
            implementationChoice.IsEnabled = available.Length > 0;
            activeImplementation = selected ?? "";
        }
        finally { updatingImplementation = false; }
    }
}
