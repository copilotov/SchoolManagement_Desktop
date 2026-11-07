using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Tests.Academic;

/// <summary>
/// In-memory localization double. Keys without an explicit value are returned
/// verbatim, which keeps the assertions readable ("validators.required").
/// </summary>
internal sealed class FakeLocalization : ILocalizationService
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public string CurrentLanguage => "en";

    public bool IsRightToLeft => false;

    public IReadOnlyList<string> SupportedLanguages { get; } = new[] { "en" };

    public IReadOnlyCollection<string> MissingKeys { get; } = Array.Empty<string>();

    public event EventHandler? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task<bool> InitializeAsync(string? language = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> SetLanguageAsync(string language, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public string Tr(string key, IReadOnlyDictionary<string, string>? parameters = null)
        => Values.TryGetValue(key, out var value) ? value : key;

    public string this[string key] => Tr(key);

    public bool HasKey(string key) => Values.ContainsKey(key);

    public void RaiseLanguageChanged()
    {
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}

/// <summary>
/// Builds the four academic ViewModels on top of substituted repositories, so
/// every test exercises the real use cases and the real ViewModel logic without
/// a WPF message loop.
/// </summary>
internal sealed class AcademicVmHarness
{
    public FakeLocalization Localization { get; } = new();

    public IUserDialogService Dialogs { get; } = Substitute.For<IUserDialogService>();

    public IAcademicLevelRepository LevelRepository { get; } = Substitute.For<IAcademicLevelRepository>();

    public IGradeRepository GradeRepository { get; } = Substitute.For<IGradeRepository>();

    public ISubjectRepository SubjectRepository { get; } = Substitute.For<ISubjectRepository>();

    public ITeacherRepository TeacherRepository { get; } = Substitute.For<ITeacherRepository>();

    public LicenseViewModel License { get; }

    public AcademicVmHarness()
    {
        LevelRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AcademicLevelListItem>>.Success(
                Array.Empty<AcademicLevelListItem>()));

        GradeRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GradeListItem>>.Success(Array.Empty<GradeListItem>()));

        SubjectRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SubjectListItem>>.Success(Array.Empty<SubjectListItem>()));

        TeacherRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TeacherListItem>>.Success(Array.Empty<TeacherListItem>()));

        Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        License = BuildLicense(Localization);
    }

    public static ILicenseGuard Allowing()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Success(true));
        return guard;
    }

    public static ILicenseGuard Blocking()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Fail(
            Failure.License(TranslationKeys.License.ErrorWriteBlocked)));
        return guard;
    }

    public AcademicLevelsViewModel CreateLevels(ILicenseGuard? guard = null)
    {
        var licenseGuard = guard ?? Allowing();

        return new AcademicLevelsViewModel(
            new GetAcademicLevelsUseCase(LevelRepository),
            new AddAcademicLevelUseCase(LevelRepository, licenseGuard),
            new UpdateAcademicLevelUseCase(LevelRepository, licenseGuard),
            new DeleteAcademicLevelUseCase(LevelRepository, licenseGuard),
            Localization,
            License,
            Dialogs);
    }

    public AcademicGradesViewModel CreateGrades(ILicenseGuard? guard = null)
    {
        var licenseGuard = guard ?? Allowing();

        return new AcademicGradesViewModel(
            new GetGradesUseCase(GradeRepository),
            new GetAcademicLevelsUseCase(LevelRepository),
            new GetSubjectsUseCase(SubjectRepository),
            new GetGradeSubjectsUseCase(GradeRepository),
            new AddGradeUseCase(GradeRepository, licenseGuard),
            new UpdateGradeUseCase(GradeRepository, licenseGuard),
            new DeleteGradeUseCase(GradeRepository, licenseGuard),
            new SetGradeSubjectsUseCase(GradeRepository, licenseGuard),
            Localization,
            License,
            Dialogs);
    }

    public AcademicSubjectsViewModel CreateSubjects(ILicenseGuard? guard = null)
    {
        var licenseGuard = guard ?? Allowing();

        return new AcademicSubjectsViewModel(
            new GetSubjectsUseCase(SubjectRepository),
            new AddSubjectUseCase(SubjectRepository, licenseGuard),
            new UpdateSubjectUseCase(SubjectRepository, licenseGuard),
            new DeleteSubjectUseCase(SubjectRepository, licenseGuard),
            Localization,
            License,
            Dialogs);
    }

    public AcademicTeachersViewModel CreateTeachers(ILicenseGuard? guard = null)
    {
        var licenseGuard = guard ?? Allowing();

        return new AcademicTeachersViewModel(
            new GetTeachersUseCase(TeacherRepository),
            new AddTeacherUseCase(TeacherRepository, licenseGuard),
            new UpdateTeacherUseCase(TeacherRepository, licenseGuard),
            new DeleteTeacherUseCase(TeacherRepository, licenseGuard),
            Localization,
            License,
            Dialogs);
    }

    public AcademicsViewModel CreateHost(ILicenseGuard? guard = null) => new(
        CreateLevels(guard),
        CreateGrades(guard),
        CreateSubjects(guard),
        CreateTeachers(guard));

    private static LicenseViewModel BuildLicense(ILocalizationService localization)
    {
        var service = Substitute.For<ILicenseService>();
        service.LoadAndVerifyAsync(Arg.Any<CancellationToken>())
            .Returns(LicenseInfo.Unlicensed("FINGERPRINT"));

        return new LicenseViewModel(
            service,
            Substitute.For<IActivationDialogService>(),
            localization);
    }
}

public sealed class AcademicViewModelTests
{
    private static readonly AcademicLevelListItem Primary =
        new(1, "Primary", 1, true, 2, 1);

    [Fact]
    public async Task Levels_load_the_projection_and_send_the_default_query()
    {
        var harness = new AcademicVmHarness();
        harness.LevelRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AcademicLevelListItem>>.Success(new[] { Primary }));

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        vm.IsLoaded.Should().BeTrue();
        vm.Items.Should().ContainSingle().Which.Name.Should().Be("Primary");
        vm.HasError.Should().BeFalse();

        await harness.LevelRepository.Received(1).GetListAsync(
            Arg.Is<AcademicListQuery>(query =>
                query.SearchTerm == null
                && !query.IncludeInactive
                && query.SortBy == AcademicSortField.Name
                && !query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ensure_loaded_only_queries_the_repository_once()
    {
        var harness = new AcademicVmHarness();
        using var vm = harness.CreateLevels();

        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();

        await harness.LevelRepository.Received(1).GetListAsync(
            Arg.Any<AcademicListQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Levels_send_search_sort_and_include_inactive()
    {
        var harness = new AcademicVmHarness();
        using var vm = harness.CreateLevels();

        vm.SearchText = "  Pri  ";
        vm.IncludeInactive = true;
        vm.SelectedSort = vm.SortOptions.Single(option => option.Field == AcademicSortField.Order);
        vm.SortDescending = true;

        await vm.SearchCommand.ExecuteAsync(null);

        await harness.LevelRepository.Received(1).GetListAsync(
            Arg.Is<AcademicListQuery>(query =>
                query.SearchTerm == "  Pri  "
                && query.IncludeInactive
                && query.SortBy == AcademicSortField.Order
                && query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_read_failure_is_displayed_without_the_renew_button()
    {
        var harness = new AcademicVmHarness();
        harness.LevelRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AcademicLevelListItem>>.Fail(
                Failure.Database(TranslationKeys.Errors.DatabaseFailure)));

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.DatabaseFailure);
        vm.IsRenewVisible.Should().BeFalse();
        vm.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task Adding_a_level_saves_trims_and_closes_the_editor()
    {
        AcademicLevel? stored = null;
        var harness = new AcademicVmHarness();
        harness.LevelRepository
            .AddAsync(Arg.Do<AcademicLevel>(entity => stored = entity), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(10));

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.IsEditorOpen.Should().BeTrue();
        vm.EditorTitle.Should().Be(TranslationKeys.Levels.Add);
        vm.EditorName = "  Level A  ";
        vm.EditorOrder = " 3 ";
        vm.EditorIsActive = false;

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.IsEditorOpen.Should().BeFalse();
        vm.HasError.Should().BeFalse();
        vm.StatusMessage.Should().Be(TranslationKeys.Messages.AddSuccess);

        stored.Should().NotBeNull();
        stored!.Name.Should().Be("Level A");
        stored.OrderIndex.Should().Be(3);
        stored.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task A_non_numeric_order_is_rejected_before_the_write()
    {
        var harness = new AcademicVmHarness();
        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.EditorName = "Level A";
        vm.EditorOrder = "abc";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(TranslationKeys.Validators.InvalidNumber);
        vm.IsEditorOpen.Should().BeTrue();
        await harness.LevelRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task A_required_name_failure_keeps_the_editor_open()
    {
        var harness = new AcademicVmHarness();
        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.EditorName = "   ";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(TranslationKeys.Validators.Required);
        vm.IsEditorOpen.Should().BeTrue();
        await harness.LevelRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Editing_a_level_updates_the_same_row()
    {
        var entity = new AcademicLevel { Id = 4, Name = "Old", OrderIndex = 2, IsActive = true };
        var harness = new AcademicVmHarness();
        harness.LevelRepository
            .GetByIdAsync(4, Arg.Any<CancellationToken>())
            .Returns(Result<AcademicLevel?>.Success(entity));
        harness.LevelRepository
            .UpdateAsync(Arg.Any<AcademicLevel>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        var item = new AcademicLevelListItem(4, "Old", 2, true, 0, 0);
        vm.EditCommand.Execute(item);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditorTitle.Should().Be(TranslationKeys.Levels.Edit);
        vm.EditorName.Should().Be("Old");
        vm.EditorOrder.Should().Be("2");

        vm.EditorName = "New";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        entity.Name.Should().Be("New");
        vm.StatusMessage.Should().Be(TranslationKeys.Messages.UpdateSuccess);
        vm.IsEditorOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_level_asks_for_confirmation_and_reloads()
    {
        var harness = new AcademicVmHarness();
        harness.LevelRepository.CountGradesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        harness.LevelRepository.CountGroupsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        harness.LevelRepository.DeleteAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(Primary);

        await harness.Dialogs.Received(1).ConfirmAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
        await harness.LevelRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Messages.DeleteSuccess);
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task A_declined_confirmation_never_deletes()
    {
        var harness = new AcademicVmHarness();
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(Primary);

        await harness.LevelRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task A_linked_level_delete_shows_the_counted_blocked_message()
    {
        var harness = new AcademicVmHarness();
        harness.LevelRepository.CountGradesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(Primary);

        vm.ErrorMessage.Should().Be(TranslationKeys.Levels.DeleteBlockedGrades);
        vm.IsRenewVisible.Should().BeFalse();
        await harness.LevelRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task A_license_blocked_delete_offers_the_renew_button()
    {
        var harness = new AcademicVmHarness();
        using var vm = harness.CreateLevels(AcademicVmHarness.Blocking());
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(Primary);

        vm.ErrorMessage.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        vm.IsRenewVisible.Should().BeTrue();
        await harness.LevelRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Subjects_surface_a_duplicate_code_without_closing_the_editor()
    {
        var harness = new AcademicVmHarness();
        harness.SubjectRepository
            .CodeExistsAsync("M1", null, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        using var vm = harness.CreateSubjects();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.EditorName = "Mathematics";
        vm.EditorCode = "M1";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(TranslationKeys.Validators.CodeExists);
        vm.IsEditorOpen.Should().BeTrue();
        await harness.SubjectRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Teachers_edit_maps_every_field_into_the_editor()
    {
        var hireDate = new DateTime(2021, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var harness = new AcademicVmHarness();
        using var vm = harness.CreateTeachers();
        await vm.EnsureLoadedAsync();

        var item = new TeacherListItem(
            3,
            "Alice",
            "0555",
            "alice@example.com",
            "Rue 1",
            "Math",
            hireDate,
            true,
            2);

        vm.EditCommand.Execute(item);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditorTitle.Should().Be(TranslationKeys.Teachers.Edit);
        vm.EditorFullName.Should().Be("Alice");
        vm.EditorPhone.Should().Be("0555");
        vm.EditorEmail.Should().Be("alice@example.com");
        vm.EditorAddress.Should().Be("Rue 1");
        vm.EditorSpecialization.Should().Be("Math");
        vm.EditorHireDate.Should().Be(hireDate);
        vm.EditorIsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Grades_load_the_level_options_and_select_the_grade_level()
    {
        var harness = new AcademicVmHarness();
        harness.GradeRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GradeListItem>>.Success(new[]
            {
                new GradeListItem(7, "Grade A", 2, "Middle", 1, true, 0, 0),
            }));
        harness.LevelRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AcademicLevelListItem>>.Success(new[]
            {
                new AcademicLevelListItem(1, "Primary", 1, true, 0, 0),
                new AcademicLevelListItem(2, "Middle", 2, true, 0, 0),
            }));

        using var vm = harness.CreateGrades();
        await vm.EnsureLoadedAsync();

        vm.Items.Should().ContainSingle();
        vm.LevelOptions.Should().HaveCount(3);
        vm.LevelOptions[0].Id.Should().BeNull();
        vm.LevelOptions[0].Name.Should().Be(TranslationKeys.Common.NotSpecified);

        vm.EditCommand.Execute(vm.Items[0]);

        vm.EditorName.Should().Be("Grade A");
        vm.SelectedLevelOption.Should().NotBeNull();
        vm.SelectedLevelOption!.Id.Should().Be(2);
    }

    [Fact]
    public async Task Grades_manage_subjects_marks_links_and_saves_the_selection()
    {
        var harness = new AcademicVmHarness();
        harness.SubjectRepository
            .GetListAsync(Arg.Any<AcademicListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SubjectListItem>>.Success(new[]
            {
                new SubjectListItem(1, "Mathematics", "MATH", null, true, 0, 0),
                new SubjectListItem(2, "Science", "SCI", null, true, 0, 0),
            }));
        harness.GradeRepository
            .GetSubjectIdsAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<int>>.Success(new[] { 2 }));
        harness.GradeRepository
            .SetSubjectsAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        using var vm = harness.CreateGrades();
        await vm.EnsureLoadedAsync();

        var grade = new GradeListItem(5, "Grade A", null, null, 1, true, 1, 0);
        await vm.ManageSubjectsCommand.ExecuteAsync(grade);

        vm.IsSubjectPanelOpen.Should().BeTrue();
        vm.SubjectPanelTitle.Should().Be(TranslationKeys.Grades.ManageSubjects);
        vm.SubjectChoices.Should().HaveCount(2);
        vm.SubjectChoices.Single(choice => choice.Id == 2).IsSelected.Should().BeTrue();
        vm.SubjectChoices.Single(choice => choice.Id == 1).IsSelected.Should().BeFalse();

        vm.SubjectChoices.Single(choice => choice.Id == 1).IsSelected = true;
        vm.SubjectChoices.Single(choice => choice.Id == 2).IsSelected = false;

        await vm.SaveSubjectsCommand.ExecuteAsync(null);

        await harness.GradeRepository.Received(1).SetSubjectsAsync(
            5,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1 })),
            Arg.Any<CancellationToken>());
        vm.IsSubjectPanelOpen.Should().BeFalse();
        vm.StatusMessage.Should().Be(TranslationKeys.Messages.UpdateSuccess);
    }

    [Fact]
    public async Task The_academics_host_lazily_loads_only_the_selected_tab()
    {
        var harness = new AcademicVmHarness();
        using var host = harness.CreateHost();

        await host.SelectTabAsync(2);

        host.SelectedTabIndex.Should().Be(2);
        await harness.SubjectRepository.Received(1).GetListAsync(
            Arg.Any<AcademicListQuery>(),
            Arg.Any<CancellationToken>());
        await harness.TeacherRepository.DidNotReceiveWithAnyArgs().GetListAsync(default!, default);

        await host.EnsureLoadedAsync();

        await harness.SubjectRepository.Received(1).GetListAsync(
            Arg.Any<AcademicListQuery>(),
            Arg.Any<CancellationToken>());

        await host.SelectTabAsync(3);

        await harness.TeacherRepository.Received(1).GetListAsync(
            Arg.Any<AcademicListQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_language_switch_refreshes_sort_labels_and_the_open_editor_title()
    {
        var harness = new AcademicVmHarness();
        harness.Localization.Values[TranslationKeys.Levels.Add] = "Add level";
        harness.Localization.Values[TranslationKeys.Common.SortName] = "Name";

        using var vm = harness.CreateLevels();
        await vm.EnsureLoadedAsync();

        vm.SortOptions.Single(option => option.Field == AcademicSortField.Name)
            .Label.Should().Be("Name");

        vm.AddCommand.Execute(null);
        vm.EditorTitle.Should().Be("Add level");

        harness.Localization.Values[TranslationKeys.Levels.Add] = "Ajouter un niveau";
        harness.Localization.Values[TranslationKeys.Common.SortName] = "Nom";
        harness.Localization.Values[TranslationKeys.Common.SortOrder] = "Ordre";
        harness.Localization.Values[TranslationKeys.Common.SortLinked] = "Liés";

        harness.Localization.RaiseLanguageChanged();

        vm.EditorTitle.Should().Be("Ajouter un niveau");
        vm.SortOptions.Single(option => option.Field == AcademicSortField.Name)
            .Label.Should().Be("Nom");
        vm.SortOptions.Single(option => option.Field == AcademicSortField.LinkedItems)
            .Label.Should().Be("Liés");
    }

    [Fact]
    public void Disposing_a_tab_stops_listening_to_language_changes()
    {
        var harness = new AcademicVmHarness();
        var vm = harness.CreateLevels();
        vm.Dispose();

        var act = () => harness.Localization.RaiseLanguageChanged();

        act.Should().NotThrow();
    }
}

/// <summary>
/// Static checks on the four academic views. A CommunityToolkit command name is
/// generated from the method name, so a hand-written binding can silently fail
/// at runtime; these tests catch a wrong name (they already caught
/// <c>SaveCommand</c> and <c>CloseEditorCommand</c>) and a lost shortcut.
/// </summary>
public sealed class AcademicViewBindingTests
{
    private static readonly Regex CommandBinding = new(
        @"\{Binding\s+(?:DataContext\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*Command)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TheoryData<string, Type> Views => new()
    {
        { "AcademicLevelsTabView.xaml", typeof(AcademicLevelsViewModel) },
        { "AcademicGradesTabView.xaml", typeof(AcademicGradesViewModel) },
        { "AcademicSubjectsTabView.xaml", typeof(AcademicSubjectsViewModel) },
        { "AcademicTeachersTabView.xaml", typeof(AcademicTeachersViewModel) },
    };

    [Theory]
    [MemberData(nameof(Views))]
    public void Every_command_binding_exists_on_its_view_model(string fileName, Type viewModelType)
    {
        var xaml = ReadView(fileName);

        var names = CommandBinding.Matches(xaml)
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToArray();

        names.Should().NotBeEmpty();
        foreach (var name in names)
        {
            viewModelType.GetProperty(name).Should().NotBeNull(
                $"{fileName} binds {name}, which the ViewModel must expose");
        }
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void Every_academic_view_offers_the_keyboard_shortcuts(string fileName, Type viewModelType)
    {
        _ = viewModelType;
        var xaml = ReadView(fileName);

        xaml.Should().Contain("Key=\"F2\"");
        xaml.Should().Contain("Key=\"Delete\"");
        xaml.Should().Contain("Key=\"Escape\"");
        xaml.Should().Contain("Key=\"Enter\"");
    }

    private static string ReadView(string fileName)
        => File.ReadAllText(Path.Combine(ViewsDirectory(), fileName));

    private static string ViewsDirectory([CallerFilePath] string callerPath = "")
    {
        var testsDirectory = Path.GetDirectoryName(callerPath)!;
        var root = Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
        return Path.Combine(root, "SchoolManagement.Desktop", "Views");
    }
}
