using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using MimeKit;

namespace SmbcStatusBoard.Api.Services;

public class EmailService(IConfiguration config)
{
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
