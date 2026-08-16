using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RecipeHelper.Utility
{
    // Backs the redirect-after-post pattern used by Import/Dinner/Cart controllers:
    // a POST computes a result, stashes it here, and redirects to a GET action that
    // reads it back so the result page has a real URL that's safe to reload (needed
    // for the PWA's "tap to refresh" update banner, which reloads via
    // window.location.reload() rather than resubmitting the original POST).
    //
    // A plain Session.SetString/GetString pair (the original implementation) has no
    // expiry, so once a value is written it gets replayed on every future visit to
    // that GET action -- including ones with no relation to the POST that wrote it --
    // until something else happens to overwrite the same key. In practice that showed
    // up as an import error getting "stuck": one failed photo import cached the error,
    // and every later visit to Import Recipe kept re-showing that same stale failure
    // instead of a fresh form. MaxAge bounds how long a cached result is considered
    // still "just redirected here" rather than "stale leftover" -- long enough to
    // survive an update-banner reload, short enough to self-heal without ever needing
    // an explicit clear.
    public static class PendingResultCache
    {
        private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(10);

        private sealed class Envelope<T>
        {
            public T? Value { get; set; }
            public DateTime CreatedUtc { get; set; }
        }

        public static void Set<T>(ISession session, string key, T value)
        {
            var envelope = new Envelope<T> { Value = value, CreatedUtc = DateTime.UtcNow };
            session.SetString(key, JsonSerializer.Serialize(envelope));
        }

        // A fresh entry is deliberately left in session (not consumed on read) so a
        // reload of the same GET -- the PWA update banner's whole reason for
        // existing -- still finds it. Only stale or corrupt entries are removed;
        // otherwise a bad cached payload (e.g. a VM shape that changed across a
        // deploy) would throw the same JsonException on every future visit forever.
        public static bool TryGet<T>(ISession session, string key, out T? value, TimeSpan? maxAge = null)
        {
            value = default;
            var json = session.GetString(key);
            if (string.IsNullOrEmpty(json)) return false;

            Envelope<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<Envelope<T>>(json);
            }
            catch (JsonException)
            {
                session.Remove(key);
                return false;
            }

            if (envelope is null || DateTime.UtcNow - envelope.CreatedUtc > (maxAge ?? DefaultMaxAge))
            {
                session.Remove(key);
                return false;
            }

            value = envelope.Value;
            return true;
        }
    }
}
