namespace SmbcStatusBoard.Api.Services;

public class FileStorageService(IConfiguration config)
{
    private string StoragePath =>
        config["Storage:ReceiptsPath"] ?? Path.Combine(AppContext.BaseDirectory, "receipts");

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
