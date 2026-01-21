using FluentFTP;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
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

		public static bool CopyFile(string sfpath, string tfpath, bool overwrite = true, bool forceoverwrite = false, bool preservetime = true)
		{
			long srcFilesize = 0;
			long targetFilesize = 0;
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
					{
						if (forceoverwrite)
							DeleteFile(tfpath);
						else
						{
							FileInfo tfinfo = new FileInfo(tfpath);
							targetFilesize = tfinfo.Length;
							if (srcFilesize != targetFilesize)
								DeleteFile(tfpath);
						}
					}
					else
						return false;
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(tfpath));
				}

				ws = new FileStream(tfpath, FileMode.Create);

				byte[] b = null;

				if (srcFilesize < 4096)
					b = new byte[srcFilesize];
				else
					b = new byte[4096];

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
			if (Directory.Exists(sdpath) == false)
				return;

			if (sdpath.Equals(tfpath, StringComparison.CurrentCultureIgnoreCase))
				return;

			foreach (var file in Directory.GetFiles(sdpath, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
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
				if (File.Exists(sfpath) == false)
					return;

				if (sfpath.Equals(tfpath, StringComparison.CurrentCultureIgnoreCase))
					return;

				DeleteFile(tfpath);

				CopyFile(sfpath, tfpath);
				DeleteFile(sfpath);
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

		public static void DeleteFiles(string fpath)
		{
			DeleteFile(fpath);
			DeleteFile(GetBakFilePath(fpath));
			DeleteFile(GetTempFilePath(fpath));
		}

		public static string GetTempFilePath(string fpath)
		{
			return string.Format("{0}.tmp", fpath);
		}

		public static string GetBakFilePath(string fpath)
		{
			return string.Format("{0}.bak", fpath);
		}

		public static void CheckTempFile(string fpath)
		{
			string tmppath = GetTempFilePath(fpath);

			FileInfo tmpfi = new FileInfo(tmppath);

			if (tmpfi.Exists && tmpfi.Length > 0)
				CopyFile(tmppath, fpath);

			DeleteFile(tmppath);
		}

		public static void CheckBakFile(string fpath)
		{
			FileInfo _fi = new FileInfo(fpath);

			if (_fi.Exists && _fi.Length > 0)
				return;

			string bakpath = GetBakFilePath(fpath);
			FileInfo bakfi = new FileInfo(bakpath);

			if (bakfi.Exists && bakfi.Length > 0)
				CopyFile(bakpath, fpath);
		}

		public static string CheckFile(string fpath)
		{
			CheckTempFile(fpath);
			CheckBakFile(fpath);

			return fpath;
		}

		public static string GetValidPath4Read(string fpath)
		{
			string _retStr = fpath;

			FileInfo _fi = new FileInfo(fpath);

			if (_fi.Exists && _fi.Length > 0)
				return _retStr;

			string _bakpath = GetBakFilePath(fpath);
			FileInfo _bakfi = new FileInfo(_bakpath);

			if (_bakfi.Exists && _bakfi.Length > 0)
				_retStr = _bakpath;

			return _retStr;
		}

		public static void WriteUTF8XML(DataTable dt, string filepath, bool withBOM = false)
		{
			Encoding utf8 = new UTF8Encoding(withBOM);

			FileStream stream = null;
			XmlTextWriter xmlWriter = null;

			string tmppath = GetTempFilePath(filepath);

			try
			{
				FileInfo fi = new FileInfo(filepath);

				if (fi.Exists && fi.Length > 0)
					CopyFile(filepath, tmppath);

				using (stream = new FileStream(filepath, System.IO.FileMode.Create))
				using (xmlWriter = new XmlTextWriter(stream, utf8))
				{
					xmlWriter.Formatting = Formatting.Indented;
					xmlWriter.Indentation = 4;
					xmlWriter.WriteStartDocument();

					dt.WriteXml(xmlWriter);
				}

				DeleteFile(tmppath);

				CopyFile(filepath, GetBakFilePath(filepath));
			}
			catch (Exception e)
			{
				if (xmlWriter != null)
					xmlWriter.Close();

				if (stream != null)
					stream.Close();

				FileInfo tmpfi = new FileInfo(tmppath);

				if (tmpfi.Exists && tmpfi.Length > 0)
					CopyFile(tmppath, filepath);

				DeleteFile(tmppath);
			}
		}

		public static void WriteUTF8XML(string filepath, string content, bool withBOM = false)
		{
			Encoding utf8 = new UTF8Encoding(withBOM);
			string decode = string.Empty;

			string tmppath = GetTempFilePath(filepath);

			try
			{
				byte[] bytes = utf8.GetBytes(content);
				decode = utf8.GetString(bytes);

				if (File.Exists(filepath))
					CopyFile(filepath, tmppath);

				File.WriteAllText(filepath, decode, utf8);

				DeleteFile(tmppath);

				CopyFile(filepath, GetBakFilePath(filepath));
			}
			catch (Exception e)
			{
				if (File.Exists(tmppath))
					CopyFile(tmppath, filepath);

				DeleteFile(tmppath);
			}
		}

		public static void WriteUTF8XML(string filepath, string[] content, bool withBOM = false)
		{
			Encoding utf8 = new UTF8Encoding(withBOM);
			string decode = string.Empty;

			string tmppath = GetTempFilePath(filepath);

			try
			{
				byte[] bytes = utf8.GetBytes(ConvertStringLineArrayToString(content));
				decode = utf8.GetString(bytes);

				if (File.Exists(filepath))
					CopyFile(filepath, tmppath);

				File.WriteAllText(filepath, decode, utf8);

				DeleteFile(tmppath);

				CopyFile(filepath, GetBakFilePath(filepath));
			}
			catch (Exception e)
			{
				if (File.Exists(tmppath))
					CopyFile(tmppath, filepath);

				DeleteFile(tmppath);
			}
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
			StreamWriter swriter = new StreamWriter(fpath, false, new UTF8Encoding(withBOM));
			swriter.Write(content);
			swriter.Flush();
			swriter.Close();
		}

		public static void WriteAppendTextFile(string fpath, string append, bool withBOM = false)
		{
			StreamWriter swriter = new StreamWriter(fpath, true, new UTF8Encoding(withBOM));
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

		public static void ExtractImagesToDisk(string baseDir = "", bool overwrite = false)
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

				if (File.Exists(tpath) && overwrite == false)
					continue;

				ExtractFileFromAssembly(spath, tpath, overwrite);
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

		public static string EncodeURLString(string url)
		{
			char[] ch = url.ToCharArray();
			StringBuilder sb = new StringBuilder();
			Encoding ksc = Encoding.GetEncoding("ks_c_5601-1987");
			for (int i = 0; i < ch.Length; i++)
			{
				int temp = Convert.ToInt32(ch[i]);
				if (temp < 0 || temp >= 128)
				{
					byte[] bysrc = ksc.GetBytes(ch[i].ToString());
					sb.Append(HttpUtility.UrlEncode(bysrc));
				}
				else
				{
					sb.Append(ch[i]);
				}
			}

			return sb.ToString();
		}

		public static bool EmailIsValid(string emailAddress)
		{
			string validEmailPattern = @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|"
				+ @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)"
				+ @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$";

			return new Regex(validEmailPattern, RegexOptions.IgnoreCase).IsMatch(emailAddress);
		}

		public static bool HasWrongPathCharacter(string str)
		{
			bool has = false;

			foreach (char ch in Path.GetInvalidPathChars())
			{
				has = str.IndexOf(ch) > -1;
				if (has)
					break;
			}

			return has;
		}

		public static bool HasWrongFileCharacter(string str)
		{
			bool has = false;

			foreach (char ch in Path.GetInvalidFileNameChars())
			{
				has = str.IndexOf(ch) > -1;
				if (has)
					break;
			}

			return has;
		}

		public static bool Exists(string fpath)
		{
			if (File.Exists(fpath))
			{
				if (new FileInfo(fpath).Length <= 0)
				{
					DeleteFile(fpath);
					return false;
				}

				return true;
			}

			return false;
		}


		public static long GetFileSizeBytesFromURL(string url)
		{
			long _size = 0;

			try
			{
				var webRequest = HttpWebRequest.Create(new Uri(url));
				webRequest.Method = "HEAD";

				using (var webResponse = webRequest.GetResponse())
					_size = Convert.ToInt64(webResponse.Headers.Get("Content-Length"));
			}
			catch { }

			return _size;
		}


		public static async Task DownloadAsyncFromRemote(string host, int port, string id, string pw, string remotePath, string localPath, int retryCount = 2)
		{
			string _localHash = XXHash64.ComputePartialSignature(localPath);
            if(string.IsNullOrEmpty(_localHash) == false)
			{
				string _remoteHash = await XXHash64.ComputePartialSignatureFtpAsync(host, port, id, pw, remotePath);
				if (_localHash.Equals(_remoteHash))
					return;
			}

			using (AsyncFtpClient ftp = new AsyncFtpClient(host, id, pw, port))
			{
				try
				{
					ftp.Config.RetryAttempts = retryCount;
					await ftp.Connect();
					await ftp.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite, FtpVerify.Retry);
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
			}
		}

	}
}
