using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace A3SD_File_Worker {
	class Program {
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

		public static async Task Main(string[] args) {
			//string sourcePath = Console.ReadLine();
			//string outputPath = Console.ReadLine();
			//string sourcePath = @"F:\Documents";
			string sourcePath = @"F:\A3-doc-src";
			string outputPath = @"F:\Doc2";
			Stopwatch s = new Stopwatch();
			for (int i = 0; i < 4; i++) {
				CancellationTokenSource cancel = new CancellationTokenSource();
				Console.WriteLine(CountAccessibleFiles(sourcePath) + " files in '" + sourcePath + "' ");
				FileCopyWorkerAsync fileCopier = new FileCopyWorkerAsync();
				fileCopier.readWriteStreamBuffer = 4096 * (int)Math.Pow(2, i);
				Console.WriteLine("readWriteStreamBuffer: " + (int)(4096 * Math.Pow(2, i)));
				Console.WriteLine("Press key to start");
				Console.ReadKey(); Console.WriteLine();
				s.Restart();
				await fileCopier.CreateCopyJobs(sourcePath, outputPath, cancel.Token);
				Console.WriteLine(s.ElapsedMilliseconds + "ms");
				s.Stop();
				cancel.Dispose();
				Console.WriteLine(CountAccessibleFiles(outputPath) + " files in '" + outputPath + "' ");
			}
			Console.WriteLine("Exec Done. Press key to exit.");
			Console.ReadKey(); Console.WriteLine();

		}
	}
}
