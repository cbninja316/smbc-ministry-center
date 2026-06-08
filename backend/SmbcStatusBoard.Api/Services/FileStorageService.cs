namespace SmbcStatusBoard.Api.Services;

public class FileStorageService(IConfiguration config)
{
    private string StoragePath =>
        config["Storage:ReceiptsPath"] ?? Path.Combine(AppContext.BaseDirectory, "receipts");

    private string EventPhotosPath =>
        config["Storage:EventPhotosPath"] ?? Path.Combine(AppContext.BaseDirectory, "event-photos");

    // ── Event Photos ─────────────────────────────────────────────────────────

    public async Task<string> SaveEventPhotoAsync(int itemId, Stream stream, string originalName)
    {
        var folder = Path.Combine(EventPhotosPath, itemId.ToString());
        Directory.CreateDirectory(folder);

        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(originalName)}";
        var fullPath = Path.Combine(folder, safeName);

        using var dest = File.Create(fullPath);
        await stream.CopyToAsync(dest);

        return safeName;
    }

    public Stream GetEventPhotoStream(int itemId, string fileName)
    {
        var fullPath = Path.Combine(EventPhotosPath, itemId.ToString(), fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Event photo not found.", fileName);
        return File.OpenRead(fullPath);
    }

    public void DeleteEventPhoto(int itemId, string fileName)
    {
        var fullPath = Path.Combine(EventPhotosPath, itemId.ToString(), fileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    public void DeleteEventPhotosFolder(int itemId)
    {
        var folder = Path.Combine(EventPhotosPath, itemId.ToString());
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    // ── Receipts ─────────────────────────────────────────────────────────────

    public async Task<string> SaveReceiptAsync(Stream fileStream, string fileName)
    {
        Directory.CreateDirectory(StoragePath);

        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(StoragePath, safeName);

        using var dest = File.Create(fullPath);
        await fileStream.CopyToAsync(dest);

        return safeName;
    }

    public Stream GetReceiptStream(string fileName)
    {
        var fullPath = Path.Combine(StoragePath, fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Receipt image not found.", fileName);

        return File.OpenRead(fullPath);
    }

    public void DeleteReceipt(string fileName)
    {
        var fullPath = Path.Combine(StoragePath, fileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    public string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".png"  => "image/png",
            ".webp" => "image/webp",
            _       => "image/jpeg"
        };
    }
}
