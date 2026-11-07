using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Controls;

namespace SchoolManagement.Tests.DesignSystem;

/// <summary>
/// Phase D3 - the reusable components.
///
/// The visual controls need a WPF message loop, but their decision logic does
/// not: filtering, sorting, conflict detection, the month contract and the
/// semantic tone map are plain functions so they are tested here without a
/// window. A reflection test then guards the public dependency-property surface
/// a screen binds to, because a renamed property is a silent binding failure in
/// WPF. The gallery itself is exercised by <see cref="DesignGalleryWindowTests"/>.
/// </summary>
public sealed class ComponentTests
{
    // -----------------------------------------------------------------
    // Semantic tone map
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("present", StatusTone.Success)]
    [InlineData("paid", StatusTone.Success)]
    [InlineData("paidFull", StatusTone.Success)]
    [InlineData("completed", StatusTone.Success)]
    [InlineData("submitted", StatusTone.Success)]
    [InlineData("active", StatusTone.Success)]
    [InlineData("late", StatusTone.Warning)]
    [InlineData("partial", StatusTone.Warning)]
    [InlineData("pending", StatusTone.Warning)]
    [InlineData("scheduled", StatusTone.Warning)]
    [InlineData("postponed", StatusTone.Warning)]
    [InlineData("expiringSoon", StatusTone.Warning)]
    [InlineData("inProgress", StatusTone.Warning)]
    [InlineData("absent", StatusTone.Danger)]
    [InlineData("unexcused", StatusTone.Danger)]
    [InlineData("unpaid", StatusTone.Danger)]
    [InlineData("overdue", StatusTone.Danger)]
    [InlineData("cancelled", StatusTone.Danger)]
    [InlineData("readOnly", StatusTone.Danger)]
    [InlineData("missing", StatusTone.Danger)]
    [InlineData("exempted", StatusTone.Info)]
    [InlineData("notStarted", StatusTone.Info)]
    [InlineData("makeup", StatusTone.Info)]
    [InlineData("extra", StatusTone.Info)]
    [InlineData("accent", StatusTone.Accent)]
    [InlineData("unknown-status", StatusTone.Neutral)]
    [InlineData("", StatusTone.Neutral)]
    [InlineData(null, StatusTone.Neutral)]
    public void Status_tone_map_covers_every_entity_vocabulary(string? status, StatusTone expected)
        => StatusBadgeMapper.MapTone(status).Should().Be(expected);

    [Theory]
    [InlineData("  IN_PROGRESS  ")]
    [InlineData("in-progress")]
    [InlineData("In Progress")]
    public void Status_tone_map_ignores_case_and_separators(string status)
        => StatusBadgeMapper.MapTone(status).Should().Be(StatusTone.Warning);

    [Fact]
    public void Every_tone_names_palette_tokens_that_exist_in_both_themes()
    {
        foreach (var tone in Enum.GetValues<StatusTone>())
        {
            var palette = ToneResources.For(tone);

            foreach (var key in new[] { palette.Background, palette.Border, palette.Foreground, palette.Solid })
            {
                key.Should().NotBeNullOrWhiteSpace($"{tone} must name a resource key, never a colour");

                DesignFiles.ReadPalette(DesignFiles.Light).Should().ContainKey(key);
                DesignFiles.ReadPalette(DesignFiles.Dark).Should().ContainKey(key);
            }
        }
    }

    [Fact]
    public void Neutral_tone_falls_back_to_the_surface_tokens()
    {
        var palette = ToneResources.For(StatusTone.Neutral);

        palette.Background.Should().Be("Surface.Alt");
        palette.Border.Should().Be("Border.Subtle");
        palette.Foreground.Should().Be("Text.Secondary");
    }

    // -----------------------------------------------------------------
    // DataTable filtering, value access and sorting
    // -----------------------------------------------------------------

    [Fact]
    public void Get_value_reads_a_nested_path_case_insensitively()
        => OnSta(() =>
        {
            var row = new SampleRow("Amina", "present", 12, new SampleGroup("G1"));

            DataTableFilter.GetValue(row, "Name").Should().Be("Amina");
            DataTableFilter.GetValue(row, "name").Should().Be("Amina");
            DataTableFilter.GetValue(row, "Group.Name").Should().Be("G1");
            DataTableFilter.GetValue(row, "Missing").Should().BeNull();
            DataTableFilter.GetValue(row, "Group.Missing").Should().BeNull();
            DataTableFilter.GetValue(row, string.Empty).Should().BeNull();
        });

    [Fact]
    public void Format_uses_the_shared_date_and_culture_contract()
    {
        DataTableFilter.Format(null).Should().BeEmpty();
        DataTableFilter.Format(new DateTime(2026, 11, 7, 15, 30, 0, DateTimeKind.Utc))
            .Should().Be("2026-11-07");
        DataTableFilter.Format(42).Should().Be("42");
        DataTableFilter.Format(12500.5).Should().Contain("12500");
    }

    [Fact]
    public void Compare_orders_nulls_first_and_numbers_numerically()
    {
        DataTableFilter.Compare(null, 5).Should().BeNegative();
        DataTableFilter.Compare(5, null).Should().BePositive();
        DataTableFilter.Compare(null, null).Should().Be(0);
        DataTableFilter.Compare(9, 10).Should().BeNegative();
        DataTableFilter.Compare("Amina", "amina").Should().Be(0);
    }

    [Fact]
    public void Global_search_matches_only_the_visible_columns()
        => OnSta(() =>
        {
            var row = new SampleRow("Amina", "present", 12, new SampleGroup("G1"));

            var visible = new List<DataTableColumn>
            {
                new() { BindingPath = "Name" },
                new() { BindingPath = "Status" },
            };

            DataTableFilter.Matches(row, visible, "ami").Should().BeTrue();
            DataTableFilter.Matches(row, visible, "present").Should().BeTrue();
            DataTableFilter.Matches(row, visible, "G1").Should().BeFalse();

            var hidden = new List<DataTableColumn>
            {
                new() { BindingPath = "Name" },
                new() { BindingPath = "Group.Name", IsVisible = false },
            };

            DataTableFilter.Matches(row, hidden, "G1").Should().BeFalse(
                "a hidden column must not leak into the search");
        });

    [Fact]
    public void Column_filters_combine_and_ignore_blank_values()
        => OnSta(() =>
        {
            var row = new SampleRow("Amina", "present", 12, new SampleGroup("G1"));
            var columns = new List<DataTableColumn>
            {
                new() { BindingPath = "Name", FilterText = "ami" },
                new() { BindingPath = "Status", FilterText = "pres" },
            };

            DataTableFilter.Matches(row, columns, null).Should().BeTrue();

            columns[1].FilterText = "absent";
            DataTableFilter.Matches(row, columns, null).Should().BeFalse();

            columns[1].FilterText = "   ";
            DataTableFilter.Matches(row, columns, null).Should().BeTrue(
                "a whitespace filter is not a filter");
        });

    [Fact]
    public void An_empty_search_matches_every_row_but_a_null_row_never_matches()
        => OnSta(() =>
        {
            var columns = new List<DataTableColumn> { new() { BindingPath = "Name" } };

            DataTableFilter.Matches(new SampleRow("Amina", "present", 12, null), columns, null)
                .Should().BeTrue();
            DataTableFilter.Matches(new SampleRow("Amina", "present", 12, null), columns, "   ")
                .Should().BeTrue();
            DataTableFilter.Matches(null, columns, null).Should().BeFalse();
        });

    [Fact]
    public void A_column_change_reports_which_property_changed()
        => OnSta(() =>
        {
            var column = new DataTableColumn();
            var changes = new List<string>();
            column.Changed += (_, args) => changes.Add(args.PropertyName);

            column.Header = "Name";
            column.FilterText = "ami";
            column.IsVisible = false;

            changes.Should().Equal(
                nameof(DataTableColumn.Header),
                nameof(DataTableColumn.FilterText),
                nameof(DataTableColumn.IsVisible));
        });

    // -----------------------------------------------------------------
    // Weekly schedule conflicts
    // -----------------------------------------------------------------

    [Fact]
    public void Overlapping_slots_of_the_same_resource_are_both_marked()
    {
        var first = Slot(0, 8, 10, "teacher-1");
        var second = Slot(0, 9, 11, "teacher-1");

        WeekScheduleConflictPolicy.MarkConflicts([first, second]);

        first.IsConflict.Should().BeTrue();
        second.IsConflict.Should().BeTrue();
    }

    [Fact]
    public void Touching_ranges_do_not_conflict()
    {
        var first = Slot(0, 8, 10, "teacher-1");
        var second = Slot(0, 10, 12, "teacher-1");

        WeekScheduleConflictPolicy.MarkConflicts([first, second]);

        first.IsConflict.Should().BeFalse("the range is half-open");
        second.IsConflict.Should().BeFalse();
        WeekScheduleConflictPolicy.Overlaps(first, second).Should().BeFalse();
    }

    [Fact]
    public void Different_days_or_different_resources_never_conflict()
    {
        var monday = Slot(0, 8, 10, "teacher-1");
        var tuesday = Slot(1, 8, 10, "teacher-1");
        var otherTeacher = Slot(0, 8, 10, "teacher-2");

        WeekScheduleConflictPolicy.MarkConflicts([monday, tuesday, otherTeacher]);

        monday.IsConflict.Should().BeFalse();
        tuesday.IsConflict.Should().BeFalse();
        otherTeacher.IsConflict.Should().BeFalse();
    }

    [Fact]
    public void Two_blank_resource_keys_are_treated_as_the_same_resource()
    {
        var first = Slot(0, 8, 10, null);
        var second = Slot(0, 9, 11, null);

        WeekScheduleConflictPolicy.MarkConflicts([first, second]);

        first.IsConflict.Should().BeTrue();
        second.IsConflict.Should().BeTrue();
    }

    [Fact]
    public void An_invalid_range_is_ignored_and_stale_flags_are_cleared()
    {
        var invalid = Slot(0, 10, 10, "teacher-1");
        invalid.IsConflict = true;

        var valid = Slot(2, 8, 9, "teacher-1");
        valid.IsConflict = true;

        WeekScheduleConflictPolicy.MarkConflicts([invalid, valid]);

        invalid.IsConflict.Should().BeTrue(
            "an end equal to the start is not a real slot, so the policy leaves it untouched");
        valid.IsConflict.Should().BeFalse("a fresh pass clears a flag that no longer applies");
    }

    [Fact]
    public void Duration_is_never_negative()
    {
        Slot(0, 8, 10, "teacher-1").Duration.Should().Be(TimeSpan.FromHours(2));
        Slot(0, 10, 10, "teacher-1").Duration.Should().Be(TimeSpan.Zero);
        Slot(0, 11, 10, "teacher-1").Duration.Should().Be(TimeSpan.Zero);
    }

    // -----------------------------------------------------------------
    // Month picker contract
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("2026-11", 2026, 11)]
    [InlineData("2026/11", 2026, 11)]
    [InlineData("2026-1", 2026, 1)]
    [InlineData("2026/1", 2026, 1)]
    [InlineData("  2026-11  ", 2026, 11)]
    public void Month_year_parser_accepts_the_supported_formats(string text, int year, int month)
    {
        MonthYearParser.TryParse(text, out var parsed).Should().BeTrue();
        parsed.Year.Should().Be(year);
        parsed.Month.Should().Be(month);
        parsed.Day.Should().Be(1);
        parsed.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("2026")]
    [InlineData("11-2026")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Month_year_parser_refuses_everything_else(string? text)
        => MonthYearParser.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void Month_year_round_trips()
    {
        var month = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        MonthYearParser.Format(month).Should().Be("2026-11");
        MonthYearParser.TryParse(MonthYearParser.Format(month), out var parsed).Should().BeTrue();
        parsed.Should().Be(month);
    }

    // -----------------------------------------------------------------
    // Value converters
    // -----------------------------------------------------------------

    [Fact]
    public void Visibility_converters_follow_the_blank_and_null_contract()
    {
        var inverseBool = new InverseBoolToVisibilityConverter();
        var stringToVisibility = new StringToVisibilityConverter();
        var inverseString = new InverseStringToVisibilityConverter();
        var objectToVisibility = new ObjectToVisibilityConverter();
        var nullToVisibility = new NullToVisibilityConverter();
        var inverseBoolValue = new InverseBoolConverter();

        inverseBool.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        inverseBool.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);

        stringToVisibility.Convert(" ", typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        stringToVisibility.Convert("text", typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);

        inverseString.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);
        inverseString.Convert("text", typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);

        objectToVisibility.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        objectToVisibility.Convert(new object(), typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);

        nullToVisibility.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);
        nullToVisibility.Convert(new object(), typeof(Visibility), null!, CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);

        inverseBoolValue.Convert(true, typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(false);
        inverseBoolValue.Convert(false, typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(true);
    }

    [Fact]
    public void Flow_direction_alignment_mirrors_numeric_cells()
    {
        var converter = new FlowDirectionToAlignmentConverter();

        converter.Convert(FlowDirection.LeftToRight, typeof(TextAlignment), null!, CultureInfo.InvariantCulture)
            .Should().Be(TextAlignment.Left);
        converter.Convert(FlowDirection.RightToLeft, typeof(TextAlignment), null!, CultureInfo.InvariantCulture)
            .Should().Be(TextAlignment.Right);

        converter.Convert(FlowDirection.LeftToRight, typeof(TextAlignment), "Trailing", CultureInfo.InvariantCulture)
            .Should().Be(TextAlignment.Right);
        converter.Convert(FlowDirection.RightToLeft, typeof(TextAlignment), "Trailing", CultureInfo.InvariantCulture)
            .Should().Be(TextAlignment.Left);
    }

    [Fact]
    public void Count_format_converter_uses_the_localized_pattern()
    {
        var converter = new CountFormatConverter();

        converter.Convert([3, "{0} selected"], typeof(string), null!, CultureInfo.InvariantCulture)
            .Should().Be("3 selected");
        converter.Convert([3], typeof(string), null!, CultureInfo.InvariantCulture)
            .Should().Be("3");
        converter.Convert([3, "  "], typeof(string), null!, CultureInfo.InvariantCulture)
            .Should().Be("3");
    }

    // -----------------------------------------------------------------
    // Public dependency-property surface
    // -----------------------------------------------------------------

    [Fact]
    public void Every_component_exposes_the_properties_a_screen_binds_to()
    {
        // A renamed dependency property is a silent binding failure in WPF, and
        // a renamed content property silently swallows the child a screen sets.
        // This is the cheap guard that keeps the D3 contract stable.
        var expected = new Dictionary<Type, string[]>
        {
            [typeof(PageHeader)] = ["Breadcrumb", "Title", "Description", "ActionsContent"],
            [typeof(StatCard)] = ["Icon", "Title", "Value", "Trend", "TrendText", "SparklineValues"],
            [typeof(Sparkline)] = ["Values", "Stroke", "Fill", "StrokeThickness", "ShowArea"],
            [typeof(DataTable)] =
            [
                "ItemsSource", "SearchText", "ShowSearch", "Density", "ShowColumnChooser",
                "ShowDetailPanel", "DetailTemplate", "ToolbarContent", "BulkActionsContent",
                "IsBusy", "IsEmpty", "PageSize", "CurrentPage", "PageCount", "ShowPager",
                "SelectionCount", "HasSelection", "SelectedItem", "IsDetailOpen",
                "AddCommand", "EditCommand", "DeleteCommand", "RefreshCommand", "ExportCommand",
                "EmptyTitle", "EmptyDescription", "EmptyActionText", "EmptyActionCommand", "EmptyGlyph",
            ],
            [typeof(EmptyState)] = ["Title", "Description", "ActionText", "ActionCommand", "Glyph"],
            [typeof(ErrorState)] = ["Title", "Message", "Details", "RetryText", "RetryCommand"],
            [typeof(SkeletonLoader)] = ["Rows", "RowHeight", "RowSpacing", "IsActive"],
            [typeof(StatusBadge)] = ["Text", "Tone", "Glyph", "ShowDot"],
            [typeof(Chip)] = ["Text", "Tone", "Glyph", "ShowClose", "CloseCommand", "CommandParameter"],
            [typeof(FormField)] = ["Label", "Description", "ErrorText", "IsRequired", "FieldContent"],
            [typeof(FormSection)] = ["Title", "Description", "SectionContent"],
            [typeof(Wizard)] = ["Steps", "CurrentIndex", "ShowNavigation", "FinishCommand"],
            [typeof(SidePanel)] =
                ["IsOpen", "Title", "PanelWidth", "ShowScrim", "PanelContent", "ActionsContent", "CloseCommand"],
            [typeof(Dialog)] =
            [
                "IsOpen", "Title", "Description", "Tone", "DialogContent", "PrimaryText",
                "PrimaryCommand", "SecondaryText", "SecondaryCommand", "CloseCommand",
            ],
            [typeof(CapacityBar)] = ["Current", "Maximum", "Caption", "ShowValue", "CapacityText"],
            [typeof(ProgressStat)] = ["Title", "Value", "Maximum", "DisplayText", "Tone", "ShowPercent"],
            [typeof(Avatar)] = ["DisplayName", "Initials", "ImageSource", "Size", "Tone", "AutoTone"],
            [typeof(RatingDots)] = ["Value", "Maximum", "Tone", "IsReadOnly"],
            [typeof(DateRangePicker)] = ["StartDate", "EndDate", "StartLabel", "EndLabel"],
            [typeof(MonthPicker)] = ["SelectedMonth", "MonthNames", "StartYear", "EndYear", "ShowLabels"],
            [typeof(SearchBox)] = ["Text", "Placeholder", "SearchCommand"],
            [typeof(TagInput)] = ["Tags", "NewTagText", "Placeholder", "MaxTags"],
            [typeof(QrCardPreview)] = ["Title", "Subtitle", "Code", "Footer", "QrImage"],
            [typeof(ChartCard)] =
            [
                "Title", "Description", "Summary", "Kind", "Tone", "Labels", "Values",
                "ShowEmptyState", "EmptyText", "ActionsContent",
            ],
            [typeof(WeekScheduleGrid)] =
            [
                "Items", "DayLabels", "StartHour", "EndHour", "HourHeight", "IsEditable",
                "IsCompact", "Title", "Description", "EmptyText", "ActionsContent",
                "SelectedItem", "HasConflicts", "IsEmpty",
            ],
        };

        var missing = new List<string>();
        foreach (var (type, properties) in expected)
        {
            var declared = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(field => field.FieldType == typeof(DependencyProperty))
                .Select(field => field.Name[..^"Property".Length])
                .ToHashSet(StringComparer.Ordinal);

            missing.AddRange(properties
                .Where(property => !declared.Contains(property))
                .Select(property => $"{type.Name}.{property}"));
        }

        missing.Should().BeEmpty("every bound property must stay a dependency property");
    }

    // -----------------------------------------------------------------
    // Translation surface added by D3
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("fr")]
    public void Every_component_and_month_constant_exists_in_every_language(string language)
    {
        var keys = LoadKeys(language);

        keys.Should().Contain(ComponentConstants(), $"every components.* key must exist in {language}.json");
        keys.Should().Contain(MonthConstants(), $"every months.* key must exist in {language}.json");
    }

    [Fact]
    public void Component_and_month_constants_use_their_prefix()
    {
        ComponentConstants().Should().OnlyContain(key => key.StartsWith("components.", StringComparison.Ordinal));
        MonthConstants().Should().OnlyContain(key => key.StartsWith("months.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_month_scale_is_complete()
        => MonthConstants().Should().HaveCount(12, "a YYYY-MM picker needs twelve names");

    [Fact]
    public void No_orphan_component_or_month_keys_remain()
    {
        var known = new HashSet<string>(
            ComponentConstants().Concat(MonthConstants()),
            StringComparer.Ordinal);

        foreach (var language in new[] { "ar", "en", "fr" })
        {
            var orphans = LoadKeys(language)
                .Where(key => key.StartsWith("components.", StringComparison.Ordinal)
                    || key.StartsWith("months.", StringComparison.Ordinal))
                .Where(key => !known.Contains(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            orphans.Should().BeEmpty($"{language}.json must not carry a component or month key no constant uses");
        }
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static WeekScheduleItem Slot(int day, double start, double end, string? resourceKey)
        => new(
            day,
            TimeSpan.FromHours(start),
            TimeSpan.FromHours(end),
            "Math",
            "08:00 - 10:00",
            StatusTone.Accent,
            resourceKey);

    private static IReadOnlyList<string> ComponentConstants()
        => typeof(TranslationKeys.Components)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> MonthConstants()
        => typeof(TranslationKeys.Months)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static HashSet<string> LoadKeys(string language)
    {
        var path = Path.Combine(DesignFiles.TranslationsDirectory, $"{language}.json");
        var root = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {path}.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        Flatten(root, string.Empty, keys);
        return keys;
    }

    private static void Flatten(JsonNode? node, string prefix, ISet<string> keys)
    {
        if (node is not JsonObject jsonObject)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                keys.Add(prefix);
            }

            return;
        }

        foreach (var (key, value) in jsonObject)
        {
            Flatten(value, prefix.Length == 0 ? key : $"{prefix}.{key}", keys);
        }
    }

    /// <summary>
    /// Runs a body that builds a WPF <see cref="DependencyObject"/> on an STA
    /// thread and rethrows whatever assertion failed, so FluentAssertions still
    /// reports the real message instead of a generic thread error.
    /// </summary>
    private static void OnSta(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(20)).Should().BeTrue("the STA test thread must finish");

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed record SampleGroup(string Name);

    private sealed record SampleRow(string Name, string Status, int Amount, SampleGroup? Group);
}
