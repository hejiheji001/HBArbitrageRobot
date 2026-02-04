using ReactiveUI;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Models;
using Client.Services;

namespace Client.ViewModels;

public class UserManagementViewModel : ViewModelBase
{
    private readonly CredentialStorageService _credentialStorage;
    private UserCredential? _selectedUser;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _displayName = string.Empty;
    private bool _isEnabled = true;
    private bool _isEditMode;

    public UserManagementViewModel(CredentialStorageService credentialStorage)
    {
        _credentialStorage = credentialStorage;
        Users = new ObservableCollection<UserCredential>();
        
        AddUserCommand = ReactiveCommand.Create(AddUser);
        EditUserCommand = ReactiveCommand.Create<UserCredential>(EditUser);
        DeleteUserCommand = ReactiveCommand.Create<UserCredential>(DeleteUser);
        SaveUserCommand = ReactiveCommand.Create(SaveUser);
        CancelEditCommand = ReactiveCommand.Create(CancelEdit);
        ToggleEnabledCommand = ReactiveCommand.Create<UserCredential>(ToggleEnabled);
        
        RefreshUsers();
    }

    public ObservableCollection<UserCredential> Users { get; }

    public UserCredential? SelectedUser
    {
        get => _selectedUser;
        set => this.RaiseAndSetIfChanged(ref _selectedUser, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => this.RaiseAndSetIfChanged(ref _isEditMode, value);
    }

    public ICommand AddUserCommand { get; }
    public ICommand EditUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand SaveUserCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ToggleEnabledCommand { get; }


    public void RefreshUsers()
    {
        // Fetch data first (may be synchronous I/O but typically fast from local storage)
        var users = _credentialStorage.GetAllUsers();
        
        // Update UI collection on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
        });
    }

    private void AddUser()
    {
        ClearForm();
        IsEditMode = true;
    }

    private void EditUser(UserCredential? user)
    {
        if (user == null) return;

        SelectedUser = user;
        Username = user.Username;
        Password = user.DecryptedPassword ?? "";
        DisplayName = user.DisplayName ?? "";
        IsEnabled = user.IsEnabled;
        IsEditMode = true;
    }

    private void DeleteUser(UserCredential? user)
    {
        if (user == null) return;

        _credentialStorage.DeleteUser(user.Id);
        RefreshUsers();
    }

    private void ToggleEnabled(UserCredential? user)
    {
        if (user == null) return;

        user.IsEnabled = !user.IsEnabled;
        _credentialStorage.SaveUser(user);
        RefreshUsers();
    }

    private void SaveUser()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            // TODO: Show error message
            return;
        }

        var user = SelectedUser ?? new UserCredential();
        user.Username = Username;
        user.DecryptedPassword = Password;
        user.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName;
        user.IsEnabled = IsEnabled;

        _credentialStorage.SaveUser(user);
        RefreshUsers();
        CancelEdit();
    }

    private void CancelEdit()
    {
        ClearForm();
        IsEditMode = false;
    }

    private void ClearForm()
    {
        SelectedUser = null;
        Username = string.Empty;
        Password = string.Empty;
        DisplayName = string.Empty;
        IsEnabled = true;
    }
}
