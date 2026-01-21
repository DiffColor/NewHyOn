using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using K4os.Hash.xxHash;

namespace TurtleTools
{
	/// <summary>
	/// xxHash.net을 사용한 xxHash64 해시 계산기.
	/// 스트리밍 기반으로 동작해 대용량 파일도 최소한의 메모리로 처리합니다.
	/// </summary>
	public static class XXHash64
	{
		private const int BufferSize = 4 * 1024 * 1024; // 4MB
		private const int PartialBlockSize = 1024; // 1KB

		public static string ComputePartialSignature(string filePath, int blockSize = PartialBlockSize)
		{
			if (string.IsNullOrWhiteSpace(filePath) || blockSize <= 0)
				return string.Empty;

			try
			{
				var fileInfo = new FileInfo(filePath);
				if (!fileInfo.Exists)
					return string.Empty;

				long fileSize = fileInfo.Length;

				if (fileSize < blockSize * 3)
				{
					byte[] data = File.ReadAllBytes(filePath);
					return ComputeXXHash64(data);
				}

				byte[] block1 = new byte[blockSize];
				byte[] block2 = new byte[blockSize];
				byte[] block3 = new byte[blockSize];

				using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					fs.Read(block1, 0, blockSize);
					fs.Seek(fileSize / 2, SeekOrigin.Begin);
					fs.Read(block2, 0, blockSize);
					fs.Seek(fileSize - blockSize, SeekOrigin.Begin);
					fs.Read(block3, 0, blockSize);
				}

				return ComputePartialSignature(block1, block2, block3, fileSize);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"로컬 부분 해시 계산 실패: {ex.Message}");
				return string.Empty;
			}
		}

		public static string ComputePartialSignature(byte[] block1, byte[] block2, byte[] block3, long fileSize)
		{
			try
			{
				if (block1 == null || block2 == null || block3 == null)
					return string.Empty;
				if (fileSize < 0)
					return string.Empty;

				var hasher = new XXH64();
				hasher.Update(block1, 0, block1.Length);
				hasher.Update(block2, 0, block2.Length);
				hasher.Update(block3, 0, block3.Length);

				byte[] sizeBytes = BitConverter.GetBytes(fileSize);
				if (BitConverter.IsLittleEndian)
					Array.Reverse(sizeBytes);

				hasher.Update(sizeBytes, 0, sizeBytes.Length);
				return hasher.Digest().ToString("X16");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"부분 해시 계산 실패: {ex.Message}");
				return string.Empty;
			}
		}

		public static async Task<string> ComputePartialSignatureFtpAsync(
			string host,
			int port,
			string username,
			string password,
			string remotePath,
			int blockSize = PartialBlockSize)
		{
			if (string.IsNullOrWhiteSpace(remotePath) || blockSize <= 0)
				return string.Empty;

			using (var client = new AsyncFtpClient(host, username, password, port))
			{
				try
				{
					await client.Connect();

					long fileSize = 0;
					try
					{
						fileSize = await client.GetFileSize(remotePath);
						if (fileSize <= 0)
							return string.Empty;
					}
					catch (Exception sizeEx)
					{
						Debug.WriteLine($"파일 크기 확인 오류: {sizeEx.Message}");
						return string.Empty;
					}

					if (fileSize < blockSize * 3)
					{
						var fullHash = await DownloadFullFileAndHashAsync(client, remotePath);
						return fullHash ?? string.Empty;
					}

					byte[] block1 = await DownloadChunkAsync(client, remotePath, 0, blockSize);
					byte[] block2 = await DownloadChunkAsync(client, remotePath, fileSize / 2, blockSize);
					byte[] block3 = await DownloadChunkAsync(client, remotePath, fileSize - blockSize, blockSize);

					if (block1 == null || block2 == null || block3 == null)
						return string.Empty;

					return ComputePartialSignature(block1, block2, block3, fileSize);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"FTP 파일 해시 오류: {ex.Message}");
					return string.Empty;
				}
			}
		}

		private static async Task<string> DownloadFullFileAndHashAsync(AsyncFtpClient client, string remotePath)
		{
			try
			{
				using (var ms = new MemoryStream())
				{
					var status = await client.DownloadStream(ms, remotePath);

					if (status)
					{
						ms.Position = 0;
						return ComputeXXHash64(ms.ToArray());
					}

					Debug.WriteLine($"파일 다운로드 실패: {status}");
					return string.Empty;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"전체 파일 다운로드 오류: {ex.Message}");
				return string.Empty;
			}
		}

		private static async Task<byte[]> DownloadChunkAsync(AsyncFtpClient client, string remotePath, long offset, int length)
		{
			byte[] buffer = new byte[length];
			try
			{
				using (var stream = await client.OpenRead(remotePath, FtpDataType.Binary, offset))
				{
					int totalRead = 0;
					int retryCount = 0;
					const int maxRetries = 3;

					while (totalRead < length && retryCount < maxRetries)
					{
						int bytesRead = await stream.ReadAsync(buffer, totalRead, length - totalRead);
						if (bytesRead == 0)
						{
							retryCount++;
							await Task.Delay(200);
							continue;
						}

						totalRead += bytesRead;
						retryCount = 0;
					}

					if (totalRead < length)
					{
						byte[] resizedBuffer = new byte[totalRead];
						Array.Copy(buffer, resizedBuffer, totalRead);
						return resizedBuffer;
					}

					return buffer;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"청크 다운로드 오류 (오프셋: {offset}, 길이: {length}): {ex.Message}");
				return null;
			}
		}

		public static string ComputeXXHash64(byte[] data)
		{
			if (data == null)
				return string.Empty;

			try
			{
				var hasher = new XXH64();
				hasher.Update(data, 0, data.Length);
				return hasher.Digest().ToString("X16");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"바이트 해시 계산 실패: {ex.Message}");
				return string.Empty;
			}
		}

		public static string ComputeXXHash64(string value)
		{
			if (value == null)
				return string.Empty;

			try
			{
				byte[] bytes = Encoding.UTF8.GetBytes(value);
				return ComputeXXHash64(bytes);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"문자열 해시 계산 실패: {ex.Message}");
				return string.Empty;
			}
		}

		public static async Task<string> ComputeXXHash64Async(
			string filePath,
			IProgress<int> progress = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				return string.Empty;

			byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

			try
			{
				var fileInfo = new FileInfo(filePath);
				if (!fileInfo.Exists)
					return string.Empty;

				long fileSize = fileInfo.Length;
				long totalBytesRead = 0;
				int lastReportedProgress = 0;

				using (var fileStream = new FileStream(
					filePath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read,
					BufferSize,
					FileOptions.SequentialScan | FileOptions.Asynchronous))
				{
					var hasher = new XXH64();

					int bytesRead;
					while ((bytesRead = await fileStream
						.ReadAsync(buffer, 0, BufferSize, cancellationToken)
						.ConfigureAwait(false)) > 0)
					{
						hasher.Update(buffer, 0, bytesRead);
						totalBytesRead += bytesRead;

						if (progress != null && fileSize > 0)
						{
							int progressPercentage = (int)((totalBytesRead * 100) / fileSize);
							if (progressPercentage != lastReportedProgress)
							{
								lastReportedProgress = progressPercentage;
								progress.Report(progressPercentage);
							}
						}
					}

					progress?.Report(100);

					ulong hash = hasher.Digest();
					return hash.ToString("X16");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"파일 해시 계산 실패: {ex.Message}");
				return string.Empty;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}
	}
}
