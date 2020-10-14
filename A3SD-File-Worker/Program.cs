using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace A3SD_File_Worker {
	class Program {
		public struct InOutPath {
			public string source, output;
			public InOutPath(string source, string output) {
				this.source = source;
				this.output = output;
			}
		}

		public static string outputDirectory;
		public static ConcurrentQueue<InOutPath> copyJobs = new ConcurrentQueue<InOutPath>();
		public static ManualResetEvent waitForCopy = new ManualResetEvent(false);

		public static async Task StartCopyRoutine(CancellationToken streamComplete, CancellationToken cancel) {
			FileStream reader;
			FileStream writer;
			InOutPath job;
			bool hasDelayed = false;
			while (!cancel.IsCancellationRequested) {
				if (copyJobs.TryDequeue(out job)) {
					//Console.WriteLine("Dequeued ["+ job.source + "; "+ job.output + "]");
					hasDelayed = false;
					reader = new FileStream(job.source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
					writer = new FileStream(Path.Combine(outputDirectory, job.output), FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
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

		public static Thread StartCopyThread(CancellationToken streamComplete, CancellationToken cancel) {
			Thread copyThread = new Thread(async () => {
				int maxConcurrentTasks = 6;
				Task[] Routines = new Task[maxConcurrentTasks];
				for (int i = 0; i < maxConcurrentTasks; i++) {
					Routines[i] = StartCopyRoutine(streamComplete, cancel);
				};
				await Task.WhenAll(Routines);
				waitForCopy.Set();
			});
			copyThread.Start();
			return copyThread;
		}

		public static void CreateCopyJobs(string sourcePath, string outputPath, CancellationToken cancel) {
			DirectoryInfo sourceDir = new DirectoryInfo(sourcePath);
			if (!sourceDir.Exists) {
				throw new DirectoryNotFoundException("Could not find '" + sourcePath + "'");
			}
			outputDirectory = outputPath;
			foreach (DirectoryInfo dir in sourceDir.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				Directory.CreateDirectory(Path.Combine(outputPath, Path.GetRelativePath(sourcePath, dir.FullName)));
			}

			CancellationTokenSource streamComplete = new CancellationTokenSource();
			Thread copyThread = StartCopyThread(streamComplete.Token, cancel);
			foreach (FileInfo file in sourceDir.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) {
				copyJobs.Enqueue(new InOutPath(file.FullName, Path.GetRelativePath(sourcePath, file.FullName)));
				//Console.WriteLine("Enqueued [" + file.FullName + "; " + Path.Combine(outputPath, Path.GetRelativePath(sourcePath, file.FullName)) + "]");
			}
			streamComplete.Cancel();
			waitForCopy.WaitOne();
		}

		public static int CountAccessibleFiles(string dir) {
			DirectoryInfo sourceDir = new DirectoryInfo(dir);
			if (!sourceDir.Exists) {
				throw new DirectoryNotFoundException("Could not find '" + dir + "'");
			}
			return sourceDir.EnumerateFiles("*", new EnumerationOptions {
				IgnoreInaccessible = true,
				RecurseSubdirectories = true
			}).Count();
		}

		public static void Main(string[] args) {
			Console.WriteLine("Press key to start");
			Console.ReadKey(); Console.WriteLine();
			string sourcePath = Console.ReadLine();
			string outputPath = Console.ReadLine();
			CancellationTokenSource cancel = new CancellationTokenSource();
			Stopwatch s = new Stopwatch();
			Console.WriteLine(CountAccessibleFiles(sourcePath) + " files in '"+ sourcePath + "' ");
			s.Start();
			CreateCopyJobs(sourcePath, outputPath, cancel.Token);
			Console.WriteLine(s.ElapsedMilliseconds + "ms");
			s.Stop();
			Console.WriteLine(CountAccessibleFiles(outputPath) + " files in '" + outputPath + "' ");
			Console.WriteLine("Exec Done.");
			Console.ReadKey(); Console.WriteLine();

		}
	}
}
