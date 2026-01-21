using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;

namespace TurtleTools.LiteDbSample
{
    internal static class LiteDbSampleContext
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static string _connectionString;
        private static readonly HashSet<string> EnsuredCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                string dbPath = Path.Combine(FNDTools.GetDataRootDirPath(), "LiteDbSample.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                _connectionString = $"Filename={dbPath};Connection=shared";

                _initialized = true;
            }
        }

        public static void EnsureCollection<T>(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name is required.", nameof(collectionName));
            }

            EnsureInitialized();

            lock (SyncRoot)
            {
                if (!EnsuredCollections.Add($"{typeof(T).FullName}:{collectionName}"))
                {
                    return;
                }

                using (var db = new LiteDatabase(_connectionString))
                {
                    db.GetCollection<T>(collectionName);
                }
            }
        }

        public static T Execute<T>(Func<LiteDatabase, T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            EnsureInitialized();

            using (var db = new LiteDatabase(_connectionString))
            {
                return action(db);
            }
        }

        public static void Execute(Action<LiteDatabase> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            EnsureInitialized();

            using (var db = new LiteDatabase(_connectionString))
            {
                action(db);
            }
        }
    }
}
