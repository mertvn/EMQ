using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EMQ.Client;

public static class AutocompleteDataLoader
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_autocompleteLoadLocks = new();

    public static T[] GetAutocompleteData<T>(string key)
    {
        return ClientState.AutocompleteData.TryGetValue(key, out var data) ? (T[])data : Array.Empty<T>();
    }

    public static async Task EnsureAutocompleteDataAsync<T>(string key, Func<Task<T[]?>> load)
    {
        var semaphore = s_autocompleteLoadLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            if (!ClientState.AutocompleteData.ContainsKey(key))
            {
                var data = await load();
                if (data != null)
                {
                    ClientState.AutocompleteData[key] = data;
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}
