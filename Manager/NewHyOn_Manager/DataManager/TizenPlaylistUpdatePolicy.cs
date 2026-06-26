using System;
using System.Collections.Generic;
using System.Linq;

namespace AndoW_Manager
{
    public sealed class TizenPlaylistBlockResult
    {
        public PlayerInfoClass Player { get; set; }
        public string PlaylistName { get; set; } = string.Empty;
        public List<string> InvalidPageNames { get; set; } = new List<string>();
    }

    public static class TizenPlaylistUpdatePolicy
    {
        public static bool IsTizenPlayer(PlayerInfoClass player)
        {
            return player != null
                && string.IsNullOrWhiteSpace(player.PIF_OSName) == false
                && player.PIF_OSName.IndexOf("tizen", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsSingleScreenPage(PageInfoClass page)
        {
            if (page == null)
            {
                return false;
            }

            int rows = page.PIC_Rows > 0 ? page.PIC_Rows : 1;
            int columns = page.PIC_Columns > 0 ? page.PIC_Columns : 1;
            if (rows == 1 && columns == 1)
            {
                return true;
            }

            return HasExactlyOneCanvasObject(page);
        }

        public static bool IsSingleScreenPlaylist(string playlistName)
        {
            List<string> invalidPages;
            return TryValidateSingleScreenPlaylist(playlistName, out invalidPages);
        }

        public static bool TryValidatePlayerPlaylist(PlayerInfoClass player, string playlistName, out TizenPlaylistBlockResult blockResult)
        {
            blockResult = null;
            if (IsTizenPlayer(player) == false)
            {
                return true;
            }

            List<string> invalidPages;
            if (TryValidateSingleScreenPlaylist(playlistName, out invalidPages))
            {
                return true;
            }

            if (invalidPages.Count < 1)
            {
                return true;
            }

            blockResult = new TizenPlaylistBlockResult
            {
                Player = player,
                PlaylistName = playlistName ?? string.Empty,
                InvalidPageNames = invalidPages
            };
            return false;
        }

        public static bool TryValidatePlayerSchedule(PlayerInfoClass player, out TizenPlaylistBlockResult blockResult)
        {
            blockResult = null;
            if (IsTizenPlayer(player) == false || player == null || string.IsNullOrWhiteSpace(player.PIF_PlayerName))
            {
                return true;
            }

            SpecialScheduleInfoManager scheduleManager = new SpecialScheduleInfoManager();
            scheduleManager.LoadSchedulesForPlayer(player.PIF_PlayerName);

            foreach (SpecialScheduleInfoClass schedule in scheduleManager.g_SpecialScheduleInfoClassList ?? new List<SpecialScheduleInfoClass>())
            {
                if (schedule == null || string.IsNullOrWhiteSpace(schedule.PageListName))
                {
                    continue;
                }

                List<string> invalidPages;
                if (TryValidateSingleScreenPlaylist(schedule.PageListName, out invalidPages) == false && invalidPages.Count > 0)
                {
                    blockResult = new TizenPlaylistBlockResult
                    {
                        Player = player,
                        PlaylistName = schedule.PageListName,
                        InvalidPageNames = invalidPages
                    };
                    return false;
                }
            }

            return true;
        }

        public static List<TizenPlaylistBlockResult> FindBlockedPlaylistUpdates(IEnumerable<PlayerInfoClass> players, string playlistName)
        {
            List<TizenPlaylistBlockResult> blocked = new List<TizenPlaylistBlockResult>();

            foreach (PlayerInfoClass player in players ?? new List<PlayerInfoClass>())
            {
                TizenPlaylistBlockResult blockResult;
                if (TryValidatePlayerPlaylist(player, playlistName, out blockResult) == false && blockResult != null)
                {
                    blocked.Add(blockResult);
                }
            }

            return blocked;
        }

        public static string BuildBlockedMessage(IEnumerable<TizenPlaylistBlockResult> blockedResults)
        {
            List<TizenPlaylistBlockResult> blocks = (blockedResults ?? new List<TizenPlaylistBlockResult>())
                .Where(x => x != null && x.Player != null)
                .ToList();

            if (blocks.Count < 1)
            {
                return string.Empty;
            }

            List<string> names = blocks
                .Select(x => string.IsNullOrWhiteSpace(x.Player.PIF_PlayerName) ? "이름 없음" : x.Player.PIF_PlayerName)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            List<string> pageNames = blocks
                .SelectMany(x => x.InvalidPageNames ?? new List<string>())
                .Where(x => string.IsNullOrWhiteSpace(x) == false)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            string playerText = string.Join(", ", names);
            string pageText = pageNames.Count > 0
                ? Environment.NewLine + "대상 페이지: " + string.Join(", ", pageNames)
                : string.Empty;

            return string.Format(
                "Tizen 플레이어 [{0}]에 단일화면이 아닌 페이지가 포함되어 업데이트가 취소되었습니다.{1}",
                playerText,
                pageText);
        }

        private static bool TryValidateSingleScreenPlaylist(string playlistName, out List<string> invalidPageNames)
        {
            invalidPageNames = new List<string>();
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                invalidPageNames.Add("플레이리스트 없음");
                return false;
            }

            PageListInfoClass pageList = DataShop.Instance.g_PageListInfoManager.GetPageListByName(playlistName);
            if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count < 1)
            {
                invalidPageNames.Add(playlistName);
                return false;
            }

            DataShop.Instance.g_PageInfoManager.LoadPagesForList(playlistName);
            List<PageInfoClass> pages = DataShop.Instance.g_PageInfoManager.g_PageInfoClassList?.ToList()
                ?? new List<PageInfoClass>();

            if (pages.Count < 1 || pages.Count != pageList.PLI_Pages.Count)
            {
                invalidPageNames.Add(playlistName);
                return false;
            }

            foreach (PageInfoClass page in pages)
            {
                if (IsSingleScreenPage(page))
                {
                    continue;
                }

                invalidPageNames.Add(string.IsNullOrWhiteSpace(page.PIC_PageName) ? playlistName : page.PIC_PageName);
            }

            return invalidPageNames.Count == 0;
        }

        private static bool HasExactlyOneCanvasObject(PageInfoClass page)
        {
            if (page == null || page.PIC_Elements == null)
            {
                return false;
            }

            return page.PIC_Elements.Count(IsValidCanvasObject) == 1;
        }

        private static bool IsValidCanvasObject(ElementInfoClass element)
        {
            if (element == null)
            {
                return false;
            }

            return (string.IsNullOrWhiteSpace(element.EIF_Type) == false
                || string.IsNullOrWhiteSpace(element.EIF_Name) == false);
        }
    }
}
