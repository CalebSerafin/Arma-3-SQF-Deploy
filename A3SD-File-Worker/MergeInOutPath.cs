// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace A3SD_File_Worker_MergeInOutPath {
	public struct MergeInOutPath : IComparable {
		public ImmutableArray<string> inputs;
		public string output;
		public MergeInOutPath(params string[]? data) {
			if (data is null || data.Length < 2) {
				this = new MergeInOutPath();
			} else {
				inputs = data[0..^2].ToImmutableArray();
				output = data[^1];
			}
		}
		public MergeInOutPath(params A3SD_File_Worker_InOutPath.InOutPath[] jobs) {
			if (jobs is null || jobs.Length == 0) {
				this = new MergeInOutPath();
			} else {
				inputs = jobs.Select(x => x.input).ToImmutableArray();
				output = jobs[0].output;
			}
		}
		public MergeInOutPath(ImmutableArray<string>? inputs, string? output) {
			this.inputs = inputs ?? new ImmutableArray<string>() { "" };
			this.output = output ?? "";
		}

		public override int GetHashCode() => HashCode.Combine(inputs, output);
		public override string? ToString() => $"'{inputs}' '{output}'";
		public bool Equals(MergeInOutPath inOutPath) {
			if (!output.Equals(inOutPath.output, StringComparison.OrdinalIgnoreCase) || inputs.Length != inOutPath.inputs.Length) return false;
			ImmutableSortedSet<string> our = inputs.ToImmutableSortedSet();
			ImmutableSortedSet<string> their = inOutPath.inputs.ToImmutableSortedSet();
			return our.SetEquals(their);
		}

		public override bool Equals(object? obj) => obj is MergeInOutPath && Equals(obj);
		public static bool operator ==(MergeInOutPath left, MergeInOutPath right) => left.Equals(right);
		public static bool operator !=(MergeInOutPath left, MergeInOutPath right) => !(left == right);
		public static bool operator <(MergeInOutPath left, MergeInOutPath right) => left.CompareTo(right) < 0;
		public static bool operator <=(MergeInOutPath left, MergeInOutPath right) => left.CompareTo(right) <= 0;
		public static bool operator >(MergeInOutPath left, MergeInOutPath right) => left.CompareTo(right) > 0;
		public static bool operator >=(MergeInOutPath left, MergeInOutPath right) => left.CompareTo(right) >= 0;

		int IComparable.CompareTo(object? obj) {
			if (obj is null) return 1;
			MergeInOutPath path = (MergeInOutPath)obj;
			return CompareTo(path);
		}

		int CompareTo(MergeInOutPath right) => string.Compare(output, right.output, StringComparison.OrdinalIgnoreCase);
	}

	public class SortOutputAscendingHelper : Comparer<MergeInOutPath> {
		public override int Compare(MergeInOutPath left, MergeInOutPath right) {
			return string.Compare(left.output, right.output, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SortOutputDescendingHelper : Comparer<MergeInOutPath> {
		public override int Compare(MergeInOutPath left, MergeInOutPath right) {
			return -string.Compare(left.output, right.output, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SortOutputThenInputAscendingHelper : Comparer<MergeInOutPath> {
		public override int Compare(MergeInOutPath left, MergeInOutPath right) {
			int comparison = string.Compare(left.output, right.output, StringComparison.OrdinalIgnoreCase);
			if (comparison == 0) {
				if (left.Equals(right)) {
					comparison = 0;
				} else {
					comparison = left.inputs.Length >= right.inputs.Length ? 1 : -1;
				}
			}
			return comparison;
		}
	}
}