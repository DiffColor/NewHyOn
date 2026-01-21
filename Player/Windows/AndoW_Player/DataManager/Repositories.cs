using AndoW.LiteDb;
using AndoW.Shared;
using HyOnPlayer.DataManager;
using System;
using System.Collections.Generic;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace HyOnPlayer
{
    public sealed class PlayerInfoRepository : LiteDbRepository<PlayerInfoClass>, IDisposable
    {
        public PlayerInfoRepository() : base("PlayerInfoManager", "Id") { }

        public void Dispose() { }
    }

    public sealed class TTPlayerRepository : LiteDbRepository<TTPlayerInfoClass>, IDisposable
    {
        public TTPlayerRepository() : base("TTPlayerInfoManager", "id") { }

        public void Dispose() { }
    }

    public sealed class PageRepository : LiteDbRepository<PageInfoClass>, IDisposable
    {
        public PageRepository() : base("PageInfoManager", "PIC_GUID") { }

        public void Dispose() { }
    }

    public sealed class PageListRepository : LiteDbRepository<PageListInfoClass>, IDisposable
    {
        public PageListRepository() : base("PageListInfoManager", "id") { }

        public void Dispose() { }
    }

    public sealed class TextRepository : LiteDbRepository<TextInfoClass>, IDisposable
    {
        // TextInfoClass PK는 CIF_Id
        public TextRepository() : base("TextInfoManager", "CIF_Id") { }

        public void Dispose() { }
    }

    public sealed class UpdateQueueRepository : LiteDbRepository<UpdateQueue>, IDisposable
    {
        public UpdateQueueRepository() : base("UpdateQueue", "id") { }

        public void Dispose() { }
    }

    public sealed class CommandHistoryRepository : LiteDbRepository<CommandHistory>, IDisposable
    {
        public CommandHistoryRepository() : base("CommandHistory", "id") { }

        public void Dispose() { }
    }

    public sealed class WeeklyScheduleRepository : LiteDbRepository<SharedWeeklyPlayScheduleInfo>, IDisposable
    {
        public WeeklyScheduleRepository() : base("WeeklyInfoManagerClass", "id") { }

        public void Dispose() { }
    }

    public sealed class LocalPlayerSettingsRepository : LiteDbRepository<LocalPlayerSettings>, IDisposable
    {
        public LocalPlayerSettingsRepository() : base("LocalPlayerSettings", "id") { }

        public void Dispose() { }
    }

    public sealed class SpecialScheduleCache
    {
        public string Id { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public string UpdatedAt { get; set; } = string.Empty;

        public List<SpecialSchedulePayload> Schedules { get; set; } = new List<SpecialSchedulePayload>();

        public List<SchedulePlaylistPayload> Playlists { get; set; } = new List<SchedulePlaylistPayload>();
    }

    public sealed class SpecialScheduleCacheRepository : LiteDbRepository<SpecialScheduleCache>, IDisposable
    {
        public SpecialScheduleCacheRepository() : base("SpecialScheduleCache", "Id") { }

        public void Dispose() { }
    }
}
