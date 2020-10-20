// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
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

			Stopwatch s = new Stopwatch();
			CancellationTokenSource cancel = new CancellationTokenSource();

			for (int i = 0; i < 5; i++) {
				FileCopierAsync fileCopier = new FileCopierAsync();
				FileMergerAsync fileMerger = new FileMergerAsync();

				s.Restart();
				fileCopier.Add(sourcePath, outputPath);
				s.Stop();
				Console.WriteLine($"fileCopier enqueue time: {s.ElapsedMilliseconds}ms");

				s.Restart();
				await fileCopier.CopyAsync(cancel.Token);
				s.Stop();
				Console.WriteLine($"fileCopier run time: {s.ElapsedMilliseconds}ms");
				new DirectoryInfo(outputPath).Delete(true);

				s.Restart();
				fileMerger.Add(sourcePath, outputPath);
				s.Stop();
				Console.WriteLine($"fileMerger enqueue time: {s.ElapsedMilliseconds}ms");

				s.Restart();
				await fileMerger.MergeAsync(cancel.Token);
				s.Stop();
				Console.WriteLine($"fileMerger run time: {s.ElapsedMilliseconds}ms");
				new DirectoryInfo(outputPath).Delete(true);

			}
			s.Stop();
			cancel.Dispose();

			Console.WriteLine("Exec Done. Press key to exit.");
			Console.ReadKey(); Console.WriteLine();
		}
	}
}
