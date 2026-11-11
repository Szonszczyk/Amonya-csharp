using System.Reflection;
using Amonya.CustomClasses;
using Amonya.Helpers;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Enums;

namespace Amonya.Integrations;

[Injectable(InjectionType.Singleton)]
public class APBSIntegration(
    ConfigLoader configLoader,
    CustomLogger logger,
    ModsCompatibility modsCompatibility,
    CustomItemCreator customItemCreator,
    ItemHelper itemHelper,
    ModHelper modHelper)
{
    public async Task InitializeAsync(CancellationToken token)
    {
        string folder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string statePath = Path.Combine(folder, "config", "apbs-ammo-blacklist-state.json");
        bool enabled = configLoader.Config.EnableAPBSAmmoBlacklist;
        if (!enabled && !File.Exists(statePath)) return;

        try
        {
            var apbs = modsCompatibility.Modlist.Find(mod => mod.ModMetadata.ModGuid == "com.acidphantasm.progressivebotsystem");
            if (apbs is null)
            {
                logger.Warning("APBS ammo blacklist update skipped because APBS is not installed. Any existing Amonya ownership record is retained.");
                return;
            }
            string blacklistPath = Path.Combine(apbs.Directory, "blacklists.json");
            var apbsType = apbs.Assemblies.Select(assembly => assembly.GetType("ProgressiveBotSystem.Globals.ModConfig", false))
                .FirstOrDefault(type => type is not null);
            var reload = ResolveReload(apbsType);
            var desired = enabled
                ? customItemCreator.ItemsAdded.Keys.Where(id => itemHelper.IsOfBaseclass(id, BaseClasses.AMMO))
                    .Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            async Task UpdateAsync(CancellationToken updateToken)
            {
                var store = new APBSAmmoBlacklistStore(statePath, blacklistPath);
                store.Load();
                var result = await store.SynchronizeAsync(desired, cancellationToken => ReloadAsync(reload, cancellationToken), updateToken);
                logger.Info($"APBS ammo blacklist: {desired.Count} Amonya ammo templates requested; {result.Added} tier entries added, {result.Removed} removed; full refresh {(result.Refreshed ? "yes" : "no")}.");
            }

            var dwv = modsCompatibility.Modlist.Find(mod => mod.ModMetadata.ModGuid == "com.szonszczyk.definitiveweaponvariants");
            if (dwv is null)
            {
                await UpdateAsync(token);
                return;
            }
            var bridge = dwv.Assemblies.Select(assembly => assembly.GetType("DefinitiveWeaponVariants.Integrations.AmonyaIntegration", false))
                .FirstOrDefault(type => type is not null);
            var coordinate = bridge?.GetMethod("RunBlacklistUpdateAsync", BindingFlags.Public | BindingFlags.Static, null,
                [typeof(string), typeof(Func<CancellationToken, Task>), typeof(CancellationToken)], null);
            if (coordinate?.ReturnType != typeof(Task))
                throw new NotSupportedException("Installed DWV lacks the Amonya APBS coordination API. Update DWV to enable coordinated ammo blacklist updates.");
            await Invoke(coordinate, [blacklistPath, (Func<CancellationToken, Task>)UpdateAsync, token]);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.Warning($"APBS ammo blacklist update failed: {ex.Message} Check the APBS blacklist and Amonya ownership record before restarting. Normal Amonya item registration continues.");
        }
    }

    private static MethodInfo ResolveReload(Type? type)
    {
        var method = type?.GetMethod("ReloadConfig", BindingFlags.Public | BindingFlags.Static, null, [typeof(CancellationToken)], null);
        if (method is null || !method.ReturnType.IsGenericType || method.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
            throw new NotSupportedException("APBS ReloadConfig(CancellationToken) API is unavailable or unsupported.");
        var result = method.ReturnType.GenericTypeArguments[0];
        if (!result.IsEnum || !new[] { "Success", "Failure", "ActiveProcess" }.All(Enum.GetNames(result).Contains))
            throw new NotSupportedException("APBS reload result API is unsupported.");
        return method;
    }

    private static async Task<string> ReloadAsync(MethodInfo method, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var task = Invoke(method, [token]);
            await task.ConfigureAwait(false);
            string result = task.GetType().GetProperty("Result")?.GetValue(task)?.ToString() ?? "Unknown";
            if (result != "ActiveProcess" || attempt >= 4) return result;
            await Task.Delay(250 * (1 << attempt), token);
        }
    }

    private static Task Invoke(MethodInfo method, object[] arguments)
    {
        try
        {
            if (method.Invoke(null, arguments) is not Task task)
                throw new InvalidOperationException("APBS integration API returned an invalid task.");
            return task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
