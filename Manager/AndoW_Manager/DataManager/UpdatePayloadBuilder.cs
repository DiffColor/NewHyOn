using System;
using System.Collections.Generic;
using System.Linq;
using AndoW.Shared;

namespace AndoW_Manager
{
    public sealed class UpdatePayloadBuilder
    {
        public UpdatePayload BuildPayload(PlayerInfoClass player)
        {
            if (player == null)
            {
                return null;
            }

            var listManager = DataShop.Instance.g_PageListInfoManager;
            var pageManager = DataShop.Instance.g_PageInfoManager;

            listManager.LoadDataFromDatabase();
            var pageList = listManager.GetPageListByName(player.PIF_CurrentPlayList);
            if (pageList == null)
            {
                return null;
            }

            pageManager.LoadPagesForList(pageList.PLI_PageListName);
            var pages = pageManager.g_PageInfoClassList?.ToList() ?? new List<PageInfoClass>();
            var contract = BuildContractPayload(player, pageList, pages);

            return new UpdatePayload
            {
                PageList = pageList,
                Pages = pages,
                Contract = contract
            };
        }

        public string BuildPayloadBase64(PlayerInfoClass player)
        {
            var payload = BuildPayload(player);
            return UpdatePayloadCodec.Encode(payload);
        }

        public ContractPlaylistPayload BuildContractPayload(PlayerInfoClass player, PageListInfoClass pageList, List<PageInfoClass> pages)
        {
            var payload = new ContractPlaylistPayload
            {
                PlayerId = player?.PIF_GUID ?? string.Empty,
                PlayerName = player?.PIF_PlayerName ?? string.Empty,
                PlayerLandscape = player?.PIF_IsLandScape ?? false,
                PlaylistId = pageList?.PLI_PageListName ?? string.Empty,
                PlaylistName = pageList?.PLI_PageListName ?? string.Empty,
                Pages = new List<ContractPagePayload>()
            };

            var orderedIds = pageList?.PLI_Pages ?? new List<string>();

            foreach (var page in pages ?? new List<PageInfoClass>())
            {
                var pageEntry = new ContractPagePayload
                {
                    PageId = page.PIC_GUID,
                    PageName = page.PIC_PageName,
                    OrderIndex = orderedIds.IndexOf(page.PIC_GUID),
                    PlayHour = page.PIC_PlaytimeHour,
                    PlayMinute = page.PIC_PlaytimeMinute,
                    PlaySecond = page.PIC_PlaytimeSecond,
                    Volume = page.PIC_Volume,
                    Landscape = page.PIC_IsLandscape,
                    Elements = new List<ContractElementPayload>()
                };
                if (pageEntry.OrderIndex < 0)
                {
                    pageEntry.OrderIndex = payload.Pages.Count;
                }

                if (page.PIC_Elements != null)
                {
                    foreach (var element in page.PIC_Elements)
                    {
                        var elementId = $"{page.PIC_GUID}_{element.EIF_Name}";
                        var elementEntry = new ContractElementPayload
                        {
                            ElementId = elementId,
                            PageId = page.PIC_GUID,
                            Name = element.EIF_Name,
                            Type = element.EIF_Type,
                            Width = element.EIF_Width,
                            Height = element.EIF_Height,
                            PosLeft = element.EIF_PosLeft,
                            PosTop = element.EIF_PosTop,
                            ZIndex = element.EIF_ZIndex,
                            Contents = new List<ContractContentPayload>()
                        };

                        var contents = element.EIF_ContentsInfoClassList ?? new List<ContentsInfoClass>();
                        for (int idx = 0; idx < contents.Count; idx++)
                        {
                            var content = contents[idx];
                            var uid = $"{elementId}_{idx}";
                            var contentEntry = new ContractContentPayload
                            {
                                Uid = uid,
                                ElementId = elementId,
                                FileName = content.CIF_FileName,
                                FileFullPath = content.CIF_FileFullPath,
                                ContentType = content.CIF_ContentType,
                                PlayMinute = content.CIF_PlayMinute,
                                PlaySecond = content.CIF_PlaySec,
                                Valid = content.CIF_ValidTime,
                                ScrollSpeedSec = content.CIF_ScrollTextSpeedSec,
                                RemoteChecksum = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID : content.CIF_FileHash,
                                FileSize = content.CIF_FileSize,
                                FileExist = content.CIF_FileExist
                            };
                            elementEntry.Contents.Add(contentEntry);
                        }
                        pageEntry.Elements.Add(elementEntry);
                    }
                }
                payload.Pages.Add(pageEntry);
            }

            payload.Pages = payload.Pages.OrderBy(p => p.OrderIndex).ToList();
            return payload;
        }
    }
}
