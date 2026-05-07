using System.Windows.Controls;
using System.Windows.Input;

namespace MotionControl.App.Controls;

public partial class CylinderMonitorPanel : UserControl
{
    public CylinderMonitorPanel()
    {
        InitializeComponent();
    }

    private void SectionDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollHelper.HandleDataGridMouseWheel(sender, e, this);
    }
}
