// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

using A3SD_File_Worker_InOutPath;
using A3SD_File_Worker_MergeInOutPath;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace A3SD_File_Worker {
	public class FileMergeWorkerAsync {
		public bool CleanOutput;
		public int concurrentTasks;
		public int readWriteStreamBufferSize;
		private ImmutableArray<MergeInOutPath> mergeJobs = new ImmutableArray<MergeInOutPath>();
		private readonly ImmutableArray<MergeInOutPath>.Builder mergeJobsBuilder = ImmutableArray.CreateBuilder<MergeInOutPath>();
		private readonly SortedSet<InOutPath> mergeRequests = new SortedSet<InOutPath>(new A3SD_File_Worker_InOutPath.SortOutputThenInputAscendingHelper());
		private readonly List<InOutPath> directoryJobs = new List<InOutPath>();
		private int mergeJobIndex = -1;

		public FileMergeWorkerAsync(bool CleanOutput = true, int concurrentTasks = 6, int readWriteStreamBufferSize = 32_768) {
			this.CleanOutput = CleanOutput;
			this.concurrentTasks = concurrentTasks;
			this.readWriteStreamBufferSize = readWriteStreamBufferSize;
		}

		public void ApplyLargeCopyOptimisationPreset() {
			concurrentTasks = 2;
			readWriteStreamBufferSize = 16_777_216;
		}

		public async Task StartJobs(CancellationToken cancel) {
			{
				foreach (InOutPath job in directoryJobs) {
					DirectoryInfo sourceDir = new DirectoryInfo(job.input);
					foreach (DirectoryInfo dir in sourceDir.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
						Directory.CreateDirectory(Path.Combine(job.output, Path.GetRelativePath(job.input, dir.FullName)));
					}
				};
				directoryJobs.Clear();
				InOutPath[] mergeRequest = mergeRequests.ToArray();
				mergeRequests.Clear();
				InOutPath last = new InOutPath();
				int indexStart = 0;
				for (int i = 0; i < mergeRequest.Length; i++) {
					if (!mergeRequest[i].Equals(last)) {
						mergeJobsBuilder.Add(new MergeInOutPath(mergeRequest[indexStart..(i + 1)]));
						indexStart = i + 1;
					}
					last = mergeRequest[i];
				}
				mergeJobs = mergeJobsBuilder.ToImmutable();
				mergeJobsBuilder.Clear();
			}
			Task[] ThreadedTasks = new Task[concurrentTasks];
			for (int i = 0; i < concurrentTasks; i++) {
				ThreadedTasks[i] = CreateMergeTask(cancel);
			}
			await Task.WhenAll(ThreadedTasks);
			mergeJobs.Clear();
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
			if (createDirectory) Directory.CreateDirectory(Path.GetDirectoryName(job.output));
			mergeRequests.Add(job);
		}

		private bool GetJob(out MergeInOutPath job) {
			int index = Interlocked.Increment(ref mergeJobIndex);
			if (index < mergeJobs.Length) {
				job = mergeJobs[index];
				return true;
			} else {
				job = new MergeInOutPath();
				return false;
			}
		}

		private async Task CreateMergeTask(CancellationToken cancel) {
			while (!cancel.IsCancellationRequested && GetJob(out MergeInOutPath mergeJob)) {
				if (CleanOutput && File.Exists(mergeJob.output)) {
					File.Delete(mergeJob.output);
				}
				using FileStream writer = new FileStream(mergeJob.output, FileMode.Append, FileAccess.Write, FileShare.None, readWriteStreamBufferSize, true);
				foreach (string input in mergeJob.inputs) {
					using FileStream reader = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read, readWriteStreamBufferSize, true);
					await reader.CopyToAsync(writer, cancel);
					await reader.DisposeAsync();
				}
				await writer.DisposeAsync();
			}
		}
	}
}