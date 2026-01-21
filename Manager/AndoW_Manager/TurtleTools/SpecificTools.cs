using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Linq;
using WPF_FontFamily = System.Windows.Media.FontFamily;
using Shell32;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace TurtleTools
{
    public class SpecificTools
    {
        #region Font
        public static Dictionary<string, List<string>> GetFontsDic()
        {
            
                Dictionary<string, List<string>> retDics = new Dictionary<string, List<string>>();
                Dictionary<string, string> fontFileInfoDics = GetFontFilesInfo();
                List<string> files = new List<string>();
                string familyName = string.Empty;

                foreach (WPF_FontFamily ff in Fonts.SystemFontFamilies)
                {
                    try
                    {
                        familyName = ff.FamilyNames.Values.ToArray<string>()[0];

                        if (fontFileInfoDics.ContainsValue(familyName))
                        {
                            foreach (KeyValuePair<string, string> kvp in fontFileInfoDics)
                            {
                                if (kvp.Value.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                                {
                                    files.Add(kvp.Key);
                                }
                            }

                            retDics.Add(familyName, files);
                            files = new List<string>();
                            continue;
                        }

                        retDics.Add(familyName, new List<string>());
                    }
                    catch (Exception exc)
                    {

                    }
                }

            return retDics;
        }

        public static string GetFontSourceFolderPath()
        {
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
        }

        public static string GetFontSourceFilePath(string filename)
        {
            return System.IO.Path.Combine(GetFontSourceFolderPath(), filename);
        }

        public static string GetFontTargetFilePath(string foldername, string filename)
        {
            Directory.CreateDirectory(foldername);
            return System.IO.Path.Combine(foldername, filename);
        }

        public static Dictionary<string, string> GetFontFilesInfo()
        {
            Dictionary<string, string> retDics = new Dictionary<string, string>();
            string[] filePathArr = Directory.GetFiles(GetFontSourceFolderPath());
            string fontName = string.Empty;

            foreach (string filePath in filePathArr)
            {
                // winform font routines (old routine)
                //PrivateFontCollection pfc = new PrivateFontCollection();
                //pfc.AddFontFile(filePath);
                //if (pfc.Families.Length < 1) continue;
                //retDics.Add(System.IO.Path.GetFileName(filePath), pfc.Families[0].Name);
                //System.Console.WriteLine(System.IO.Path.GetFileName(filePath) + " | " + pfc.Families[0].Name);

                ICollection<WPF_FontFamily> fontFamilyList = Fonts.GetFontFamilies(filePath);

                if (fontFamilyList.Count > 0)
                {
                    try
                    {
                        fontName = fontFamilyList.ToArray<FontFamily>()[0].FamilyNames.Values.ToArray<string>()[0];
                        retDics.Add(System.IO.Path.GetFileName(filePath), fontName);
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
            return retDics;
        }
        
        public static string GetExactFontFile(string targetfolder, string familyName, List<string> fontfilenames, int stylesInt)
        {
            if (string.IsNullOrEmpty(familyName) || fontfilenames.Count < 1) return string.Empty;

            string filePath = string.Empty;
            string title = string.Empty;
            int FONT_TITLE_IDX = 21;

            foreach (string fontfilename in fontfilenames)
            {
                filePath = GetFontSourceFilePath(fontfilename);

                if (System.IO.File.Exists(filePath))
                {
                    string targetPath = GetFontTargetFilePath(targetfolder, fontfilename);

                    try
                    {
                        FileTools.CopyFile(filePath, targetPath);

                        Shell Sh = new Shell();
                        Folder F = Sh.NameSpace(targetfolder);
                        FolderItem FI = F.ParseName(fontfilename);

                        title = F.GetDetailsOf(FI, FONT_TITLE_IDX);
                    }
                    catch (Exception ex)
                    {
                    }

                    string retStr = title.Replace(familyName, "");

                    if (string.IsNullOrEmpty(retStr))
                    {
                        if (stylesInt == 0)
                        {
                            return fontfilename;
                        }
                    }

                    retStr.Trim();

                    string[] styles = retStr.Split(' ');

                    int styleSum = 0;

                    foreach (string style in styles)
                    {
                        switch (style)
                        {
                            case "Bold":
                                styleSum += (int)System.Drawing.FontStyle.Bold;
                                break;

                            case "Italic":
                                styleSum += (int)System.Drawing.FontStyle.Italic;
                                break;

                            case "Underline":
                                styleSum += (int)System.Drawing.FontStyle.Underline;
                                break;

                            case "Strikeout":
                                styleSum += (int)System.Drawing.FontStyle.Strikeout;
                                break;

                            case "Regular":
                            default:
                                break;
                        }
                    }

                    if (styleSum == stylesInt) return fontfilename;
                }
            }

            return fontfilenames.FirstOrDefault();
        }

        public static void CopyFontIfNeed(string sourceName, string targetfolder, string targetName)
        {

            List<string> targetFiles = Directory.GetFiles(targetfolder).ToList();
            if (targetFiles.Exists(file => file.Equals(targetName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            } 
            
            string sourcepath = GetFontSourceFilePath(sourceName);
            FileTools.CopyFile(sourcepath, GetFontTargetFilePath(targetfolder, targetName));
        }

        public static void LineReplacer(string filePath, string oldStartStr, string newStr)
        {
            File.WriteAllLines(filePath,
                File.ReadAllLines(filePath).Select(
                                    x =>
                                    {
                                        x = x.TrimStart(new char[] { ' ', '\t' });

                                        if (x.StartsWith(oldStartStr))
                                            return newStr;

                                        return x;
                                    }));
        }

        [DllImport("gdi32.dll", EntryPoint = "AddFontResourceW", SetLastError = true)]
        public static extern int AddFontResource([In][MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("gdi32.dll", EntryPoint = "RemoveFontResourceW", SetLastError = true)]
        public static extern int RemoveFontResource([In][MarshalAs(UnmanagedType.LPWStr)] string lpFileName);
        #endregion

        #region HW Acceleration
        public static void DisableHWAccelerationByReg(bool disable)
        {
            string subKeys = "Microsoft\\Avalon.Graphics";
            string valueKey = "DisableHWAcceleration";
            int value = 0;

            if (disable)
                value = 1;

            SecurityTools.WriteRegKey(subKeys, valueKey, unchecked(value), RegistryValueKind.DWord);
        }

        public static void DisableWindowHWAcceleration(Window window, bool disable)
        {
            HwndSource hwndSource = PresentationSource.FromVisual(window) as HwndSource;
            HwndTarget hwndTarget = hwndSource.CompositionTarget;

            if (disable)                
                hwndTarget.RenderMode = RenderMode.SoftwareOnly;
            else
                hwndTarget.RenderMode = RenderMode.Default;
        }

        /*.NET 4.0 */
        //public static void DisableProcessHWAcceleration(bool disable)
        //{
        //    if (disable)
        //        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        //    else
        //        RenderOptions.ProcessRenderMode = RenderMode.Default;
        //}
        #endregion

    }
}