using System;
using System.Collections.Generic;
using System.Linq;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;

namespace TurtleTools
{
    public static class RethinkDbContext
    {
        private static readonly object SyncRoot = new object();
        private static readonly RethinkDB R = RethinkDB.R;
        private static string _host = "127.0.0.1";
        private static int _port = 28015;
        private static string _username = "admin";
        private static string _password = "turtle04!9";
        private static readonly HashSet<string> InitializedDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> InitializedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Configure(string host, int port, string username = null, string password = null)
        {
            lock (SyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(host))
                {
                    _host = host;
                }

                if (port > 0)
                {
                    _port = port;
                }

                if (!string.IsNullOrWhiteSpace(username))
                {
                    _username = username;
                }

                if (!string.IsNullOrWhiteSpace(password))
                {
                    _password = password;
                }

                InitializedDatabases.Clear();
                InitializedTables.Clear();
            }
        }

        private static Connection CreateConnection()
        {
            return R.Connection()
                .Hostname(_host)
                .Port(_port)
                .User(_username, _password)
                .Connect();
        }

        public static void EnsureTable(string databaseName, string tableName, string primaryKey)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("Database name is required.", nameof(databaseName));
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name is required.", nameof(tableName));
            }

            string tableKey = $"{databaseName}.{tableName}";
            lock (SyncRoot)
            {
                EnsureDatabase(databaseName);

                if (InitializedTables.Contains(tableKey))
                {
                    return;
                }

                using (var conn = CreateConnection())
                {
                    var existingTables = R.Db(databaseName).TableList().RunAtom<List<string>>(conn) ?? new List<string>();
                    if (!existingTables.Contains(tableName))
                    {
                        try
                        {
                            var tableCreate = R.Db(databaseName).TableCreate(tableName);
                            if (!string.IsNullOrWhiteSpace(primaryKey) &&
                                !primaryKey.Equals("id", StringComparison.OrdinalIgnoreCase))
                            {
                                tableCreate = tableCreate.OptArg("primary_key", primaryKey);
                            }

                            tableCreate.Run(conn);
                        }
                        catch (ReqlRuntimeError ex) when (ex.Message != null && ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // 다른 프로세스가 이미 생성함.
                        }
                    }
                }

                InitializedTables.Add(tableKey);
            }
        }

        private static void EnsureDatabase(string databaseName)
        {
            if (InitializedDatabases.Contains(databaseName))
            {
                return;
            }

            using (var conn = CreateConnection())
            {
                var databases = R.DbList().RunAtom<List<string>>(conn) ?? new List<string>();
                if (!databases.Contains(databaseName))
                {
                    try
                    {
                        R.DbCreate(databaseName).Run(conn);
                    }
                    catch (ReqlRuntimeError ex) when (ex.Message != null && ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // 이미 생성됨.
                    }
                }
            }

            InitializedDatabases.Add(databaseName);
        }

        public static Table Table(string databaseName, string tableName)
        {
            return R.Db(databaseName).Table(tableName);
        }

        public static List<T> RunList<T>(ReqlExpr expr)
        {
            try
            {
                using (var conn = CreateConnection())
                using (var cursor = expr.RunCursor<T>(conn))
                {
                    return cursor?.ToList() ?? new List<T>();
                }
            }
            catch (ReqlNonExistenceError)
            {
                return new List<T>();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return new List<T>();
            }
        }

        public static T RunSingleOrDefault<T>(ReqlExpr expr)
        {
            try
            {
                using (var conn = CreateConnection())
                {
                    return expr.RunAtom<T>(conn);
                }
            }
            catch (ReqlNonExistenceError)
            {
                return default;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return default;
            }
        }

        public static void Run(ReqlExpr expr)
        {
            try
            {
                using (var conn = CreateConnection())
                {
                    expr.Run(conn);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }
    }
}
