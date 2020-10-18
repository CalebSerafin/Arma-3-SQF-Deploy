// <copyright file="Program.cs" company="Caleb Sebastian Serafin">
// Copyright (c) 2020 Caleb Sebastian Serafin. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the repository root for full license information.
// </copyright>

using System;
using System.Collections.Generic;

namespace A3SD_File_Worker_InOutPath {
	public struct InOutPath : IComparable {
		public string input, output;
		public InOutPath(string? input, string? output) {
			this.input = input ?? "";
			this.output = output ?? "";
		}

		public override int GetHashCode() => HashCode.Combine(input, output);
		public override string? ToString() => $"'{input}' '{output}'";
		public bool Equals(InOutPath inOutPath) => input.Equals(inOutPath.input) && output.Equals(inOutPath.output);
		public override bool Equals(object? obj) => obj is InOutPath && Equals(obj);
		public static bool operator ==(InOutPath left, InOutPath right) => left.Equals(right);
		public static bool operator !=(InOutPath left, InOutPath right) => !(left == right);
		public static bool operator <(InOutPath left, InOutPath right) => left.CompareTo(right) < 0;
		public static bool operator <=(InOutPath left, InOutPath right) => left.CompareTo(right) <= 0;
		public static bool operator >(InOutPath left, InOutPath right) => left.CompareTo(right) > 0;
		public static bool operator >=(InOutPath left, InOutPath right) => left.CompareTo(right) >= 0;
		int CompareTo(InOutPath right) => string.Compare(output, right.output, StringComparison.OrdinalIgnoreCase);

		int IComparable.CompareTo(object? obj) {
			if (obj is null) return 1;
			InOutPath path = (InOutPath)obj;
			return CompareTo(path);
		}
	}

	public class SortInputAscendingHelper : Comparer<InOutPath> {
		public override int Compare(InOutPath left, InOutPath right) {
			return string.Compare(left.input, right.input, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SortInputDescendingHelper : Comparer<InOutPath> {
		public override int Compare(InOutPath left, InOutPath right) {
			return -string.Compare(left.input, right.input, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SortOutputAscendingHelper : Comparer<InOutPath> {
		public override int Compare(InOutPath left, InOutPath right) {
			return string.Compare(left.output, right.output, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SortOutputDescendingHelper : Comparer<InOutPath> {
		public override int Compare(InOutPath left, InOutPath right) {
			return -string.Compare(left.output, right.output, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SortOutputThenInputAscendingHelper : Comparer<InOutPath> {
		public override int Compare(InOutPath left, InOutPath right) {
			int comparison = string.Compare(left.output, right.output, StringComparison.OrdinalIgnoreCase);
			return comparison == 0 ? string.Compare(left.input, right.input, StringComparison.OrdinalIgnoreCase) : comparison;
		}
	}
}