using Avalonia.Controls;

namespace Client.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is Client.ViewModels.UserManagementViewModel viewModel)
        {
            viewModel.RefreshUsers();
        }
    }
}
