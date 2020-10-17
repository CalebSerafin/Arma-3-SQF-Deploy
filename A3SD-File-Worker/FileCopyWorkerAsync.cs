// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace A3SD_File_Worker {
	public class FileCopyWorkerAsync {
		public bool ReplaceAll;
		public int concurrentTasks;
		public int readWriteStreamBufferSize;
		private readonly ConcurrentQueue<InOutPath> copyJobs = new ConcurrentQueue<InOutPath>();

		public FileCopyWorkerAsync(bool ReplaceAll = false, int concurrentTasks = 6, int readWriteStreamBufferSize = 32_768) {
			this.ReplaceAll = ReplaceAll;
			this.concurrentTasks = concurrentTasks;
			this.readWriteStreamBufferSize = readWriteStreamBufferSize;
		}

		public void ApplyLargeCopyOptimisationPreset() {
			concurrentTasks = 2;
			readWriteStreamBufferSize = 16_777_216;
		}

		public async Task StartJobs(CancellationToken cancel) {
			CancellationTokenSource streamComplete = new CancellationTokenSource();
			Task[] ThreadedTasks = new Task[concurrentTasks];
			try {
				for (int i = 0; i < concurrentTasks; i++) {
					ThreadedTasks[i] = CreateCopyTask(streamComplete.Token, cancel);
				}
				streamComplete.Cancel();
				await Task.WhenAll(ThreadedTasks);
			} finally {
				streamComplete.Dispose();
			}
		}

		public void EnqueueJob(string source, string output) => EnqueueJob(new InOutPath(source, output));
		public void EnqueueJob(InOutPath job) {
			if (File.Exists(job.output)) {
				EnqueueFileJob(job);
			} else if (Directory.Exists(job.source)) {
				EnqueueDirectoryJob(job);
			} else {
				throw new DirectoryNotFoundException("Could not find Directory/File '" + job.source + "'");
			}
		}

		public void EnqueueDirectoryJob(string source, string output) => EnqueueDirectoryJob(new InOutPath(source, output));
		public void EnqueueDirectoryJob(InOutPath job) {
			DirectoryInfo sourceDir = new DirectoryInfo(job.source);
			foreach (DirectoryInfo dir in sourceDir.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				Directory.CreateDirectory(Path.Combine(job.output, Path.GetRelativePath(job.source, dir.FullName)));
			}
			foreach (FileInfo file in sourceDir.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				EnqueueFileJob(new InOutPath(file.FullName, Path.Combine(job.output, Path.GetRelativePath(job.source, file.FullName))));
			}
		}

		public void EnqueueFileJob(string source, string output, bool createDirectory = false) => EnqueueFileJob(new InOutPath(source, output), createDirectory);
		public void EnqueueFileJob(InOutPath job, bool createDirectory = false) {
			if (ReplaceAll || new FileInfo(job.source).LastWriteTime > new FileInfo(job.output).LastWriteTime) {
				if (createDirectory) Directory.CreateDirectory(Path.GetDirectoryName(job.output));
				copyJobs.Enqueue(job);
			}
		}

		private async Task CreateCopyTask(CancellationToken streamComplete, CancellationToken cancel) {
			bool hasDelayed = false;
			while (!cancel.IsCancellationRequested) {
				if (copyJobs.TryDequeue(out InOutPath job)) {
					{
						hasDelayed = false;
						using FileStream reader = new FileStream(job.source, FileMode.Open, FileAccess.Read, FileShare.Read, readWriteStreamBufferSize, true);
						using FileStream writer = new FileStream(job.output, FileMode.Create, FileAccess.Write, FileShare.None, readWriteStreamBufferSize, true);
						await reader.CopyToAsync(writer, cancel);
						await reader.DisposeAsync();
						await writer.DisposeAsync();
					}
				} else {
					if (hasDelayed && streamComplete.IsCancellationRequested) break;
					hasDelayed = true;
					await Task.Delay(1, cancel);
				}
			}
		}
	}

	public struct InOutPath {
		public string source, output;
		public InOutPath(string source, string output) {
			this.source = source;
			this.output = output;
		}

		public bool Equals(InOutPath inOutPath) {
			return source.Equals(inOutPath.source) && output.Equals(inOutPath.output);
		}
		public override bool Equals(object? obj) {
			return obj is InOutPath && Equals(obj);
		}

		public override int GetHashCode() {
			return System.HashCode.Combine(source, output);
		}

		public static bool operator ==(InOutPath left, InOutPath right) {
			return left.Equals(right);
		}

		public static bool operator !=(InOutPath left, InOutPath right) {
			return !(left == right);
		}
	}
}