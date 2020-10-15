using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace A3SD_File_Worker {
	public class FileCopyWorkerAsync {
		public FileCopyWorkerAsync() {
		}

		public void ApplyLargeCopyOptimisationPreset() {
			concurrentTasks = 2;
			readWriteStreamBuffer = 16_777_216;
		}

		private ConcurrentQueue<InOutPath> copyJobs = new ConcurrentQueue<InOutPath>();
		private int concurrentTasks = 6;
		public int readWriteStreamBuffer = 32_768;
		public bool ReplaceAll = false;

		private struct InOutPath {
			public string source, output;
			public InOutPath(string source, string output) {
				this.source = source;
				this.output = output;
			}
		}

		private async Task CreateCopyTask(CancellationToken streamComplete, CancellationToken cancel) {
			FileStream reader;
			FileStream writer;
			InOutPath job;
			bool hasDelayed = false;
			while (!cancel.IsCancellationRequested) {
				if (copyJobs.TryDequeue(out job)) {
					hasDelayed = false;
					reader = new FileStream(job.source, FileMode.Open, FileAccess.Read, FileShare.Read, readWriteStreamBuffer, true);
					writer = new FileStream(job.output, FileMode.Create, FileAccess.Write, FileShare.None, readWriteStreamBuffer, true);
					await reader.CopyToAsync(writer, cancel);
					await reader.DisposeAsync();
					await writer.DisposeAsync();
				} else {
					if (hasDelayed && streamComplete.IsCancellationRequested) break;
					hasDelayed = true;
					await Task.Delay(1, cancel);
				}
			}
		}

		public async Task CreateCopyJobs(string sourcePath, string outputPath, CancellationToken cancel) {
			DirectoryInfo sourceDir = new DirectoryInfo(sourcePath);
			if (!sourceDir.Exists) {
				throw new DirectoryNotFoundException("Could not find '" + sourcePath + "'");
			}
			foreach (DirectoryInfo dir in sourceDir.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				Directory.CreateDirectory(Path.Combine(outputPath, Path.GetRelativePath(sourcePath, dir.FullName)));
			}

			CancellationTokenSource streamComplete = new CancellationTokenSource();
			Task[] ThreadedTasks = new Task[concurrentTasks];
			try {
				for (int i = 0; i < concurrentTasks; i++) {
					ThreadedTasks[i] = CreateCopyTask(streamComplete.Token, cancel);
				}
				foreach (FileInfo file in sourceDir.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
					string targetFilePath = Path.Combine(outputPath, Path.GetRelativePath(sourcePath, file.FullName));
					if (!ReplaceAll && file.LastWriteTime <= new FileInfo(targetFilePath).LastWriteTime) continue;
					copyJobs.Enqueue(new InOutPath(file.FullName, targetFilePath));
				}
				streamComplete.Cancel();
				await Task.WhenAll(ThreadedTasks);
			} finally {
				foreach (Task task in ThreadedTasks) {
					task.Dispose();
				}
				streamComplete.Dispose();
			}
		}
	}
}