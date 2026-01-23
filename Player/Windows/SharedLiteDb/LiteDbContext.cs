using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using AndoW.Shared;

namespace AndoW.LiteDb
{
    /// <summary>
    /// 단일 파일 LiteDB 컨텍스트.
    /// 앱 기준 경로(local.db)에 DB 파일을 생성하고 공유 커넥션 문자열을 유지한다.
    /// </summary>
    public static class LiteDbContext
    {
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> EnsuredCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _connectionString;
        private static bool _initialized;
        private static bool _mapperConfigured;

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

                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? AppDomain.CurrentDomain.BaseDirectory);
                _connectionString = $"Filename={dbPath};Connection=shared;Password=turtle04!9";
                ConfigureMapper();
                _initialized = true;
            }
        }

        private static void ConfigureMapper()
        {
            if (_mapperConfigured)
            {
                return;
            }

            BsonMapper.Global.RegisterType<string>(
                s => new BsonValue(s),
                v =>
                {
                    if (v == null || v.IsNull) return string.Empty;
                    if (v.IsObjectId) return v.AsObjectId.ToString();
                    if (v.IsGuid) return v.AsGuid.ToString();
                    return v.AsString;
                });

            BsonMapper.Global.Entity<PlayerInfoClass>().Id(x => x.Id, false);
            BsonMapper.Global.Entity<WeeklyPlayScheduleInfo>().Id(x => x.Id, false);

            _mapperConfigured = true;
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
                string key = $"{typeof(T).FullName}:{collectionName}";
                if (!EnsuredCollections.Add(key))
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
