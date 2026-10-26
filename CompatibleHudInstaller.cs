using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace KyvoraPvp;

/// <summary>
/// Installs client-side HUD projects only when Modrinth has an exact Forge build
/// for the selected Minecraft release. Missing optional HUD builds never stop launch.
/// </summary>
internal static class CompatibleHudInstaller
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly (string Slug, string Name)[] Projects =
    [
        ("hippo-keystrokes", "Hippo Keystrokes"),
        ("freelook-oneconfig", "Freelook")
    ];

    public static async Task<IReadOnlyList<string>> InstallAsync(
        string gameDirectory,
        string gameVersion,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modsDirectory = Path.Combine(gameDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);
        var installed = new List<string>();
        var visitedVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                progress?.Report($"Buscando {project.Name} compatible con {gameVersion}…");
                using var version = await FindProjectVersionAsync(project.Slug, gameVersion, cancellationToken);
                if (version is null)
                    continue;

                await InstallVersionTreeAsync(version.RootElement, gameVersion, modsDirectory, visitedVersions, installed,
                    cancellationToken, depth: 0);
                progress?.Report($"{project.Name} listo para Minecraft {gameVersion}.");
            }
            catch (Exception ex)
            {
                progress?.Report($"No se pudo preparar {project.Name}; continuaré sin ese mod ({ex.Message}).");
            }
        }

        return installed;
    }

    private static async Task<JsonDocument?> FindProjectVersionAsync(
        string project,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        var games = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { gameVersion }));
        var loaders = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { "forge" }));
        using var response = await Http.GetAsync(
            $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(project)}/version?game_versions={games}&loaders={loaders}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var version = json.RootElement.EnumerateArray().FirstOrDefault();
        if (version.ValueKind != JsonValueKind.Object)
        {
            json.Dispose();
            return null;
        }

        // Return an independent document so the parent result can be disposed safely.
        var selected = JsonDocument.Parse(version.GetRawText());
        json.Dispose();
        return selected;
    }

    private static async Task<JsonDocument?> FindDependencyVersionAsync(
        JsonElement dependency,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        if (dependency.TryGetProperty("version_id", out var versionId) &&
            versionId.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(versionId.GetString()))
        {
            using var response = await Http.GetAsync(
                $"https://api.modrinth.com/v2/version/{Uri.EscapeDataString(versionId.GetString()!)}",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (VersionMatchesForge(json.RootElement, gameVersion))
                return json;
            json.Dispose();
            return null;
        }

        if (dependency.TryGetProperty("project_id", out var projectId) && projectId.ValueKind == JsonValueKind.String)
            return await FindProjectVersionAsync(projectId.GetString()!, gameVersion, cancellationToken);

        return null;
    }

    private static async Task InstallVersionTreeAsync(
        JsonElement version,
        string gameVersion,
        string modsDirectory,
        HashSet<string> visitedVersions,
        List<string> installed,
        CancellationToken cancellationToken,
        int depth)
    {
        if (depth > 8)
            throw new InvalidOperationException("Se alcanzó el límite de dependencias del mod.");
        if (!VersionMatchesForge(version, gameVersion))
            throw new InvalidOperationException($"Una dependencia no tiene build Forge para {gameVersion}.");

        var versionId = version.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(versionId) || !visitedVersions.Add(versionId))
            return;

        if (version.TryGetProperty("dependencies", out var dependencies) && dependencies.ValueKind == JsonValueKind.Array)
        {
            foreach (var dependency in dependencies.EnumerateArray())
            {
                if (!dependency.TryGetProperty("dependency_type", out var type) || type.GetString() != "required")
                    continue;
                using var dependencyVersion = await FindDependencyVersionAsync(dependency, gameVersion, cancellationToken)
                    ?? throw new InvalidOperationException("Falta una dependencia requerida compatible con Forge.");
                await InstallVersionTreeAsync(dependencyVersion.RootElement, gameVersion, modsDirectory,
                    visitedVersions, installed, cancellationToken, depth + 1);
            }
        }

        var files = version.GetProperty("files");
        var file = files.EnumerateArray().FirstOrDefault(item =>
            item.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True);
        if (file.ValueKind != JsonValueKind.Object)
            file = files.EnumerateArray().FirstOrDefault();
        if (file.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Modrinth no proporcionó un archivo descargable.");

        var downloadUrl = file.GetProperty("url").GetString();
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("La URL del mod no usa HTTPS.");

        var fileName = Path.GetFileName(file.GetProperty("filename").GetString());
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("El nombre de archivo del mod no es válido.");
        var expectedHash = file.GetProperty("hashes").GetProperty("sha512").GetString();
        if (string.IsNullOrWhiteSpace(expectedHash))
            throw new InvalidOperationException("Modrinth no proporcionó el SHA-512 del archivo.");

        var bytes = await Http.GetByteArrayAsync(uri, cancellationToken);
        var actualHash = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
            throw new InvalidOperationException($"La verificación SHA-512 falló para {fileName}.");

        var destination = Path.Combine(modsDirectory, fileName);
        if (File.Exists(destination))
        {
            var existingHash = Convert.ToHexString(SHA512.HashData(await File.ReadAllBytesAsync(destination, cancellationToken)))
                .ToLowerInvariant();
            if (string.Equals(existingHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                installed.Add(fileName);
                return;
            }

            var extension = Path.GetExtension(fileName);
            destination = Path.Combine(modsDirectory,
                Path.GetFileNameWithoutExtension(fileName) + "-" + actualHash[..8] + extension);
        }

        var temporaryPath = destination + ".download";
        await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
        try
        {
            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        installed.Add(Path.GetFileName(destination));
    }

    private static bool VersionMatchesForge(JsonElement version, string gameVersion)
    {
        return version.TryGetProperty("game_versions", out var gameVersions) &&
               gameVersions.ValueKind == JsonValueKind.Array &&
               gameVersions.EnumerateArray().Any(item => item.GetString() == gameVersion) &&
               version.TryGetProperty("loaders", out var loaders) &&
               loaders.ValueKind == JsonValueKind.Array &&
               loaders.EnumerateArray().Any(item => item.GetString() == "forge");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KyvoraClient/26.2 (compatible client HUD installer)");
        return client;
    }
}
