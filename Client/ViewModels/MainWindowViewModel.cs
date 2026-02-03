using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Input;
using Client.Models;
using Client.Services;

namespace Client.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly CredentialStorageService _credentialStorage;
    private readonly ApiClientService _apiClient;
    private ViewModelBase _currentView;
    
    public MainWindowViewModel()
    {
        _credentialStorage = new CredentialStorageService();
        _apiClient = new ApiClientService();
        
        UserManagementViewModel = new UserManagementViewModel(_credentialStorage);
        ExecutionViewModel = new ExecutionViewModel(_credentialStorage, _apiClient);
        ResultsViewModel = new ResultsViewModel(_credentialStorage);
        SettingsViewModel = new SettingsViewModel();
        
        _currentView = UserManagementViewModel;
        
        ShowUserManagementCommand = ReactiveCommand.Create(ShowUserManagement);
        ShowExecutionCommand = ReactiveCommand.Create(ShowExecution);
        ShowResultsCommand = ReactiveCommand.Create(ShowResults);
        ShowSettingsCommand = ReactiveCommand.Create(ShowSettings);
    }
    
    public UserManagementViewModel UserManagementViewModel { get; }
    public ExecutionViewModel ExecutionViewModel { get; }
    public ResultsViewModel ResultsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }
    
    public ICommand ShowUserManagementCommand { get; }
    public ICommand ShowExecutionCommand { get; }
    public ICommand ShowResultsCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    
    private void ShowUserManagement() 
    {
        try
        {
            // Ensure UI updates happen on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentView = UserManagementViewModel;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing user management: {ex}");
        }
    }
    
    private void ShowExecution() 
    {
        try
        {
            // Ensure UI updates happen on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentView = ExecutionViewModel;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing execution: {ex}");
        }
    }
    
    private void ShowResults() 
    {
        try
        {
            // Fetch data first (may be synchronous I/O but typically fast from local storage)
            var results = _credentialStorage.GetExecutionResults(limit: 200);
            
            // Update UI on UI thread - both results and view change together
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ResultsViewModel.Results.Clear();
                foreach (var result in results)
                {
                    ResultsViewModel.Results.Add(result);
                }
                CurrentView = ResultsViewModel;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing results: {ex}");
        }
    }
    
    private void ShowSettings() 
    {
        try
        {
            // Ensure UI updates happen on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentView = SettingsViewModel;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing settings: {ex}");
        }
    }
}
