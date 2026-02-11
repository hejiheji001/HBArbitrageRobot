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
        Results = [];
        _credentialStorage.ExecutionResultSaved += OnExecutionResultSaved;
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
        // Fetch data first (may be synchronous I/O but typically fast from local storage)
        var results = _credentialStorage.GetExecutionResults(limit: 200);
        
        // Update UI collection on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }
        });
    }

    private void OnExecutionResultSaved(object? sender, ExecutionResult e)
    {
        RefreshResults();
    }
}
