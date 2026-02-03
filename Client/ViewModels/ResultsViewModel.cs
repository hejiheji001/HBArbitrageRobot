using ReactiveUI;
using System.Collections.ObjectModel;
using Client.Models;
using Client.Services;

namespace Client.ViewModels;

public class ResultsViewModel : ViewModelBase
{
    private readonly CredentialStorageService _credentialStorage;
    private ExecutionResult? _selectedResult;

    public ResultsViewModel(CredentialStorageService credentialStorage)
    {
        _credentialStorage = credentialStorage;
        Results = new ObservableCollection<ExecutionResult>();
        RefreshResults();
    }

    public ObservableCollection<ExecutionResult> Results { get; }

    public ExecutionResult? SelectedResult
    {
        get => _selectedResult;
        set => this.RaiseAndSetIfChanged(ref _selectedResult, value);
    }

    public void RefreshResults()
    {
        // Ensure UI updates happen on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Results.Clear();
            var results = _credentialStorage.GetExecutionResults(limit: 200);
            foreach (var result in results)
            {
                Results.Add(result);
            }
        });
    }
}
