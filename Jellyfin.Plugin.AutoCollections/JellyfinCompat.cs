#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.AutoCollections
{
    // ================================================================
    // SERVER COMPATIBILITY HELPERS
    // ================================================================
    // Shims for Jellyfin APIs whose shape changed within the 10.11.x line.
    // Binding these late keeps a single plugin binary working across every
    // 10.11 server instead of hard-failing on the ones that moved a member.
    internal static class JellyfinCompat
    {
        private static Func<IUserManager, IEnumerable<User>>? _usersAccessor;
        private static bool _usersAccessorResolved;

        /// <summary>
        /// Returns every user known to the server.
        /// 10.11.0-10.11.8 expose <c>IUserManager.Users</c>; 10.11.9 replaced it with
        /// <c>GetUsers()</c>, which is a binary-breaking change for plugins compiled
        /// against the older interface.
        /// </summary>
        /// <returns>The users, or an empty list when neither member is available.</returns>
        public static IReadOnlyList<User> GetUsers(IUserManager userManager)
        {
            var accessor = ResolveUsersAccessor();
            if (accessor == null)
            {
                // Neither shape is present: a Jellyfin this plugin does not know about.
                return Array.Empty<User>();
            }

            return accessor(userManager).ToList();
        }

        private static Func<IUserManager, IEnumerable<User>>? ResolveUsersAccessor()
        {
            if (_usersAccessorResolved)
            {
                return _usersAccessor;
            }

            // 10.11.9+: IEnumerable<User> GetUsers()
            var getUsers = typeof(IUserManager).GetMethod("GetUsers", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (getUsers != null && typeof(IEnumerable<User>).IsAssignableFrom(getUsers.ReturnType))
            {
                _usersAccessor = manager => (IEnumerable<User>)getUsers.Invoke(manager, null)!;
            }
            else
            {
                // 10.11.0-10.11.8: IEnumerable<User> Users { get; }
                var users = typeof(IUserManager).GetProperty("Users", BindingFlags.Public | BindingFlags.Instance);
                if (users != null && typeof(IEnumerable<User>).IsAssignableFrom(users.PropertyType))
                {
                    _usersAccessor = manager => (IEnumerable<User>)users.GetValue(manager)!;
                }
            }

            _usersAccessorResolved = true;
            return _usersAccessor;
        }
    }
}
