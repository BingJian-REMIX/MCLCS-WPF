using System.Windows.Controls;
using System.Windows.Input;
using MCLCS.App.ViewModels;

namespace MCLCS.App.Views;

public partial class GameView : UserControl
{
    public GameView()
    {
        InitializeComponent();
        DataContext = new GameViewModel();
    }

    private void AnnualReportCard_Click(object sender, MouseButtonEventArgs e)
    {
        ((GameViewModel)DataContext).OpenAnnualReportCommand.Execute(null);
    }
}
