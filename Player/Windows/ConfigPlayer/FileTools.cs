using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace TurtleTools
{
    public class FileTools
    {

        public static void CopyDirectory(string sdpath, string tdpath, bool recursive = true)
        {
            try
            {
                if (sdpath.Equals(tdpath, StringComparison.CurrentCultureIgnoreCase))
                    return;

                DirectoryInfo dir = new DirectoryInfo(sdpath);
                DirectoryInfo[] dirs = dir.GetDirectories();

                if (!dir.Exists)
                {
                    return;
                }

                if (!Directory.Exists(tdpath))
                {
                    Directory.CreateDirectory(tdpath);
                }

                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    string temppath = Path.Combine(tdpath, file.Name);

                    CopyFile(file.FullName, temppath);
                }

                if (recursive)
                {
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        string temppath = Path.Combine(tdpath, subdir.Name);
                        CopyDirectory(subdir.FullName, temppath, recursive);
                    }
                }
            }
            catch (Exception e) { }
        }

        public static bool CopyFile(string sfpath, string tfpath, bool overwrite=true, bool preservetime=true)
        {
            long srcFilesize = 0;
            long fileSize = 0;

            try
            {

                if (sfpath.Equals(tfpath, StringComparison.CurrentCultureIgnoreCase))
                    return true;

                FileInfo sfinfo = new FileInfo(sfpath);
                srcFilesize = sfinfo.Length;

                bool succeed = false;

                FileStream rs = null;
                FileStream ws = null;

                rs = File.OpenRead(sfpath);

                if (File.Exists(tfpath))
                {
                    if (overwrite)
                        DeleteFile(tfpath);
                    else
                        return false;
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tfpath));
                }

                ws = new FileStream(tfpath, FileMode.Create);

                byte[] b = null;

                if (srcFilesize < 4096 * 20)
                    b = new byte[srcFilesize];
                else
                    b = new byte[4096 * 20];

                int count = 0;

                do
                {
                    count = rs.Read(b, 0, b.Length);

                    if (count > 0)
                    {
                        ws.Write(b, 0, count);
                        fileSize += count;
                    }
                    else
                    {
                        if (fileSize >= srcFilesize)
                        {
                            succeed = true;
                        }
                        else
                        {
                            succeed = false;
                        }
                    }
                } while (count > 0);

                rs.Close();
                ws.Close();

                if (preservetime)
                {
                    FileInfo tfinfo = new FileInfo(tfpath);
                    tfinfo.CreationTime = sfinfo.CreationTime;
                    tfinfo.LastWriteTime = sfinfo.LastWriteTime;
                    tfinfo.LastAccessTime = sfinfo.LastAccessTime;
                }

                return succeed;
            }
            catch (IOException exc)
            {
                return false;
            }
        }

        public static void MoveDirectory(string sdpath, string tfpath, bool overwrite = true, bool recursive = true)
        {
            if (sdpath.Equals(tfpath, StringComparison.CurrentCultureIgnoreCase))
                return;

            foreach (var file in Directory.GetFiles(sdpath, "*.*", recursive ? SearchOption.AllDirectories:SearchOption.TopDirectoryOnly))
            {
                string targetFile = Path.Combine(tfpath, file.Replace(sdpath, "").TrimStart('\\'));

                FileTools.CopyFile(file, targetFile, overwrite);
            }

            DeleteDirectory(sdpath);
        }

        public static bool DeleteDirectory(string dirpath, bool recursive = true)
        {
            try
            {
                foreach (string path in Directory.GetFiles(dirpath, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                {
                    if (IsDirectory(path))
                        continue;

                    DeleteFile(path);
                }

                DirectoryInfo dirinfo = new DirectoryInfo(dirpath);
                dirinfo.Delete(true);
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        public static bool IsFileLocked(FileInfo finfo)
        {
            FileStream stream = null;

            try
            {
                stream = finfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null) stream.Close();
            }
            return false;
        }

        public static void MoveFile(string sfpath, string tfpath)
        {
            try
            {
                if (sfpath.Equals(tfpath, StringComparison.CurrentCultureIgnoreCase))
                    return;

                DeleteFile(tfpath);

                if (File.Exists(sfpath))
                {
                    CopyFile(sfpath, tfpath);
                    DeleteFile(sfpath);
                }
            }
            catch (Exception e) { }
        }

        public static void DeleteFile(string fpath)
        {
            try
            {
                if (File.Exists(fpath))
                {
                    FileInfo fi = new FileInfo(fpath);

                    if (!IsFileLocked(fi))
                        fi.Delete();
                }
            }
            catch (Exception e) { }
        }

        public static void WriteUTF8XML(DataTable dt, string tfpath, bool withBOM = false)
        {
            FileStream stream = new FileStream(tfpath, System.IO.FileMode.Create);
            XmlTextWriter xmlWriter = new XmlTextWriter(stream, new UTF8Encoding(withBOM));
            xmlWriter.Formatting = Formatting.Indented;
            xmlWriter.Indentation = 4;
            xmlWriter.WriteStartDocument();

            dt.WriteXml(xmlWriter);
            xmlWriter.Close();
            stream.Close();
        }

        public static void WriteUTF8XML(string filepath, string content, bool withBOM = false)
        {
            Encoding utf8 = new UTF8Encoding(withBOM);
            byte[] bytes = utf8.GetBytes(content);
            string decode = utf8.GetString(bytes);
            File.WriteAllText(filepath, decode, utf8);
        }

        public static void WriteUTF8XML(string filepath, string[] content, bool withBOM = false)
        {
            Encoding utf8 = new UTF8Encoding(withBOM);
            byte[] bytes = utf8.GetBytes(ConvertStringLineArrayToString(content));
            string decode = utf8.GetString(bytes);
            File.WriteAllText(filepath, decode, utf8);
        }

        public static string ConvertStringLineArrayToString(string[] array)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string value in array)
            {
                builder.Append(value);
                builder.Append("\r\n");
            }
            return builder.ToString();
        }

        public static bool IsDirectory(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            // now we will detect whether its a directory or file
            if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                return true;

            return false;
        }

        public static void WriteNewTextFile(string fpath, string content, bool withBOM = false)
        {
            StreamWriter swriter = new StreamWriter(new FileStream(fpath, FileMode.Create, FileAccess.ReadWrite), new UTF8Encoding(withBOM));
            swriter.Write(content);
            swriter.Flush();
            swriter.Close();
        }

        public static void WriteAppendTextFile(string fpath, string append, bool withBOM = false)
        {
            StreamWriter swriter = new StreamWriter(new FileStream(fpath, FileMode.OpenOrCreate, FileAccess.ReadWrite), new UTF8Encoding(withBOM));
            swriter.Write(append);
            swriter.Flush();
            swriter.Close();
        }

        public static void ExtractFileFromAssembly(string nameSpace, string sourcepath, string targetpath)
        {
            Assembly assembly = Assembly.GetCallingAssembly();

            using (Stream s = assembly.GetManifestResourceStream(nameSpace + "." + sourcepath))
                using (BinaryReader r = new BinaryReader(s))
                    using (FileStream fs = new FileStream(targetpath, FileMode.Create))
                      using (BinaryWriter w = new BinaryWriter(fs))
                           w.Write(r.ReadBytes((int)s.Length));
        }

        public static string[] GetEmbeddedResourceNames()
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            return assembly.GetManifestResourceNames();
        }

        public static void ExtractImagesToDisk(string baseDir="", bool overwrite = false)
        {
            if (string.IsNullOrEmpty(baseDir))
                baseDir = AppDomain.CurrentDomain.BaseDirectory;

            foreach (string spath in GetEmbeddedResourceNames())
            {
                string tpath = string.Empty;
                string[] tsplit = spath.Split('.');

                if (tsplit[tsplit.Length - 1].Equals("resources"))
                    continue;

                for (int i = 0; i < tsplit.Length; i++)
                {
                    if (i < 1)
                        continue;

                    if (i == (tsplit.Length - 2))
                    {
                        tpath += (tsplit[i] + "." + tsplit[i + 1]);
                        break;
                    }

                    tpath += tsplit[i] + @"\";
                }

                tpath = Path.Combine(baseDir, tpath);

                if(File.Exists(tpath) && overwrite == false)
                    continue;

                ExtractFileFromAssembly(spath, tpath , overwrite);
            }
        }

        public static void ExtractFileFromAssembly(string sourcepath, string targetpath, bool overwrite = false)
        {
            Assembly assembly = Assembly.GetCallingAssembly();

            Directory.CreateDirectory(Path.GetDirectoryName(targetpath));

            using (Stream s = assembly.GetManifestResourceStream(sourcepath))
            using (BinaryReader r = new BinaryReader(s))
            using (FileStream fs = new FileStream(targetpath, FileMode.OpenOrCreate))
            using (BinaryWriter w = new BinaryWriter(fs))
                w.Write(r.ReadBytes((int)s.Length));
        }

        //public static string EncodeURLString(string url)
        //{
        //    char[] ch = url.ToCharArray();
        //    StringBuilder sb = new StringBuilder();
        //    Encoding ksc = Encoding.GetEncoding("ks_c_5601-1987");
        //    for (int i = 0; i < ch.Length; i++)
        //    {
        //        int temp = Convert.ToInt32(ch[i]);
        //        if (temp < 0 || temp >= 128)
        //        {
        //            byte[] bysrc = ksc.GetBytes(ch[i].ToString());
        //            sb.Append(HttpUtility.UrlEncode(bysrc));
        //        }
        //        else
        //        {
        //            sb.Append(ch[i]);
        //        }
        //    }

        //    return sb.ToString();
        //}
    }
}
