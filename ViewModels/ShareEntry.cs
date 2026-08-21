using CommunityToolkit.Mvvm.ComponentModel;

namespace MultiSmbServer.ViewModels;

public partial class ShareEntry : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string path = string.Empty;
}
