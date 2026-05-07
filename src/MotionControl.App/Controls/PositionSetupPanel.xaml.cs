using System.Windows.Controls;
using System.Windows.Input;

namespace MotionControl.App.Controls;

public partial class PositionSetupPanel : UserControl
{
    public PositionSetupPanel()
    {
        InitializeComponent();
    }

    private void SectionDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollHelper.HandleDataGridMouseWheel(sender, e, this);
    }
}
