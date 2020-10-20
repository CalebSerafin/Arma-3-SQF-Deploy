// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

using A3SD_File_Worker_InOutPath;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace A3SD_File_Worker {
	public class FileCopyWorkerAsync {
		public bool ReplaceAll;
		public int concurrentTasks;
		public int readWriteStreamBufferSize;
		private ImmutableArray<InOutPath> copyJobs = new ImmutableArray<InOutPath>();
		private readonly ImmutableArray<InOutPath>.Builder copyJobsBuilder = ImmutableArray.CreateBuilder<InOutPath>();
		private int copyJobIndex = -1;

		private readonly List<InOutPath> directoryJobs = new List<InOutPath>();

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
			copyJobs = copyJobsBuilder.ToImmutable();
			copyJobsBuilder.Clear();
			foreach (InOutPath job in directoryJobs) {
				DirectoryInfo sourceDir = new DirectoryInfo(job.input);
				foreach (DirectoryInfo dir in sourceDir.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
					Directory.CreateDirectory(Path.Combine(job.output, Path.GetRelativePath(job.input, dir.FullName)));
				}
			};
			directoryJobs.Clear();
			Task[] ThreadedTasks = new Task[concurrentTasks];
			for (int i = 0; i < concurrentTasks; i++) {
				ThreadedTasks[i] = CreateCopyTask(cancel);
			}
			await Task.WhenAll(ThreadedTasks);
		}

		public void EnqueueJob(string source, string output) => EnqueueJob(new InOutPath(source, output));
		public void EnqueueJob(InOutPath job) {
			if (File.Exists(job.output)) {
				EnqueueFileJob(job);
			} else if (Directory.Exists(job.input)) {
				EnqueueDirectoryJob(job);
			} else {
				throw new DirectoryNotFoundException("Could not find Directory/File '" + job.input + "'");
			}
		}

		public void EnqueueDirectoryJob(string source, string output) => EnqueueDirectoryJob(new InOutPath(source, output));
		public void EnqueueDirectoryJob(InOutPath job) {
			directoryJobs.Add(job);
			foreach (FileInfo file in new DirectoryInfo(job.input).EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				EnqueueFileJob(new InOutPath(file.FullName, Path.Combine(job.output, Path.GetRelativePath(job.input, file.FullName))));
			}
		}

		public void EnqueueFileJob(string source, string output, bool createDirectory = false) => EnqueueFileJob(new InOutPath(source, output), createDirectory);
		public void EnqueueFileJob(InOutPath job, bool createDirectory = false) {
			if (ReplaceAll || new FileInfo(job.input).LastWriteTime > new FileInfo(job.output).LastWriteTime) {
				if (createDirectory) Directory.CreateDirectory(Path.GetDirectoryName(job.output));
				copyJobsBuilder.Add(job);
			}
		}

		private bool GetJob (out InOutPath job) {
			int index = Interlocked.Increment(ref copyJobIndex);
			if (index < copyJobs.Length) {
				job = copyJobs[index];
				return true;
			} else {
				job = new InOutPath();
				return false;
			}
		}

		private async Task CreateCopyTask(CancellationToken cancel) {
			while (!cancel.IsCancellationRequested && GetJob(out InOutPath job)) {
				using FileStream reader = new FileStream(job.input, FileMode.Open, FileAccess.Read, FileShare.Read, readWriteStreamBufferSize, true);
				using FileStream writer = new FileStream(job.output, FileMode.Create, FileAccess.Write, FileShare.None, readWriteStreamBufferSize, true);
				await reader.CopyToAsync(writer, cancel);
				await reader.DisposeAsync();
				await writer.DisposeAsync();
			}
		}
	}
}