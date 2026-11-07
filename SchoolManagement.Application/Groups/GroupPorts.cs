using SchoolManagement.Application.Common;

namespace SchoolManagement.Application.Groups;

/// <summary>
/// Ports implemented by Infrastructure. Domain and Application never reference
/// QuestPDF; the desktop only receives PDF bytes.
/// </summary>
public interface IGroupReportService
{
    Task<Result<byte[]>> BuildFullReportAsync(
        GroupReportData data,
        CancellationToken cancellationToken = default);
}
