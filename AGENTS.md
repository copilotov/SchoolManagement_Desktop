# School Management System - Desktop (WPF)

Windows desktop port of the Flutter/Android private-school management app.
The application is offline-only. Phase 1 (foundation) is complete; later phases
add the data model, use cases and feature screens.

## Solution map

| Project | Target | Purpose |
| --- | --- | --- |
| `SchoolManagement.Domain` | `net8.0` | Pure business rules and entities. No UI, EF Core or I/O. |
| `SchoolManagement.Application` | `net8.0` | `Result<T>`, `Failure`, `UiState<T>`, interfaces, use cases. |
| `SchoolManagement.Infrastructure` | `net8.0` | EF Core + SQLite, JSON settings, localization files, license bootstrap. |
| `SchoolManagement.Desktop` | `net8.0-windows` | WPF shell, views, ViewModels, themes, startup composition root. |
| `SchoolManagement.Tests` | `net8.0-windows` | xUnit, FluentAssertions, NSubstitute. |

References: Domain -> none, Application -> Domain, Infrastructure -> Application,
Desktop -> Domain + Application + Infrastructure, Tests -> all four.

## Commands

```powershell
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln -c Debug
dotnet test SchoolManagement.sln -c Debug
dotnet run --project SchoolManagement.Desktop
```

`Directory.Build.props` enables nullable + implicit usings and treats warnings as
errors in every project except `SchoolManagement.Desktop` (generated WPF code).

## Runtime locations

- Data: `%LOCALAPPDATA%\SchoolManagement`
- Settings: `%LOCALAPPDATA%\SchoolManagement\settings.json`
- Database: `%LOCALAPPDATA%\SchoolManagement\school_management_db.sqlite`
- Logs: `%LOCALAPPDATA%\SchoolManagement\logs\bootstrap.log`
- Translations: `<app output>\Resources\Translations\{ar,en,fr}.json`

The canonical translation files live in
`SchoolManagement.Desktop/Resources/Translations`; Infrastructure and Tests link
them into their own output folders.

## Startup order

1. Show the splash window immediately (logo + indeterminate progress only).
2. Read `settings.json` (language/theme).
3. Load the saved translation file, falling back to Arabic.
4. Build and start the Generic Host / DI container.
5. Open and integrity-check the SQLite database.
6. Run the license bootstrap hook (Phase 1: `Unlicensed` stub).
7. Apply the theme, create `ShellWindow`, close the splash.

If any step throws, `BootstrapGate` shows the icon-only retry screen. That screen
contains no text because translation may be the failed step. `bootstrap.log`
records the exception. Retry re-runs the whole order and disposes a partially
built host first.

## Phase 1 behavior

- Five shell destinations: Dashboard, Students, Groups, Finance, Settings.
- `NavigationService` caches one ViewModel per screen so tab state survives
  navigation. `NavigateToTab` is the desktop equivalent of `HomeTab.goToTab`.
- Light/dark themes use semantic `DynamicResource` brushes. `ThemeService`
  replaces the first merged theme dictionary in place; no restart.
- Arabic is the default language and sets `FlowDirection.RightToLeft`.
  `{loc:Tr key}` bindings update instantly on language change.
- `settings.json` stores only language and theme. It is separate from SQLite so
  preferences are readable before the database opens.
- The status bar reserves a banner host for the licensing phase and shows the
  application version.
- Feature pages are deliberate placeholders until their use cases exist.

## Conventions

- Repositories and use cases return `Result<T>`; expected failures are `Failure`
  values with translation keys, never exceptions thrown at the UI.
- ViewModels expose `UiState<T>` (Initial/Loading/Success/Error/Empty).
- No user-visible hardcoded strings. XAML uses `{loc:Tr key}`; C# uses
  `TranslationKeys` constants. Missing keys return their last segment and are
  recorded in `ILocalizationService.MissingKeys`.
- Every `TranslationKeys` constant and every XAML `{loc:Tr ...}` key must exist
  in all three JSON files; tests enforce this.
- One ViewModel instance per screen. Do not recreate screens for navigation or
  language/theme changes.
- Dates use explicit `DateTimeKind.Utc` constructors. Never use `DateTime.Now`
  or `DateTime.UtcNow` for license validity.
- School week: `0 = Saturday ... 6 = Friday`; convert with
  `((int)date.DayOfWeek + 1) % 7` via `WeekDayCalculator`.
- Write operations must call `ILicenseGuard.EnsureCanWrite()` once the licensing
  phase lands.
- The desktop minimum window size is 1000x650.

## Test coverage (Phase 1)

- `Result<T>` / `Failure` / `UiState<T>` behavior.
- Weekday conversion and the broken `% 7` formula regression.
- Localization loading, switching, missing-key fallback and parameters.
- Shell RTL/LTR and label refresh after a language switch.
- Navigation cache, programmatic tab switching and events.
- Splash-before-initialization ordering, failure and cancellation.
- Translation key parity, used XAML keys, hardcoded XAML text and Arabic text.

## Next phases

Data model + migrations, repositories/use cases, students/groups/finance
screens, attendance/sessions/homework, Ed25519 + DPAPI licensing, QuestPDF
reports, ZXing QR and SMS via `sms:` / Phone Link. Do not add these to Phase 1.
