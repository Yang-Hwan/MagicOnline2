using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace MagicOnline.GameApi;

/// <summary>Packaged shop art and immutable, content-addressed PNG uploads.</summary>
public sealed class ShopImages(IConfiguration configuration, IWebHostEnvironment environment)
{
    public const long MaxBytes = 8 * 1024 * 1024;
    const string UploadPrefix = "/shop-images/uploads/";
    static readonly Regex HashName = new("^[a-f0-9]{64}\\.png$", RegexOptions.CultureInvariant);
    readonly string builtins = Path.Combine(AppContext.BaseDirectory, "ShopImages", "Builtin");
    readonly string uploads = Path.GetFullPath(configuration["ShopImages:UploadDirectory"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "shop-images"));

    public record ImageOption(string Key, string Label, string PreviewUrl, string Group);
    public record UploadedImage(string Key, string Hash, int Width, int Height);

    public IReadOnlyList<ImageOption> List()
    {
        var result = new List<ImageOption>();
        if (Directory.Exists(builtins))
            foreach (var path in Directory.EnumerateFiles(builtins, "*.png", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                string key = Path.ChangeExtension(Path.GetRelativePath(builtins, path), null).Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                string group = name.StartsWith("GemPack", StringComparison.Ordinal) ? "보석 팩" :
                    name.StartsWith("CoinPack", StringComparison.Ordinal) ? "코인 팩" : "기존 상품";
                string[] tiers = ["소량", "소형", "중형", "대형", "최대"];
                string label = group != "기존 상품" && int.TryParse(name[^2..], out int tier) && tier is >= 1 and <= 5
                    ? $"{group} {tier} · {tiers[tier - 1]}" : name;
                result.Add(new(key, label, BuiltinUrl(key), group));
            }
        if (Directory.Exists(uploads))
            foreach (var path in Directory.EnumerateFiles(uploads, "*.png").OrderDescending(StringComparer.Ordinal))
                if (HashName.IsMatch(Path.GetFileName(path)))
                {
                    string key = UploadPrefix + Path.GetFileName(path);
                    result.Add(new(key, "업로드 · " + Path.GetFileName(path)[..12], key, "업로드 이미지"));
                }
        return result;
    }

    static string BuiltinUrl(string key) => "/shop-images/builtin/" + string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
    public string? Preview(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return List().FirstOrDefault(image => image.Key == key)?.PreviewUrl;
    }

    public static (int Width, int Height) ValidatePng(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new ApiFault(400, "이미지는 8MB 이하 PNG 파일로 올려 주세요.");
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 45 || !bytes.AsSpan(0, 8).SequenceEqual(signature))
            throw new ApiFault(400, "올바른 PNG 이미지 파일을 선택해 주세요.");
        int offset = 8, width = 0, height = 0;
        bool data = false, ended = false;
        while (offset <= bytes.Length - 12)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > bytes.Length - offset - 12) break;
            int size = (int)length;
            var type = bytes.AsSpan(offset + 4, 4);
            // Verify every chunk, including IHDR and compressed pixel data, before publishing it.
            uint crc = 0xffffffff;
            foreach (byte value in bytes.AsSpan(offset + 4, size + 4))
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0);
            }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + size, 4))) break;
            if (offset == 8)
            {
                if (!type.SequenceEqual("IHDR"u8) || size != 13) break;
                uint w = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8, 4));
                uint h = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 12, 4));
                if (w is < 1 or > 4096 || h is < 1 or > 4096 || (long)w * h > 16_777_216)
                    throw new ApiFault(400, "이미지 크기는 가로·세로 각각 1~4096픽셀이어야 합니다.");
                width = (int)w; height = (int)h;
            }
            else if (type.SequenceEqual("IHDR"u8)) break;
            if (type.SequenceEqual("IDAT"u8) && size > 0) data = true;
            offset += size + 12;
            if (type.SequenceEqual("IEND"u8)) { ended = size == 0 && offset == bytes.Length; break; }
        }
        if (!ended || !data || width == 0) throw new ApiFault(400, "손상되거나 불완전한 PNG 이미지입니다.");
        return (width, height);
    }

    public async Task<UploadedImage> Save(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length is <= 0 or > MaxBytes) throw new ApiFault(400, "이미지는 8MB 이하 PNG 파일로 올려 주세요.");
        await using var input = file.OpenReadStream();
        using var content = new MemoryStream();
        var buffer = new byte[65536];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (content.Length + read > MaxBytes) throw new ApiFault(400, "이미지는 8MB 이하 PNG 파일로 올려 주세요.");
            content.Write(buffer, 0, read);
        }
        byte[] bytes = content.ToArray();
        var (width, height) = ValidatePng(bytes);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Directory.CreateDirectory(uploads);
        string final = Path.Combine(uploads, hash + ".png"), temp = Path.Combine(uploads, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
            try { File.Move(temp, final, false); }
            catch (IOException) when (File.Exists(final)) { /* Identical content already stored. */ }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return new(UploadPrefix + hash + ".png", hash, width, height);
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/shop-images/builtin/{**key}", (string key, ShopImages images) =>
        {
            // Resolve through the packaged allowlist, never an arbitrary request path.
            var option = images.List().FirstOrDefault(image => image.Group != "업로드 이미지" && image.Key == key);
            return option == null ? Results.NotFound() : Results.File(
                Path.Combine(images.builtins, key.Replace('/', Path.DirectorySeparatorChar) + ".png"), "image/png");
        });
        app.MapGet("/shop-images/uploads/{name}", (string name, ShopImages images, HttpContext context) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            string path = Path.Combine(images.uploads, name);
            return !HashName.IsMatch(name) || !File.Exists(path) ? Results.NotFound() : Results.File(path, "image/png");
        });
    }
}
