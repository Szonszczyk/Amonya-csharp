using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace Amonya.Integrations;

internal sealed class APBSAmmoBlacklistJournal
{
    [JsonRequired]
    public int Version { get; set; } = 1;
    [JsonRequired]
    public string BlacklistPath { get; set; } = "";
    [JsonRequired]
    public Dictionary<int, HashSet<string>> Owned { get; set; } = APBSAmmoBlacklistStore.EmptyTiers();
    [JsonRequired]
    public APBSAmmoPendingUpdate? Pending { get; set; }
}

internal sealed class APBSAmmoPendingUpdate
{
    [JsonRequired]
    public Dictionary<int, HashSet<string>> Added { get; set; } = APBSAmmoBlacklistStore.EmptyTiers();
    [JsonRequired]
    public Dictionary<int, HashSet<string>> Removed { get; set; } = APBSAmmoBlacklistStore.EmptyTiers();
    [JsonRequired]
    public string BeforeHash { get; set; } = "";
    [JsonRequired]
    public string AfterHash { get; set; } = "";
}

internal sealed record APBSAmmoUpdateResult(int Added, int Removed, bool Refreshed);

// Ownership and recovery are limited to the ammo tiers. Whole-file fingerprints
// are still checked immediately before replacement and across each live reload.
internal sealed class APBSAmmoBlacklistStore(string statePath, string blacklistPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private APBSAmmoBlacklistJournal _journal = new() { BlacklistPath = Path.GetFullPath(blacklistPath) };

    internal static Dictionary<int, HashSet<string>> EmptyTiers() =>
        Enumerable.Range(1, 7).ToDictionary(tier => tier, _ => new HashSet<string>(StringComparer.Ordinal));

    public void Load()
    {
        if (!File.Exists(statePath)) return;
        var journal = JsonSerializer.Deserialize<APBSAmmoBlacklistJournal>(File.ReadAllText(statePath), JsonOptions)
            ?? throw new InvalidDataException("Amonya's APBS ownership record is empty.");
        if (journal.Version != 1 || !string.Equals(journal.BlacklistPath, Path.GetFullPath(blacklistPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Amonya's APBS ownership record has an unsupported version or belongs to another APBS folder.");
        ValidateTiers(journal.Owned);
        if (journal.Pending is { } pending)
        {
            ValidateTiers(pending.Added);
            ValidateTiers(pending.Removed);
            if (!ValidHash(pending.BeforeHash) || !ValidHash(pending.AfterHash) || pending.BeforeHash == pending.AfterHash
                || Enumerable.Range(1, 7).Any(tier => pending.Added[tier].Overlaps(pending.Removed[tier])
                    || !pending.Removed[tier].IsSubsetOf(journal.Owned[tier])))
                throw new InvalidDataException("Amonya's APBS pending update is invalid.");
        }
        _journal = journal;
    }

    public async Task<APBSAmmoUpdateResult> SynchronizeAsync(HashSet<string> desired,
        Func<CancellationToken, Task<string>> reload, CancellationToken token)
    {
        if (desired.Any(id => !MongoId.IsValidMongoId(id)))
            throw new InvalidDataException("Amonya generated an invalid ammo ID.");
        bool recovered = _journal.Pending is not null;
        if (recovered) await RecoverAsync(reload, token);

        string before = await File.ReadAllTextAsync(blacklistPath, token);
        var root = Parse(before);
        var owned = EmptyTiers();
        var added = EmptyTiers();
        var removed = EmptyTiers();
        string beforeHash = AmmoHash(root);
        for (int tier = 1; tier <= 7; tier++)
        {
            var current = Tier(root, tier);
            added[tier].UnionWith(desired.Except(current));
            removed[tier].UnionWith(_journal.Owned[tier].Except(desired).Intersect(current));
            owned[tier].UnionWith(_journal.Owned[tier].Intersect(current).Intersect(desired));
            owned[tier].UnionWith(added[tier]);
            current.ExceptWith(removed[tier]);
            current.UnionWith(added[tier]);
            if (added[tier].Count > 0 || removed[tier].Count > 0) SetTier(root, tier, current);
        }
        int addedCount = added.Values.Sum(ids => ids.Count);
        int removedCount = removed.Values.Sum(ids => ids.Count);
        if (addedCount == 0 && removedCount == 0)
        {
            if (Hash(await File.ReadAllTextAsync(blacklistPath, token)) != Hash(before))
                throw new IOException("APBS blacklist changed while Amonya was checking it.");
            if (!_journal.Owned.All(entry => entry.Value.SetEquals(owned[entry.Key])))
                await CommitAsync(owned, token);
            return new(0, 0, recovered);
        }

        string after = Serialize(root);
        var pending = new APBSAmmoPendingUpdate
        {
            Added = added, Removed = removed, BeforeHash = beforeHash, AfterHash = AmmoHash(root)
        };
        await SaveAsync(new() { BlacklistPath = _journal.BlacklistPath, Owned = _journal.Owned, Pending = pending }, token);
        try
        {
            await AtomicWriteAsync(blacklistPath, after, Hash(before), token);
            await ConfirmReloadAsync(reload, after, token);
            await CommitAsync(owned, token);
            return new(addedCount, removedCount, true);
        }
        catch (Exception applicationError)
        {
            // Await restoration even when shutdown cancelled the original update.
            // A failed restoration retains the journal for the next startup.
            try { await RecoverAsync(reload, CancellationToken.None); }
            catch (Exception recoveryError)
            {
                throw new IOException($"{applicationError.Message} APBS restoration is unconfirmed; Amonya retained its recovery record. {recoveryError.Message}", applicationError);
            }
            throw new IOException($"{applicationError.Message} Amonya restored the previous ammo policy and confirmed an APBS reload.", applicationError);
        }
    }

    private async Task RecoverAsync(Func<CancellationToken, Task<string>> reload, CancellationToken token)
    {
        if (_journal.Pending is not { } pending) return;
        string currentJson = await File.ReadAllTextAsync(blacklistPath, token);
        var root = Parse(currentJson);
        string hash = AmmoHash(root);
        if (hash != pending.BeforeHash && hash != pending.AfterHash)
            throw new IOException("APBS ammo tiers changed during an interrupted Amonya update. Recovery is ambiguous; the blacklist and ownership record were preserved.");

        if (hash == pending.AfterHash)
        {
            for (int tier = 1; tier <= 7; tier++)
            {
                var current = Tier(root, tier);
                current.ExceptWith(pending.Added[tier]);
                current.UnionWith(pending.Removed[tier]);
                SetTier(root, tier, current);
            }
            if (AmmoHash(root) != pending.BeforeHash)
                throw new InvalidDataException("Amonya's APBS recovery delta does not match the recorded previous ammo policy.");
            string restored = Serialize(root);
            await AtomicWriteAsync(blacklistPath, restored, Hash(currentJson), token);
            currentJson = restored;
        }
        await ConfirmReloadAsync(reload, currentJson, token);
        await CommitAsync(_journal.Owned, token);
    }

    private async Task ConfirmReloadAsync(Func<CancellationToken, Task<string>> reload, string expectedJson, CancellationToken token)
    {
        string result = await reload(token);
        if (result != "Success") throw new IOException($"APBS reload returned {result}.");
        if (Hash(await File.ReadAllTextAsync(blacklistPath, token)) != Hash(expectedJson))
            throw new IOException("APBS blacklist changed during Amonya's refresh.");
    }

    private async Task CommitAsync(Dictionary<int, HashSet<string>> owned, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (owned.Values.All(ids => ids.Count == 0))
        {
            File.Delete(statePath);
            _journal = new() { BlacklistPath = _journal.BlacklistPath };
            return;
        }
        await SaveAsync(new() { BlacklistPath = _journal.BlacklistPath, Owned = owned }, token);
    }

    private async Task SaveAsync(APBSAmmoBlacklistJournal journal, CancellationToken token)
    {
        await AtomicWriteAsync(statePath, JsonSerializer.Serialize(journal, JsonOptions) + Environment.NewLine, null, token);
        _journal = journal;
    }

    private static JsonObject Parse(string json)
    {
        var root = JsonNode.Parse(json, documentOptions: new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject
            ?? throw new InvalidDataException("APBS blacklist must be a JSON object.");
        if (root["ammoBlacklist"] is not JsonObject ammo)
            throw new InvalidDataException("APBS ammoBlacklist is missing or invalid.");
        for (int tier = 1; tier <= 7; tier++)
        {
            if (ammo[$"tier{tier}Blacklist"] is not JsonArray ids
                || ids.Any(node => node is not JsonValue value || !value.TryGetValue<string>(out var id) || !MongoId.IsValidMongoId(id)))
                throw new InvalidDataException($"APBS ammoBlacklist/tier{tier}Blacklist must contain valid ammo IDs.");
        }
        return root;
    }

    private static HashSet<string> Tier(JsonObject root, int tier) =>
        ((JsonArray)root["ammoBlacklist"]![$"tier{tier}Blacklist"]!).Select(node => node!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    private static void SetTier(JsonObject root, int tier, HashSet<string> ids)
    {
        var retained = ((JsonArray)root["ammoBlacklist"]![$"tier{tier}Blacklist"]!)
            .Select(node => node!.GetValue<string>()).Where(ids.Contains).ToList();
        retained.AddRange(ids.Except(retained).Order(StringComparer.Ordinal));
        root["ammoBlacklist"]![$"tier{tier}Blacklist"] = new JsonArray(retained.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
    }

    private static string AmmoHash(JsonObject root) => Hash(string.Join("\n",
        Enumerable.Range(1, 7).Select(tier => $"{tier}:" + string.Join(",", Tier(root, tier).Order(StringComparer.Ordinal)))));

    private static void ValidateTiers(Dictionary<int, HashSet<string>> tiers)
    {
        if (tiers is null || tiers.Count != 7 || Enumerable.Range(1, 7).Any(tier => !tiers.TryGetValue(tier, out var ids)
            || ids is null || ids.Any(id => !MongoId.IsValidMongoId(id))))
            throw new InvalidDataException("Amonya's APBS ownership tiers are invalid.");
    }

    private static bool ValidHash(string hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    private static string Serialize(JsonObject root) => root.ToJsonString(JsonOptions) + Environment.NewLine;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task AtomicWriteAsync(string path, string contents, string? expectedHash, CancellationToken token)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(contents), token);
                stream.Flush(flushToDisk: true);
            }
            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (expectedHash is not null && (!File.Exists(path) || Hash(await File.ReadAllTextAsync(path, token)) != expectedHash))
                        throw new IOException("APBS blacklist changed externally; Amonya refused to overwrite it.");
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    return;
                }
                catch (Exception ex) when (OperatingSystem.IsWindows() && ex is IOException or UnauthorizedAccessException
                    && (ex.HResult & 0xFFFF) is 5 or 32 or 33 or 1175 && attempt < 4)
                {
                    await Task.Delay(100 * (1 << attempt), token);
                }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
