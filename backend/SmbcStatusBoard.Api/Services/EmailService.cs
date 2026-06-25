using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using MimeKit;

namespace SmbcStatusBoard.Api.Services;

public class EmailService(IConfiguration config)
{
    public async Task SendNewRequestAsync(List<(string Email, string Name)> recipients, string typeLabel, List<(string Label, string Value)> details)
    {
        if (recipients.Count == 0) return;

        var rows = string.Join("", details
            .Where(d => !string.IsNullOrWhiteSpace(d.Value))
            .Select(d => $"""
                <tr>
                  <td style="padding:8px 12px;font-weight:600;color:#374151;white-space:nowrap;border-bottom:1px solid #e5e7eb;">{d.Label}</td>
                  <td style="padding:8px 12px;color:#111827;border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(d.Value)}</td>
                </tr>
                """));

        var html = $"""
            <div style="font-family:sans-serif;max-width:560px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
              <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                <div style="background:#005DBA;padding:20px 28px;">
                  <h2 style="margin:0;color:#fff;font-size:1.2rem;">New {System.Net.WebUtility.HtmlEncode(typeLabel)} Submitted</h2>
                </div>
                <div style="padding:24px 28px;">
                  <p style="margin:0 0 20px;color:#6b7280;font-size:0.9rem;">
                    A new request has been submitted and is waiting in the status board.
                  </p>
                  <table style="width:100%;border-collapse:collapse;font-size:0.9rem;border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;">
                    {rows}
                  </table>
                  <p style="margin:20px 0 0;color:#9ca3af;font-size:0.8rem;">
                    Log in to One Accord to view and manage this request.
                  </p>
                </div>
              </div>
            </div>
            """;

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));

        foreach (var (email, name) in recipients)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = $"New {typeLabel} Submitted";
            message.Body = new TextPart("html") { Text = html };
            await client.SendAsync(message);
        }

        await client.DisconnectAsync(true);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Reset Your One Accord Password";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: auto;">
                  <h2 style="color: #005DBA;">Reset Your Password</h2>
                  <p>We received a request to reset the password for your One Accord account (<strong>{System.Net.WebUtility.HtmlEncode(toName)}</strong>).</p>
                  <p>Click the button below to choose a new password. This link expires in 1 hour.</p>
                  <a href="{resetLink}" style="display:inline-block;padding:12px 24px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;">Reset My Password</a>
                  <p style="color:#888;font-size:12px;margin-top:24px;">If you did not request a password reset, you can safely ignore this email.</p>
                </div>
                """
        };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendVolunteerRequestAsync(string recipientEmail, string recipientName, string roleLabel, string roleDescription, string sundayDate, string acceptUrl, string rejectUrl, List<(string Time, string Label)>? timeSlots = null)
    {
        // Build optional schedule section
        var scheduleHtml = "";
        if (timeSlots != null && timeSlots.Count > 0)
        {
            var rows = string.Join("", timeSlots.Select(ts => $"""
                <tr>
                  <td style="padding:7px 12px;font-weight:600;color:#005DBA;white-space:nowrap;border-bottom:1px solid #f3f4f6;">{System.Net.WebUtility.HtmlEncode(ts.Time)}</td>
                  <td style="padding:7px 12px;color:#111827;border-bottom:1px solid #f3f4f6;">{System.Net.WebUtility.HtmlEncode(ts.Label)}</td>
                </tr>
                """));
            scheduleHtml = $"""
                <p style="margin:20px 0 8px;font-weight:700;color:#374151;font-size:0.85rem;text-transform:uppercase;letter-spacing:0.05em;">Schedule</p>
                <table style="width:100%;border-collapse:collapse;font-size:0.9rem;border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;margin-bottom:24px;">
                  {rows}
                </table>
                """;
        }

        var html = $"""
            <div style="font-family:sans-serif;max-width:560px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
              <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                <div style="background:#005DBA;padding:20px 28px;">
                  <h2 style="margin:0;color:#fff;font-size:1.2rem;">Volunteer Request</h2>
                </div>
                <div style="padding:24px 28px;">
                  <p style="margin:0 0 12px;color:#111827;">Hi <strong>{System.Net.WebUtility.HtmlEncode(recipientName)}</strong>,</p>
                  <p style="margin:0 0 16px;color:#374151;">You have been requested to serve in the following role:</p>
                  <table style="width:100%;border-collapse:collapse;font-size:0.9rem;border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;margin-bottom:8px;">
                    <tr>
                      <td style="padding:8px 12px;font-weight:600;color:#374151;border-bottom:1px solid #e5e7eb;">Role</td>
                      <td style="padding:8px 12px;color:#111827;border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(roleLabel)}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 12px;font-weight:600;color:#374151;border-bottom:1px solid #e5e7eb;">Description</td>
                      <td style="padding:8px 12px;color:#111827;border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(roleDescription)}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 12px;font-weight:600;color:#374151;">Date</td>
                      <td style="padding:8px 12px;color:#111827;">{System.Net.WebUtility.HtmlEncode(sundayDate)}</td>
                    </tr>
                  </table>
                  {scheduleHtml}
                  <p style="margin:0 0 16px;color:#374151;">Please respond below:</p>
                  <div style="display:flex;gap:12px;">
                    <a href="{acceptUrl}" style="display:inline-block;padding:12px 24px;background:#059669;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;">Accept</a>
                    <a href="{rejectUrl}" style="display:inline-block;padding:12px 24px;background:#dc2626;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;">Reject</a>
                  </div>
                </div>
              </div>
            </div>
            """;

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(recipientName, recipientEmail));
        message.Subject = $"Volunteer Request: {roleLabel} on {sundayDate}";
        message.Body = new TextPart("html") { Text = html };
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendVolunteerCancellationAsync(string recipientEmail, string recipientName, string roleLabel, string sundayDate)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:560px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
              <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                <div style="background:#dc2626;padding:20px 28px;">
                  <h2 style="margin:0;color:#fff;font-size:1.2rem;">Volunteer Request Cancelled</h2>
                </div>
                <div style="padding:24px 28px;">
                  <p style="margin:0 0 12px;color:#111827;">Hi <strong>{System.Net.WebUtility.HtmlEncode(recipientName)}</strong>,</p>
                  <p style="margin:0 0 20px;color:#374151;">Your request to serve has been cancelled for the following:</p>
                  <table style="width:100%;border-collapse:collapse;font-size:0.9rem;border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;margin-bottom:24px;">
                    <tr>
                      <td style="padding:8px 12px;font-weight:600;color:#374151;border-bottom:1px solid #e5e7eb;">Role</td>
                      <td style="padding:8px 12px;color:#111827;border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(roleLabel)}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px 12px;font-weight:600;color:#374151;">Date</td>
                      <td style="padding:8px 12px;color:#111827;">{System.Net.WebUtility.HtmlEncode(sundayDate)}</td>
                    </tr>
                  </table>
                  <p style="margin:0;color:#6b7280;font-size:0.85rem;">If you have any questions, please contact your church coordinator.</p>
                </div>
              </div>
            </div>
            """;

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(recipientName, recipientEmail));
        message.Subject = $"Volunteer Request Cancelled: {roleLabel} on {sundayDate}";
        message.Body = new TextPart("html") { Text = html };
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendVolunteerResponseAsync(List<(string Email, string Name)> recipients, string volunteerName, string roleLabel, string sundayDate, bool accepted)
    {
        if (recipients.Count == 0) return;
        var verb = accepted ? "accepted" : "rejected";
        var html = $"""
            <div style="font-family:sans-serif;max-width:560px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
              <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                <div style="background:#005DBA;padding:20px 28px;">
                  <h2 style="margin:0;color:#fff;font-size:1.2rem;">Volunteer Response</h2>
                </div>
                <div style="padding:24px 28px;">
                  <p style="margin:0 0 12px;color:#111827;">
                    <strong>{System.Net.WebUtility.HtmlEncode(volunteerName)}</strong> has <strong>{verb}</strong> the <strong>{System.Net.WebUtility.HtmlEncode(roleLabel)}</strong> volunteer role for {System.Net.WebUtility.HtmlEncode(sundayDate)}.
                  </p>
                  <p style="margin:16px 0 0;color:#9ca3af;font-size:0.8rem;">Log in to One Accord to manage volunteer assignments.</p>
                </div>
              </div>
            </div>
            """;

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));

        foreach (var (email, name) in recipients)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = $"{volunteerName} {verb} volunteer role: {roleLabel}";
            message.Body = new TextPart("html") { Text = html };
            await client.SendAsync(message);
        }
        await client.DisconnectAsync(true);
    }

    public async Task SendItemCompletedAsync(string toEmail, string toName, string typeLabel, List<(string Label, string Value)> details, string? completionNote = null)
    {
        var rows = string.Join("", details
            .Where(d => !string.IsNullOrWhiteSpace(d.Value))
            .Select(d => $"""
                <tr>
                  <td style="padding:8px 12px;font-weight:600;color:#374151;white-space:nowrap;border-bottom:1px solid #e5e7eb;">{d.Label}</td>
                  <td style="padding:8px 12px;color:#111827;border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(d.Value)}</td>
                </tr>
                """));

        var html = $"""
            <div style="font-family:sans-serif;max-width:560px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
              <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                <div style="background:#059669;padding:20px 28px;">
                  <h2 style="margin:0;color:#fff;font-size:1.2rem;">✓ Your {System.Net.WebUtility.HtmlEncode(typeLabel)} Has Been Completed</h2>
                </div>
                <div style="padding:24px 28px;">
                  <p style="margin:0 0 16px;color:#111827;">Hi <strong>{System.Net.WebUtility.HtmlEncode(toName)}</strong>,</p>
                  <p style="margin:0 0 20px;color:#374151;">
                    Great news — your request has been marked as completed. Here is a summary of what was submitted:
                  </p>
                  {(string.IsNullOrWhiteSpace(completionNote) ? "" : $"""
                  <div style="margin:0 0 20px;padding:14px 18px;background:#f0fdf4;border-left:4px solid #059669;border-radius:4px;">
                    <p style="margin:0 0 6px;font-weight:700;font-size:0.85rem;color:#065f46;text-transform:uppercase;letter-spacing:0.04em;">Note from the office</p>
                    <p style="margin:0;color:#111827;white-space:pre-wrap;">{System.Net.WebUtility.HtmlEncode(completionNote)}</p>
                  </div>
                  """)}
                  <table style="width:100%;border-collapse:collapse;font-size:0.9rem;border:1px solid #e5e7eb;border-radius:6px;overflow:hidden;">
                    {rows}
                  </table>
                  <p style="margin:20px 0 0;color:#9ca3af;font-size:0.8rem;">
                    If you have any questions, please contact the church office.
                  </p>
                </div>
              </div>
            </div>
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"Your {typeLabel} Has Been Completed";
        message.Body = new TextPart("html") { Text = html };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendInviteAsync(string toEmail, string toName, string inviteLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "You've been invited to One Accord";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: auto;">
                  <h2 style="color: #005DBA;">Welcome to One Accord</h2>
                  <p>You've been added as an administrator. Click the button below to set your password and get started.</p>
                  <a href="{inviteLink}" style="display:inline-block;padding:12px 24px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;">Set My Password</a>
                  <p style="color:#888;font-size:12px;margin-top:24px;">This link expires in 48 hours.</p>
                </div>
                """
        };

        using var client = new SmtpClient();
        // Allow certs where the only issue is an incomplete revocation check (common with HostGator)
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"] ?? throw new InvalidOperationException("Email:Host not configured"), int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured"), config["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured"));
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string verifyLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Verify your One Accord account";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
                  <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                    <div style="background:#005DBA;padding:20px 28px;">
                      <h2 style="margin:0;color:#fff;font-size:1.2rem;">Welcome to One Accord!</h2>
                    </div>
                    <div style="padding:24px 28px;">
                      <p style="margin:0 0 8px;color:#111827;">Hi {System.Net.WebUtility.HtmlEncode(toName)},</p>
                      <p style="margin:0 0 20px;color:#374151;">Thanks for creating an account. Click the button below to verify your email and activate your account.</p>
                      <a href="{verifyLink}" style="display:inline-block;padding:12px 28px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;font-size:1rem;">Verify My Email</a>
                      <p style="margin:20px 0 0;color:#9ca3af;font-size:0.8rem;">This link expires in 24 hours. If you did not create an account, you can ignore this email.</p>
                    </div>
                  </div>
                </div>
                """
        };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"]!, int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"]!, config["Email:Password"]!);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendRolePromotionAsync(string toEmail, string toName, string newRole, string loginUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Your One Accord role has been updated";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
                  <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                    <div style="background:#005DBA;padding:20px 28px;">
                      <h2 style="margin:0;color:#fff;font-size:1.2rem;">Role Updated</h2>
                    </div>
                    <div style="padding:24px 28px;">
                      <p style="margin:0 0 8px;color:#111827;">Hi {System.Net.WebUtility.HtmlEncode(toName)},</p>
                      <p style="margin:0 0 20px;color:#374151;">Your account has been updated to <strong>{System.Net.WebUtility.HtmlEncode(newRole)}</strong>. You now have additional access in One Accord.</p>
                      <a href="{loginUrl}" style="display:inline-block;padding:12px 28px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;font-size:1rem;">Log In</a>
                    </div>
                  </div>
                </div>
                """
        };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"]!, int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"]!, config["Email:Password"]!);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendClassInviteAsync(string toEmail, string toName, string className, string joinLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"You've been invited to join {className}";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
                  <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                    <div style="background:#005DBA;padding:20px 28px;">
                      <h2 style="margin:0;color:#fff;font-size:1.2rem;">Class Invitation</h2>
                    </div>
                    <div style="padding:24px 28px;">
                      <p style="margin:0 0 8px;color:#111827;">Hi {System.Net.WebUtility.HtmlEncode(toName)},</p>
                      <p style="margin:0 0 20px;color:#374151;">You've been invited to join <strong>{System.Net.WebUtility.HtmlEncode(className)}</strong> at South Moore Baptist Church. Click below to create your account and join the class.</p>
                      <a href="{joinLink}" style="display:inline-block;padding:12px 28px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;font-size:1rem;">Join Class</a>
                      <p style="margin:16px 0 0;color:#6b7280;font-size:0.85rem;">This invitation expires in 7 days. If you weren't expecting this email, you can ignore it.</p>
                    </div>
                  </div>
                </div>
                """
        };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"]!, int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"]!, config["Email:Password"]!);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendClassAddedNotificationAsync(string toEmail, string toName, string className, string loginUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"You've been added to {className}";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
                  <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                    <div style="background:#005DBA;padding:20px 28px;">
                      <h2 style="margin:0;color:#fff;font-size:1.2rem;">Added to a Class</h2>
                    </div>
                    <div style="padding:24px 28px;">
                      <p style="margin:0 0 8px;color:#111827;">Hi {System.Net.WebUtility.HtmlEncode(toName)},</p>
                      <p style="margin:0 0 20px;color:#374151;">You've been added to <strong>{System.Net.WebUtility.HtmlEncode(className)}</strong> at South Moore Baptist Church.</p>
                      <a href="{loginUrl}" style="display:inline-block;padding:12px 28px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;font-size:1rem;">View in One Accord</a>
                    </div>
                  </div>
                </div>
                """
        };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"]!, int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"]!, config["Email:Password"]!);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendSpouseInviteAsync(string toEmail, string toName, string inviterName, string newUsername, string joinLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"{System.Net.WebUtility.HtmlEncode(inviterName)} added you as their spouse on One Accord";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
                  <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                    <div style="background:#005DBA;padding:20px 28px;">
                      <h2 style="margin:0;color:#fff;font-size:1.2rem;">Family Invitation</h2>
                    </div>
                    <div style="padding:24px 28px;">
                      <p style="margin:0 0 8px;color:#111827;">Hi {System.Net.WebUtility.HtmlEncode(toName)},</p>
                      <p style="margin:0 0 16px;color:#374151;"><strong>{System.Net.WebUtility.HtmlEncode(inviterName)}</strong> has added you as their spouse on One Accord, the South Moore Baptist Church member portal. Click below to set your password and get started.</p>
                      <p style="margin:0 0 20px;color:#374151;">Your username is: <strong>{System.Net.WebUtility.HtmlEncode(newUsername)}</strong></p>
                      <a href="{joinLink}" style="display:inline-block;padding:12px 28px;background:#005DBA;color:#fff;border-radius:8px;text-decoration:none;font-weight:bold;font-size:1rem;">Create My Account</a>
                      <p style="margin:16px 0 0;color:#6b7280;font-size:0.85rem;">This link expires in 7 days. If you weren't expecting this email, you can safely ignore it.</p>
                    </div>
                  </div>
                </div>
                """
        };

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"]!, int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"]!, config["Email:Password"]!);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendEventRegistrationAsync(
        List<(string Email, string Name)> recipients,
        string eventName,
        string? eventDateRange,
        string? location,
        List<string> registeredNames)
    {
        if (recipients.Count == 0) return;

        var nameList = string.Join("", registeredNames.Select(n =>
            $"<li style=\"padding:4px 0;color:#111827;\">{System.Net.WebUtility.HtmlEncode(n)}</li>"));

        var dateRow = !string.IsNullOrWhiteSpace(eventDateRange)
            ? $"<p style=\"margin:0 0 4px;color:#6b7280;font-size:0.9rem;\">{System.Net.WebUtility.HtmlEncode(eventDateRange)}</p>"
            : "";
        var locationRow = !string.IsNullOrWhiteSpace(location)
            ? $"<p style=\"margin:0 0 0;color:#6b7280;font-size:0.9rem;\">&#128205; {System.Net.WebUtility.HtmlEncode(location)}</p>"
            : "";

        var html = $"""
            <div style="font-family:sans-serif;max-width:560px;margin:auto;background:#f9fafb;padding:24px;border-radius:12px;">
              <div style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);">
                <div style="background:#005DBA;padding:20px 28px;">
                  <h2 style="margin:0;color:#fff;font-size:1.2rem;">You're Registered!</h2>
                </div>
                <div style="padding:24px 28px;">
                  <h3 style="margin:0 0 6px;color:#111827;font-size:1.1rem;">{System.Net.WebUtility.HtmlEncode(eventName)}</h3>
                  {dateRow}
                  {locationRow}
                  <hr style="border:none;border-top:1px solid #e5e7eb;margin:20px 0;" />
                  <p style="margin:0 0 10px;color:#374151;font-weight:600;">Registered attendees:</p>
                  <ul style="margin:0;padding-left:20px;">
                    {nameList}
                  </ul>
                  <p style="margin:20px 0 0;color:#9ca3af;font-size:0.8rem;">
                    See you there! Log in to One Accord if you need to make changes.
                  </p>
                </div>
              </div>
            </div>
            """;

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (chain == null) return false;
            return chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                s.Status == X509ChainStatusFlags.OfflineRevocation);
        };
        await client.ConnectAsync(config["Email:Host"]!, int.Parse(config["Email:Port"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(config["Email:Username"]!, config["Email:Password"]!);

        foreach (var (email, name) in recipients)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "One Accord", config["Email:FromAddress"] ?? "admin@church.org"));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = $"Registration Confirmed: {eventName}";
            message.Body = new TextPart("html") { Text = html };
            await client.SendAsync(message);
        }

        await client.DisconnectAsync(true);
    }
}
