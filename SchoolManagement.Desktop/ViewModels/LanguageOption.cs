using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolManagement.Desktop.ViewModels;

public partial class LanguageOption : ObservableObject
{
    public LanguageOption(string code, string nameKey, string displayName)
    {
        Code = code;
        NameKey = nameKey;
        _displayName = displayName;
    }

    public string Code { get; }

    public string NameKey { get; }

    [ObservableProperty]
    private string _displayName;

    public void Refresh(string displayName) => DisplayName = displayName;
}
