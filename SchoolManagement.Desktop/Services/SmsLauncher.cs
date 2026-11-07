using System.Diagnostics;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Opens the Windows messaging application with an <c>sms:</c> URI. The app is
/// offline-only and never sends a message itself: it only starts the user's
/// messaging app, exactly like the phone app used the platform intent.
/// </summary>
public interface ISmsLauncher
{
    Task<bool> OpenAsync(IReadOnlyList<string> phones, string body);
}

public sealed class SmsLauncher : ISmsLauncher
{
    public Task<bool> OpenAsync(IReadOnlyList<string> phones, string body)
    {
        ArgumentNullException.ThrowIfNull(phones);
        ArgumentNullException.ThrowIfNull(body);

        var recipients = phones
            .Where(phone => !string.IsNullOrWhiteSpace(phone))
            .Select(phone => phone.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (recipients.Count == 0)
        {
            return Task.FromResult(false);
        }

        var uri = "sms:" + string.Join(",", recipients)
            + (string.IsNullOrWhiteSpace(body) ? string.Empty : "?body=" + Uri.EscapeDataString(body));

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            // No handler is registered on this machine; the caller shows the
            // localized "cannot open the messaging app" message and the rows
            // stay in sms_logs so the attempt is still auditable.
            return Task.FromResult(false);
        }
    }
}
