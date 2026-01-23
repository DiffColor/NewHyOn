using System;
using System.Collections.Generic;
using System.IO;
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
            ApplyContentDetailsToPages(pages);

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
                            string checksum = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID : content.CIF_FileHash;
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
                                RemoteChecksum = checksum,
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

        private void ApplyContentDetailsToPages(List<PageInfoClass> pages)
        {
            if (pages == null || pages.Count == 0)
            {
                return;
            }

            var detailsManager = DataShop.Instance?.g_ContentDetailsManager;
            if (detailsManager == null)
            {
                return;
            }

            foreach (var page in pages)
            {
                if (page?.PIC_Elements == null)
                {
                    continue;
                }

                foreach (var element in page.PIC_Elements)
                {
                    if (element?.EIF_ContentsInfoClassList == null)
                    {
                        continue;
                    }

                    foreach (var content in element.EIF_ContentsInfoClassList)
                    {
                        if (content == null)
                        {
                            continue;
                        }

                        string fileName = ResolveContentFileName(content);
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            continue;
                        }

                        string extension = ResolveExtension(fileName, content.CIF_FileFullPath);
                        ContentDetails details = ResolveContentDetails(detailsManager, fileName, content.CIF_FileHash);
                        if (details == null)
                        {
                            continue;
                        }

                        string storageFileName = BuildStorageFileName(details.Id, extension);
                        content.CIF_FileName = storageFileName;
                        content.CIF_RelativePath = $"Contents/{storageFileName}";

                        if (!string.IsNullOrWhiteSpace(details.PartialHash))
                        {
                            content.CIF_FileHash = details.PartialHash;
                        }

                        if (details.FileSize > 0)
                        {
                            content.CIF_FileSize = details.FileSize;
                        }

                        content.CIF_FileExist = true;
                    }
                }
            }
        }

        private static string ResolveContentFileName(ContentsInfoClass content)
        {
            if (content == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(content.CIF_FileName))
            {
                return Path.GetFileName(content.CIF_FileName);
            }

            if (!string.IsNullOrWhiteSpace(content.CIF_RelativePath))
            {
                return Path.GetFileName(content.CIF_RelativePath);
            }

            if (!string.IsNullOrWhiteSpace(content.CIF_FileFullPath))
            {
                return Path.GetFileName(content.CIF_FileFullPath);
            }

            return string.Empty;
        }

        private static string ResolveExtension(string fileName, string fullPath)
        {
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(fullPath))
            {
                extension = Path.GetExtension(fullPath);
            }

            return extension ?? string.Empty;
        }

        private static ContentDetails ResolveContentDetails(ContentDetailsManager manager, string fileName, string partialHash)
        {
            if (manager == null || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            if (Guid.TryParseExact(nameWithoutExt, "N", out var _))
            {
                var byId = manager.FindById(nameWithoutExt);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(partialHash))
            {
                var byHash = manager.FindByPartialHash(partialHash);
                if (byHash != null && byHash.Count > 0)
                {
                    return byHash[0];
                }
            }

            var byName = manager.FindByFileName(fileName);
            if (byName != null && byName.Count > 0)
            {
                return byName[0];
            }

            return null;
        }

        private static string BuildStorageFileName(string guid, string extension)
        {
            string safeGuid = string.IsNullOrWhiteSpace(guid) ? Guid.NewGuid().ToString("N") : guid;
            string ext = extension ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ext) && !ext.StartsWith("."))
            {
                ext = "." + ext;
            }

            return string.IsNullOrWhiteSpace(ext) ? safeGuid : $"{safeGuid}{ext}";
        }
    }
}
