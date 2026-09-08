#nullable enable
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.AutoCollections
{
    // ================================================================
    // SERVER COMPATIBILITY HELPERS
    // ================================================================
    // This used to late-bind IUserManager.Users / GetUsers() through reflection,
    // because 10.11.9 renamed the member mid-line and one binary had to serve every
    // 10.11 server. That shim is gone: the plugin now targets net10.0 / Jellyfin 12,
    // which the .NET 9 based 10.11 servers cannot load at all, so the old shape is
    // unreachable. Jellyfin 12 exposes GetUsers(), so call it directly - a missing
    // member is now a load-time error instead of a silent empty user list.
    internal static class JellyfinCompat
    {
        /// <summary>
        /// Returns every user known to the server.
        /// </summary>
        public static IReadOnlyList<User> GetUsers(IUserManager userManager)
        {
            return userManager.GetUsers().ToList();
        }
    }
}
