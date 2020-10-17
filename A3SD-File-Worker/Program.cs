// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

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

		public static async Task Main() {
			// string sourcePath = Console.ReadLine();
			// string outputPath = Console.ReadLine();
			// string sourcePath = @"F:\Documents";
			string sourcePath = @"F:\A3-doc-src";
			string outputPath = @"F:\Doc2";

			Console.WriteLine(CountAccessibleFiles(sourcePath) + " files in '" + sourcePath + "' ");
			Console.WriteLine("Press key to start");
			Console.ReadKey(); Console.WriteLine();
			Console.WriteLine("Enter cancel to stop.");

			Stopwatch s = new Stopwatch();

			for (int i = 0; i < 1; i++) {
				CancellationTokenSource cancel = new CancellationTokenSource();
				FileCopyWorkerAsync fileCopier = new FileCopyWorkerAsync();
				s.Restart();
				fileCopier.EnqueueJob(sourcePath, outputPath);
				await fileCopier.StartJobs(cancel.Token);
				Console.WriteLine(s.ElapsedMilliseconds + "ms");
				s.Stop();
				new DirectoryInfo(outputPath).Delete(true);

				cancel.Dispose();
			}

			Console.WriteLine("Exec Done. Press key to exit.");
			Console.ReadKey(); Console.WriteLine();
		}
	}
}
