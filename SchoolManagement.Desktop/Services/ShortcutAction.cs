namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Phase D2 - every global keyboard action the shell understands. The window
/// only translates a key gesture into one of these values and calls
/// <c>ShellViewModel.ExecuteShortcut</c>, so the behaviour is unit-testable
/// without a message loop.
/// </summary>
public enum ShortcutAction
{
    None = 0,
    New = 1,
    Search = 2,
    CommandPalette = 3,
    Save = 4,
    Print = 5,
    Close = 6,
    Navigate = 7,
    Help = 8,
    Back = 9,
    ToggleNav = 10,
}
