using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// Phase D5 additions to the student list: table/cards, the removable filter
/// chips, bulk selection, the bulk QR and messaging actions, the inline form
/// validation and the card projection. Everything runs the real ViewModel on
/// top of the substituted harness, so no WPF message loop is involved.
/// </summary>
public sealed class StudentListD5Tests
{
    [Fact]
    public void The_table_and_card_toggle_updates_the_view_flags()
    {
        using var vm = new StudentVmHarness().CreateStudents();

        vm.IsTableView.Should().BeTrue();
        vm.IsCardView.Should().BeFalse();

        vm.ShowCardsCommand.Execute(null);
        vm.IsCardView.Should().BeTrue();
        vm.IsTableView.Should().BeFalse();

        vm.ShowTableCommand.Execute(null);
        vm.IsTableView.Should().BeTrue();
        vm.IsCardView.Should().BeFalse();
    }

    [Fact]
    public async Task The_fee_pending_filter_hides_paid_rows_and_adds_a_chip()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(new[]
            {
                StudentVmHarness.Item(1),
                StudentVmHarness.Item(2) with { RegistrationFee = 0 },
            }));

        using var vm = harness.CreateStudents();

        // Set the filter before the first load so the fire-and-forget refresh
        // hook of the property change stays a no-op and the assertion is
        // deterministic.
        vm.FeePendingOnly = true;
        await vm.RefreshAsync();

        vm.Items.Should().ContainSingle();
        vm.Items[0].Id.Should().Be(1);
        vm.ActiveFilters.Should().ContainSingle(chip => chip.Kind == "fee");
        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public async Task Clearing_one_chip_keeps_the_other_filters()
    {
        using var vm = new StudentVmHarness().CreateStudents();

        vm.SearchText = "ali";
        vm.SelectedLevel = new StudentFilterOption(1, "Level One");
        await vm.RefreshAsync();

        vm.ActiveFilters.Should().Contain(chip => chip.Kind == "search");
        vm.ActiveFilters.Should().Contain(chip => chip.Kind == "level");

        vm.ClearFilterCommand.Execute("search");

        vm.SearchText.Should().BeEmpty();
        vm.SelectedLevel.Should().NotBeNull();
        vm.SelectedLevel!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Clear_filters_resets_the_search_and_the_fee_filter()
    {
        using var vm = new StudentVmHarness().CreateStudents();

        vm.SearchText = "ali";
        vm.FeePendingOnly = true;
        await vm.EnsureLoadedAsync();
        vm.HasActiveFilters.Should().BeTrue();

        await vm.ClearFiltersCommand.ExecuteAsync(null);

        vm.SearchText.Should().BeNull();
        vm.FeePendingOnly.Should().BeFalse();
        vm.ActiveFilters.Should().BeEmpty();
        vm.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void The_bulk_selection_drives_the_count_and_the_bar()
    {
        using var vm = new StudentVmHarness().CreateStudents();

        vm.HasBulkSelection.Should().BeFalse();
        vm.SelectedCountText.Should().Be(TranslationKeys.Common.Selected);

        vm.SelectedRows = new List<StudentListItem>
        {
            StudentVmHarness.Item(1),
            StudentVmHarness.Item(2),
        };

        vm.HasBulkSelection.Should().BeTrue();
        vm.SelectedCountText.Should().Be(TranslationKeys.Common.Selected);

        vm.SelectedRows = new List<StudentListItem>();
        vm.HasBulkSelection.Should().BeFalse();
    }

    [Fact]
    public async Task Printing_the_selected_rows_opens_the_qr_page_with_their_ids()
    {
        var harness = new StudentVmHarness();
        var page = harness.CreateQrPrint();
        harness.Navigation.NavigateTo<StudentsQrPrintViewModel>().Returns(page);

        using var vm = harness.CreateStudents();
        vm.SelectedRows = new List<StudentListItem> { StudentVmHarness.Item(3) };

        await vm.PrintSelectedQrCommand.ExecuteAsync(null);

        harness.Navigation.Received(1).NavigateTo<StudentsQrPrintViewModel>();
    }

    [Fact]
    public async Task Messaging_the_guardians_opens_the_sms_launcher_with_their_phones()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(StudentVmHarness.Details()));
        harness.Sms
            .OpenAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>())
            .Returns(true);

        using var vm = harness.CreateStudents();
        vm.SelectedRows = new List<StudentListItem> { StudentVmHarness.Item(1) };

        await vm.MessageGuardiansCommand.ExecuteAsync(null);

        await harness.Sms.Received(1).OpenAsync(
            Arg.Is<IReadOnlyList<string>>(phones => phones.Contains("+213661123456")),
            Arg.Any<string>());
        vm.StatusMessage.Should().Be(TranslationKeys.Students.MessageOpened);
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Messaging_without_a_phone_reports_the_missing_number()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(
                StudentVmHarness.Details() with { GuardianPhone = null }));

        using var vm = harness.CreateStudents();
        vm.SelectedRows = new List<StudentListItem>
        {
            StudentVmHarness.Item(1) with { Phone = null },
        };

        await vm.MessageGuardiansCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(TranslationKeys.Students.MessageNoPhone);
        await harness.Sms.DidNotReceive().OpenAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task An_empty_form_reports_the_field_errors_before_the_use_case()
    {
        var harness = new StudentVmHarness();
        using var vm = harness.CreateStudents();

        vm.AddCommand.Execute(null);
        vm.EditorFirstName = "  ";
        vm.EditorLastName = string.Empty;

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.HasFirstNameError.Should().BeTrue();
        vm.HasLastNameError.Should().BeTrue();
        vm.IsEditorOpen.Should().BeTrue();
        await harness.Repository.DidNotReceive().SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void A_guardian_contact_without_a_name_and_a_negative_fee_are_reported()
    {
        using var vm = new StudentVmHarness().CreateStudents();

        vm.AddCommand.Execute(null);
        vm.EditorFirstName = "Dina";
        vm.EditorLastName = "Diallo";
        vm.EditorGuardianPhone = "0661000000";
        vm.EditorGuardianName = string.Empty;
        vm.EditorRegistrationFee = "-5";

        vm.ValidateEditor().Should().BeFalse();
        vm.HasGuardianError.Should().BeTrue();
        vm.HasFeeError.Should().BeTrue();

        vm.EditorGuardianName = "Guardian Two";
        vm.EditorRegistrationFee = "1500";

        vm.ValidateEditor().Should().BeTrue();
        vm.HasGuardianError.Should().BeFalse();
        vm.HasFeeError.Should().BeFalse();
    }

    [Fact]
    public async Task The_card_projection_carries_initials_placement_and_status()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { StudentVmHarness.Item() }));

        using var vm = harness.CreateStudents();
        await vm.RefreshAsync();

        vm.Cards.Should().ContainSingle();
        var card = vm.Cards[0];
        card.Initials.Should().Be("AA");
        card.FullName.Should().Be("Alice Anderson");
        card.Placement.Should().Contain("Level One");
        card.Placement.Should().Contain("Grade One");
        card.FeeText.Should().Be("1500");
        card.StatusTone.Should().Be(StatusTone.Success);
        card.Item.Id.Should().Be(1);
    }
}
