using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Toolbox;

namespace MCLCS.App.ViewModels;

/// <summary>网络诊断面板：检测各服务端点连通性与延迟。</summary>
public class NetworkDiagViewModel : ObservableObject
{
    private ObservableCollection<DiagnosticResult> _results = new();
    private bool _isBusy;

    public ObservableCollection<DiagnosticResult> Results
    {
        get => _results;
        set => SetField(ref _results, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public ICommand DiagnoseCommand { get; }

    public NetworkDiagViewModel()
    {
        DiagnoseCommand = new AsyncRelayCommand(_ => DiagnoseAsync(), _ => !IsBusy);
        _ = DiagnoseAsync();
    }

    private async Task DiagnoseAsync()
    {
        IsBusy = true;
        try
        {
            var list = await NetworkDiagnostics.DiagnoseAsync(null, new HttpClient());
            Results = new ObservableCollection<DiagnosticResult>(list);
        }
        catch (Exception ex)
        {
            Results = new ObservableCollection<DiagnosticResult>
            {
                new() { Name = "诊断失败", Reachable = false, Error = ex.Message }
            };
        }
        finally
        {
            IsBusy = false;
        }
    }
}
