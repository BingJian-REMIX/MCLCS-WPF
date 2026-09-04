using System.Windows.Controls;
using MCLCS.App.ViewModels;

namespace MCLCS.App.Views;

public partial class PerfView : UserControl
{
    public PerfView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as PerfViewModel)?.Dispose();
    }
}
