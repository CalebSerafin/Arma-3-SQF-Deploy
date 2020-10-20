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
	public class FileCopierAsync {
		public bool replaceAll = false;
		public int concurrentTasks = 6;
		public int RWBufferSize = 32_768;
		private readonly List<InOutPath> directoryJobs = new List<InOutPath>();
		private readonly ImmutableArray<InOutPath>.Builder copyJobsBuilder = ImmutableArray.CreateBuilder<InOutPath>();
		private ImmutableArray<InOutPath> copyJobs = new ImmutableArray<InOutPath>();
		private int copyJobIndex = -1;

		public void ApplyLargeCopyOptimisationPreset() {
			concurrentTasks = 2;
			RWBufferSize = 16_777_216;
		}

		public async Task CopyAsync(CancellationToken cancel) {
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
				ThreadedTasks[i] = CopyTask(cancel);
			}
			await Task.WhenAll(ThreadedTasks);
		}

		public void Add(string source, string output) => Add(new InOutPath(source, output));
		public void Add(InOutPath job) {
			if (File.Exists(job.output)) {
				AddFile(job);
			} else if (Directory.Exists(job.input)) {
				AddDirectory(job);
			} else {
				throw new DirectoryNotFoundException("Could not find Directory/File '" + job.input + "'");
			}
		}

		public void AddDirectory(string source, string output) => AddDirectory(new InOutPath(source, output));
		public void AddDirectory(InOutPath job) {
			directoryJobs.Add(job);
			foreach (FileInfo file in new DirectoryInfo(job.input).EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				AddFile(new InOutPath(file.FullName, Path.Combine(job.output, Path.GetRelativePath(job.input, file.FullName))));
			}
		}

		public void AddFile(string source, string output) => AddFile(new InOutPath(source, output));
		public void AddFile(InOutPath job) {
			if (replaceAll || new FileInfo(job.input).LastWriteTime > new FileInfo(job.output).LastWriteTime) {
				copyJobsBuilder.Add(job);
			}
		}

		private bool GetJob (out InOutPath job) {
			int index = Interlocked.Increment(ref copyJobIndex);
			if (index >= copyJobs.Length) {
				job = new InOutPath();
				return false;
			};
			job = copyJobs[index];
			return true;
		}

		private async Task CopyTask(CancellationToken cancel) {
			while (!cancel.IsCancellationRequested && GetJob(out InOutPath job)) {
				using FileStream reader = new FileStream(job.input, FileMode.Open, FileAccess.Read, FileShare.Read, RWBufferSize, true);
				using FileStream writer = new FileStream(job.output, FileMode.Create, FileAccess.Write, FileShare.None, RWBufferSize, true);
				await reader.CopyToAsync(writer, cancel);
				await reader.DisposeAsync();
				await writer.DisposeAsync();
			}
		}
	}
}