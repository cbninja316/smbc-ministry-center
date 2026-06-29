using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Models;

namespace SmbcStatusBoard.Api.Controllers;

[ApiController]
[Route("api/twilio")]
[Authorize]
public class TwilioController(AppDbContext db, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private int GetChurchId() =>
        int.Parse(User.FindFirstValue("ChurchId") ?? "0");

    private bool IsSuperAdmin() =>
        User.FindFirstValue(ClaimTypes.Role) == "SuperAdmin";

    private async Task<(string accountSid, string authToken, string fromNumber)?> GetCredentialsAsync(int churchId)
    {
        var settings = await db.TwilioSettings.FirstOrDefaultAsync(t => t.ChurchId == churchId);
        if (settings == null || !settings.Enabled) return null;
        return (settings.AccountSid, settings.AuthToken, settings.FromNumber);
    }

    private async Task<string?> SendTwilioSmsAsync(string accountSid, string authToken, string fromNumber, string toNumber, string body)
    {
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("To", toNumber),
            new KeyValuePair<string, string>("From", fromNumber),
            new KeyValuePair<string, string>("Body", body),
        });
        var response = await client.PostAsync(
            $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json", content);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        // Parse SID from response JSON
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : null;
    }

    // GET /api/twilio/settings
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        if (!IsSuperAdmin()) return Forbid();
        var churchId = GetChurchId();
        var settings = await db.TwilioSettings.FirstOrDefaultAsync(t => t.ChurchId == churchId);
        return Ok(new
        {
            accountSid = settings?.AccountSid ?? "",
            fromNumber = settings?.FromNumber ?? "",
            enabled = settings?.Enabled ?? false,
            authTokenSet = !string.IsNullOrEmpty(settings?.AuthToken),
        });
    }

    // PUT /api/twilio/settings
    [HttpPut("settings")]
    public async Task<IActionResult> PutSettings([FromBody] TwilioSettingsRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();
        var churchId = GetChurchId();
        var settings = await db.TwilioSettings.FirstOrDefaultAsync(t => t.ChurchId == churchId);
        if (settings == null)
        {
            settings = new TwilioSettings { ChurchId = churchId };
            db.TwilioSettings.Add(settings);
        }

        settings.AccountSid = req.AccountSid ?? settings.AccountSid;
        if (!string.IsNullOrEmpty(req.AuthToken))
            settings.AuthToken = req.AuthToken;
        settings.FromNumber = req.FromNumber ?? settings.FromNumber;
        settings.Enabled = req.Enabled;
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { message = "Twilio settings saved." });
    }

    // POST /api/twilio/test
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestSmsRequest req)
    {
        if (!IsSuperAdmin()) return Forbid();
        var churchId = GetChurchId();
        var creds = await GetCredentialsAsync(churchId);
        if (creds == null) return BadRequest(new { message = "Twilio not configured or not enabled." });

        var (accountSid, authToken, fromNumber) = creds.Value;
        var sid = await SendTwilioSmsAsync(accountSid, authToken, fromNumber, req.ToNumber, "Test message from One Accord");
        if (sid == null) return BadRequest(new { message = "Failed to send test message via Twilio." });

        return Ok(new { message = "Test message sent.", sid });
    }

    // GET /api/twilio/conversations
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var churchId = GetChurchId();
        var messages = await db.SmsMessages
            .Where(m => m.ChurchId == churchId)
            .ToListAsync();

        var conversations = messages
            .GroupBy(m => m.ContactNumber)
            .Select(g =>
            {
                var last = g.OrderByDescending(m => m.CreatedAt).First();
                var unread = g.Count(m => m.Direction == "inbound" && m.Status == "received");
                return new
                {
                    contactNumber = g.Key,
                    contactName = last.ContactName,
                    lastMessage = last.Body,
                    lastMessageAt = last.CreatedAt,
                    direction = last.Direction,
                    unreadCount = unread,
                };
            })
            .OrderByDescending(c => c.lastMessageAt)
            .ToList();

        return Ok(conversations);
    }

    // GET /api/twilio/messages/{contactNumber}
    [HttpGet("messages/{contactNumber}")]
    public async Task<IActionResult> GetMessages(string contactNumber)
    {
        var churchId = GetChurchId();
        var messages = await db.SmsMessages
            .Where(m => m.ChurchId == churchId && m.ContactNumber == contactNumber)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Body, m.Direction, m.Status, m.CreatedAt })
            .ToListAsync();

        return Ok(messages);
    }

    // POST /api/twilio/send
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendSmsRequest req)
    {
        var churchId = GetChurchId();
        var creds = await GetCredentialsAsync(churchId);
        if (creds == null) return BadRequest(new { message = "Twilio not configured or not enabled." });

        var (accountSid, authToken, fromNumber) = creds.Value;
        var sid = await SendTwilioSmsAsync(accountSid, authToken, fromNumber, req.ContactNumber, req.Body);

        var msg = new SmsMessage
        {
            ChurchId = churchId,
            ContactNumber = req.ContactNumber,
            ContactName = req.ContactName,
            Body = req.Body,
            Direction = "outbound",
            Status = sid != null ? "sent" : "failed",
            TwilioSid = sid,
        };
        db.SmsMessages.Add(msg);
        await db.SaveChangesAsync();

        return Ok(new { msg.Id, msg.Body, msg.Direction, msg.Status, msg.CreatedAt, msg.TwilioSid });
    }

    // GET /api/twilio/scheduled
    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduled()
    {
        var churchId = GetChurchId();
        var items = await db.ScheduledSms
            .Where(s => s.ChurchId == churchId && !s.Sent)
            .OrderBy(s => s.ScheduledFor)
            .ToListAsync();
        return Ok(items);
    }

    // POST /api/twilio/scheduled
    [HttpPost("scheduled")]
    public async Task<IActionResult> CreateScheduled([FromBody] ScheduledSmsRequest req)
    {
        var churchId = GetChurchId();
        var item = new ScheduledSms
        {
            ChurchId = churchId,
            ContactNumber = req.ContactNumber,
            ContactName = req.ContactName,
            Body = req.Body,
            ScheduledFor = req.ScheduledFor,
        };
        db.ScheduledSms.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    // DELETE /api/twilio/scheduled/{id}
    [HttpDelete("scheduled/{id:int}")]
    public async Task<IActionResult> DeleteScheduled(int id)
    {
        var churchId = GetChurchId();
        var item = await db.ScheduledSms.FirstOrDefaultAsync(s => s.Id == id && s.ChurchId == churchId);
        if (item == null) return NotFound();
        if (item.Sent) return BadRequest(new { message = "Cannot delete an already-sent scheduled SMS." });

        db.ScheduledSms.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/twilio/webhook — no [Authorize]
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromForm] TwilioWebhookPayload payload)
    {
        // Find church by matching ToNumber (the Twilio number that received the message)
        var toNumber = payload.To;
        var settings = await db.TwilioSettings.FirstOrDefaultAsync(t => t.FromNumber == toNumber);

        if (settings != null)
        {
            var msg = new SmsMessage
            {
                ChurchId = settings.ChurchId,
                ContactNumber = payload.From ?? "",
                Body = payload.Body ?? "",
                Direction = "inbound",
                Status = "received",
                TwilioSid = payload.MessageSid,
            };
            db.SmsMessages.Add(msg);
            await db.SaveChangesAsync();
        }

        return Content("<?xml version=\"1.0\"?><Response></Response>", "text/xml");
    }
}

// ---- Request / Payload DTOs ----

public class TwilioSettingsRequest
{
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? FromNumber { get; set; }
    public bool Enabled { get; set; }
}

public class TestSmsRequest
{
    public string ToNumber { get; set; } = string.Empty;
}

public class SendSmsRequest
{
    public string ContactNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string Body { get; set; } = string.Empty;
}

public class ScheduledSmsRequest
{
    public string ContactNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
}

public class TwilioWebhookPayload
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Body { get; set; }
    public string? MessageSid { get; set; }
}
