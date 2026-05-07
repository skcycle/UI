using System.Windows.Controls;
using System.Windows.Input;

namespace MotionControl.App.Controls;

public partial class MagazinePanel : UserControl
{
    public MagazinePanel()
    {
        InitializeComponent();
    }

    private void SectionDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollHelper.HandleDataGridMouseWheel(sender, e, this);
    }
}
