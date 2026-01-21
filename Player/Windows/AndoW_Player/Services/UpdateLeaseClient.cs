using System;
using AndoW.Shared;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using TurtleTools;

namespace HyOnPlayer
{
    internal sealed class UpdateLeaseClient : IDisposable
    {
        private const string DatabaseName = "AndoW";
        private const string TableName = "UpdateLease";

        private static readonly RethinkDB R = RethinkDB.R;
        private readonly object syncRoot = new object();
        private readonly string host;
        private Connection connection;

        public UpdateLeaseClient(string managerHost)
        {
            host = string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost;
        }

        public void Dispose()
        {
            ResetConnection();
        }

        public UpdateLeaseEntry TryAcquire(string playerId, string queueId, int maxConcurrent, int ttlSeconds)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            int limit = Math.Max(1, maxConcurrent);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return null;
                }

                int active = R.Db(DatabaseName).Table(TableName).Count().RunAtom<int>(conn);
                if (active >= limit)
                {
                    return null;
                }

                var lease = new UpdateLeaseEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    PlayerId = playerId,
                    QueueId = queueId ?? string.Empty,
                    LastRenewAt = now
                };

                R.Db(DatabaseName)
                    .Table(TableName)
                    .Insert(lease)
                    .Run(conn);

                return lease;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return null;
            }
        }

        public bool Renew(string leaseId, int ttlSeconds)
        {
            if (string.IsNullOrWhiteSpace(leaseId))
            {
                return false;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return false;
                }

                var result = R.Db(DatabaseName)
                    .Table(TableName)
                    .Get(leaseId)
                    .Update(new
                    {
                        LastRenewAt = now
                    })
                    .Run(conn);

                return result != null;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return false;
            }
        }

        public void Release(string leaseId)
        {
            if (string.IsNullOrWhiteSpace(leaseId))
            {
                return;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                R.Db(DatabaseName)
                    .Table(TableName)
                    .Get(leaseId)
                    .Delete()
                    .Run(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        public void ReleaseByPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                string lowered = playerId.ToLowerInvariant();
                R.Db(DatabaseName)
                    .Table(TableName)
                    .Filter(row => row["PlayerId"].Downcase().Eq(lowered))
                    .Delete()
                    .Run(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        public void ReleaseStaleByLastRenew(int seconds)
        {
            int maxAgeSec = seconds <= 0 ? 60 : seconds;
            string threshold = DateTime.Now.AddSeconds(-maxAgeSec).ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                R.Db(DatabaseName)
                    .Table(TableName)
                    .Filter(row => row["LastRenewAt"].Lt(threshold))
                    .Delete()
                    .Run(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private Connection GetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(host)
                    .Port(28015)
                    .User("admin", "turtle04!9")
                    .Connect();

                return connection;
            }
        }

        private void ResetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null)
                {
                    try
                    {
                        connection.Close(false);
                        connection.Dispose();
                    }
                    catch
                    {
                    }
                    connection = null;
                }
            }
        }
    }
}
