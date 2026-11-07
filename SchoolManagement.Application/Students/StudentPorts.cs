using SchoolManagement.Application.Common;

namespace SchoolManagement.Application.Students;

// Ports implemented by Infrastructure. The application layer only describes
// what it needs; CSV/Excel writing and PDF/QR rendering stay outside Domain and
// Application so the UI never references a rendering library directly.

public interface IStudentFileGateway
{
    string DefaultExtension(StudentFileFormat format);

    /// <summary>Serializes the export rows. CSV is UTF-8 with a BOM for Excel.</summary>
    Task<Result<byte[]>> WriteAsync(
        StudentFileFormat format,
        IReadOnlyList<StudentExportRow> rows,
        CancellationToken cancellationToken = default);

    /// <summary>Reads rows and per-row parse errors without throwing at the UI.</summary>
    Task<Result<ParsedStudentFile>> ReadAsync(
        StudentFileFormat format,
        byte[] content,
        CancellationToken cancellationToken = default);
}

public interface IStudentCardService
{
    /// <summary>PNG bytes of a single QR code for the given payload.</summary>
    Result<byte[]> RenderQrPng(string payload, int pixelSize = 512);

    /// <summary>PNG preview of the printable card (rendered from the same PDF layout).</summary>
    Result<byte[]> RenderCardPreviewPng(StudentCardData data);

    /// <summary>One-card PDF ready for printing or saving.</summary>
    Result<byte[]> RenderCardPdf(StudentCardData data);

    /// <summary>A4 PDF laid out as a 3-column grid of student cards.</summary>
    Result<byte[]> RenderCardsPdf(IReadOnlyList<StudentCardData> cards);

    /// <summary>A4 PDF laid out as a QR-code sheet for bulk printing.</summary>
    Result<byte[]> RenderQrSheetPdf(IReadOnlyList<StudentQrItem> items);
}

/// <summary>Raw file bytes plus the chosen format, sent from the UI to the use case.</summary>
public sealed record StudentFileRequest(StudentFileFormat Format, byte[] Content);

public sealed record StudentExportResult(string FileName, byte[] Content, int Count);

public sealed record StudentImportRequest(StudentFileFormat Format, byte[] Content);
