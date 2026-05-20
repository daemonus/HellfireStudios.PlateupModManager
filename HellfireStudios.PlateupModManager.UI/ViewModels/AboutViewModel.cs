using CommunityToolkit.Mvvm.ComponentModel;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string CopyrightYear { get; } = DateTime.Now.Year.ToString();
}
