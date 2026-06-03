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
                    Log in to SMBC Admin to view and manage this request.
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
            message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "SMBC Admin", config["Email:FromAddress"] ?? "admin@church.org"));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = $"New {typeLabel} Submitted";
            message.Body = new TextPart("html") { Text = html };
            await client.SendAsync(message);
        }

        await client.DisconnectAsync(true);
    }

    public async Task SendInviteAsync(string toEmail, string toName, string inviteLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config["Email:FromName"] ?? "SMBC Admin", config["Email:FromAddress"] ?? "admin@church.org"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "You've been invited to SMBC Admin";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family: sans-serif; max-width: 480px; margin: auto;">
                  <h2 style="color: #005DBA;">Welcome to SMBC Admin</h2>
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
}
