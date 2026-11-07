using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.Services;

/// <summary>Outcome of one scanned/typed student code.</summary>
public enum QrScanOutcome
{
    Accepted = 0,
    Duplicate = 1,
    Invalid = 2,
    NotInGroup = 3,
}

/// <summary>One student the scanner may accept.</summary>
public sealed record QrScanStudent(int StudentId, string StudentCode, string StudentName);

/// <summary>
/// Text the scanner window displays. The ViewModel passes already-localized
/// strings because the window (like every other view) never touches the
/// localization service directly.
/// </summary>
public sealed record QrScanRequest(
    IReadOnlyList<QrScanStudent> Students,
    string Title,
    string Hint,
    string ManualHint,
    string CameraUnavailableText,
    string ScannedLabel,
    string WaitingLabel,
    string FinishLabel,
    string CancelLabel,
    string DuplicateMessage,
    string InvalidMessage,
    string WrongGroupMessage,
    string AcceptedMessageTemplate,
    string ScannedCountTemplate);

/// <summary>Accepted students when the operator confirms; null on cancel.</summary>
public sealed record QrScanResult(IReadOnlyList<int> StudentIds);

/// <summary>
/// Pure duplicate-prevention state of one scanning session. It is deliberately
/// outside the window so a test can prove that the same code is accepted once,
/// that a code from another group is refused, and that malformed input never
/// reaches the roster.
/// </summary>
public sealed class QrScanSession
{
    private readonly Dictionary<string, QrScanStudent> _byCode;
    private readonly HashSet<int> _accepted = new();

    public QrScanSession(IEnumerable<QrScanStudent> students)
    {
        ArgumentNullException.ThrowIfNull(students);

        _byCode = new Dictionary<string, QrScanStudent>(StringComparer.OrdinalIgnoreCase);
        foreach (var student in students)
        {
            if (student.StudentId <= 0 || string.IsNullOrWhiteSpace(student.StudentCode))
            {
                continue;
            }

            _byCode[student.StudentCode.Trim()] = student;
        }
    }

    public int Count => _accepted.Count;

    public IReadOnlyList<int> AcceptedStudentIds => _accepted.ToList();

    public QrScanOutcome Scan(string? payload, out QrScanStudent? student)
    {
        student = null;

        if (!QrAttendanceCode.TryParseInput(payload, out var code))
        {
            return QrScanOutcome.Invalid;
        }

        if (!_byCode.TryGetValue(code, out student))
        {
            return QrScanOutcome.NotInGroup;
        }

        return _accepted.Add(student.StudentId)
            ? QrScanOutcome.Accepted
            : QrScanOutcome.Duplicate;
    }
}
