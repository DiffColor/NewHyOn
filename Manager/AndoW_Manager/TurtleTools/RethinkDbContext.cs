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
        private static Connection _connection;
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

                if (_connection != null)
                {
                    _connection.Close(false);
                    _connection.Dispose();
                    _connection = null;
                    InitializedDatabases.Clear();
                    InitializedTables.Clear();
                }
            }
        }

        private static Connection GetConnection()
        {
            if (_connection != null)
            {
                return _connection;
            }

            lock (SyncRoot)
            {
                if (_connection == null)
                {
                    _connection = R.Connection()
                        .Hostname(_host)
                        .Port(_port)
                        .User(_username, _password)
                        .Connect();
                }

                return _connection;
            }
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

                var conn = GetConnection();
                var existingTables = R.Db(databaseName).TableList().RunAtom<List<string>>(conn) ?? new List<string>();
                if (!existingTables.Contains(tableName))
                {
                    var tableCreate = R.Db(databaseName).TableCreate(tableName);

                    if (!string.IsNullOrWhiteSpace(primaryKey) && !primaryKey.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        tableCreate = tableCreate.OptArg("primary_key", primaryKey);
                    }

                    tableCreate.Run(conn);
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

            var conn = GetConnection();
            var databases = R.DbList().RunAtom<List<string>>(conn) ?? new List<string>();
            if (!databases.Contains(databaseName))
            {
                R.DbCreate(databaseName).Run(conn);
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
                using (var cursor = expr.RunCursor<T>(GetConnection()))
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
                return expr.RunAtom<T>(GetConnection());
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
                expr.Run(GetConnection());
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public static Connection GetRawConnection()
        {
            return GetConnection();
        }
    }
}
