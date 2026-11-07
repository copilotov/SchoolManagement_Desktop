using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Students;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Rendering;

namespace SchoolManagement.Infrastructure.Files;

/// <summary>
/// ZXing (QR) plus QuestPDF (card preview and PDFs). The UI never references a
/// rendering library: it receives PNG/PDF bytes and shows them. Every method
/// returns a <see cref="Result{T}"/> so a broken font or encoder becomes a
/// translatable failure instead of an application crash.
/// </summary>
public sealed class StudentCardService : IStudentCardService
{
    private static readonly object InitGate = new();
    private static bool _questPdfConfigured;

    private readonly ILocalizationService _localization;
    private readonly ILogger<StudentCardService> _logger;

    public StudentCardService(ILocalizationService localization, ILogger<StudentCardService> logger)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Result<byte[]> RenderQrPng(string payload, int pixelSize = 512)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Result<byte[]>.Fail(Failure.Validation(TranslationKeys.Students.QrEmpty));
        }

        try
        {
            var size = Math.Clamp(pixelSize, 64, 2048);
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = size,
                    Width = size,
                    Margin = 1,
                    CharacterSet = "UTF-8",
                },
            };

            var pixels = writer.Write(payload);
            using var bitmap = new SKBitmap(
                new SKImageInfo(pixels.Width, pixels.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            System.Runtime.InteropServices.Marshal.Copy(
                pixels.Pixels,
                0,
                bitmap.GetPixels(),
                pixels.Pixels.Length);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return Result<byte[]>.Success(data.ToArray());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to render a QR code");
            return Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender));
        }
    }

    public Result<byte[]> RenderCardPreviewPng(StudentCardData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            var document = BuildCardDocument(data);
            var images = document.GenerateImages(new ImageGenerationSettings
            {
                ImageFormat = ImageFormat.Png,
                RasterDpi = 200,
            });

            var preview = images.FirstOrDefault();
            return preview is null or { Length: 0 }
                ? Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender))
                : Result<byte[]>.Success(preview);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to render the student card preview");
            return Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender));
        }
    }

    public Result<byte[]> RenderCardPdf(StudentCardData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            return Result<byte[]>.Success(BuildCardDocument(data).GeneratePdf());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to render the student card PDF");
            return Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender));
        }
    }

    public Result<byte[]> RenderCardsPdf(IReadOnlyList<StudentCardData> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (cards.Count == 0)
        {
            return Result<byte[]>.Fail(Failure.Validation(TranslationKeys.Students.QrEmpty));
        }

        try
        {
            var document = Document.Create(container =>
            {
                const int perPage = 12;
                for (var start = 0; start < cards.Count; start += perPage)
                {
                    var batch = cards.Skip(start).Take(perPage).ToArray();
                    container.Page(page =>
                    {
                        ConfigurePage(page, PageSizes.A4, 12);
                        ApplyDirection(page.Content()).Column(column =>
                        {
                            for (var index = 0; index < batch.Length; index += 3)
                            {
                                var rowCards = batch.Skip(index).Take(3).ToArray();
                                column.Item().Row(row =>
                                {
                                    foreach (var card in rowCards)
                                    {
                                        row.RelativeItem().Padding(4).Element(target => ComposeCard(target, card, compact: true));
                                    }

                                    for (var filler = rowCards.Length; filler < 3; filler++)
                                    {
                                        row.RelativeItem();
                                    }
                                });
                            }
                        });
                    });
                }
            });

            return Result<byte[]>.Success(document.GeneratePdf());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to render the student cards PDF");
            return Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender));
        }
    }

    public Result<byte[]> RenderQrSheetPdf(IReadOnlyList<StudentQrItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return Result<byte[]>.Fail(Failure.Validation(TranslationKeys.Students.QrEmpty));
        }

        try
        {
            var document = Document.Create(container =>
            {
                const int columns = 4;
                const int rows = 5;
                const int perPage = columns * rows;

                for (var start = 0; start < items.Count; start += perPage)
                {
                    var batch = items.Skip(start).Take(perPage).ToArray();
                    container.Page(page =>
                    {
                        ConfigurePage(page, PageSizes.A4, 14);
                        ApplyDirection(page.Content()).Column(column =>
                        {
                            for (var index = 0; index < batch.Length; index += columns)
                            {
                                var rowItems = batch.Skip(index).Take(columns).ToArray();
                                column.Item().Row(row =>
                                {
                                    foreach (var item in rowItems)
                                    {
                                        row.RelativeItem().Padding(6).Border(1)
                                            .BorderColor(Colors.Grey.Lighten1)
                                            .Padding(4)
                                            .Column(cell =>
                                            {
                                                cell.Item().Height(30, Unit.Millimetre).Image(item.Png).FitArea();
                                                cell.Item().AlignCenter().Text(item.FullName).Bold().FontSize(8);
                                                cell.Item().AlignCenter().Text(item.StudentCode).FontSize(7);
                                            });
                                    }

                                    for (var filler = rowItems.Length; filler < columns; filler++)
                                    {
                                        row.RelativeItem();
                                    }
                                });
                            }
                        });
                    });
                }
            });

            return Result<byte[]>.Success(document.GeneratePdf());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to render the QR sheet PDF");
            return Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender));
        }
    }

    private IDocument BuildCardDocument(StudentCardData data)        => Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page, new PageSize(85.6f, 54, Unit.Millimetre), 6);
                ApplyDirection(page.Content()).Element(target => ComposeCard(target, data, compact: false));
            });
        });

    private void ConfigurePage(PageDescriptor page, PageSize size, float margin)
    {
        EnsureQuestPdfConfigured();
        page.Size(size);
        page.Margin(margin);
        page.DefaultTextStyle(style => style.FontFamily("Segoe UI").FontSize(9));
    }

    private IContainer ApplyDirection(IContainer container)
        => _localization.IsRightToLeft
            ? container.ContentFromRightToLeft()
            : container.ContentFromLeftToRight();

    private static void EnsureQuestPdfConfigured()
    {
        if (_questPdfConfigured)
        {
            return;
        }

        lock (InitGate)
        {
            if (_questPdfConfigured)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = true;
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
            _questPdfConfigured = true;
        }
    }

    private void ComposeCard(IContainer container, StudentCardData data, bool compact)
    {
        var qr = RenderQrPng(data.StudentCode, compact ? 256 : 512);
        var classLine = string.Join(
            " / ",
            new[] { data.LevelName, data.GradeName, data.GroupName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        container
            .Border(1)
            .BorderColor(Colors.Grey.Medium)
            .Padding(compact ? 5 : 7)
            .Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(header =>
                    {
                        header.Item().Text(data.SchoolName).Bold().FontSize(compact ? 9 : 10);
                        header.Item().Text(_localization.Tr(TranslationKeys.Students.CardTitle))
                            .FontSize(compact ? 7 : 8)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    if (!string.IsNullOrWhiteSpace(data.AcademicYear))
                    {
                        row.ConstantItem(compact ? 60 : 90)
                            .AlignRight()
                            .Text(data.AcademicYear)
                            .FontSize(compact ? 7 : 8);
                    }
                });

                column.Item().PaddingTop(compact ? 3 : 5).Row(row =>
                {
                    row.RelativeItem().Column(details =>
                    {
                        details.Item().Text(data.FullName).Bold().FontSize(compact ? 11 : 13);
                        details.Item().Text($"{_localization.Tr(TranslationKeys.Students.StudentCode)}: {data.StudentCode}")
                            .FontSize(compact ? 7 : 8);
                        if (!string.IsNullOrWhiteSpace(classLine))
                        {
                            details.Item().Text(classLine).FontSize(compact ? 7 : 8);
                        }

                        if (!string.IsNullOrWhiteSpace(data.GuardianName))
                        {
                            details.Item()
                                .Text($"{_localization.Tr(TranslationKeys.Students.GuardianName)}: {data.GuardianName}")
                                .FontSize(compact ? 7 : 8);
                        }

                        if (!string.IsNullOrWhiteSpace(data.GuardianPhone))
                        {
                            details.Item()
                                .Text($"{_localization.Tr(TranslationKeys.Students.GuardianPhone)}: {data.GuardianPhone}")
                                .FontSize(compact ? 7 : 8);
                        }
                    });

                    var qrSize = compact ? 46 : 88;
                    row.ConstantItem(qrSize).Height(qrSize).Element(target =>
                    {
                        if (qr.IsSuccess)
                        {
                            target.Image(qr.Value).FitArea();
                        }
                    });
                });

                column.Item().PaddingTop(compact ? 2 : 4).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(compact ? 6 : 7).FontColor(Colors.Grey.Darken2));
                    text.Span(_localization.Tr(TranslationKeys.Students.CardOwnerNote));
                });
            });
    }
}
