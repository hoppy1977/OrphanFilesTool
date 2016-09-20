using System;

namespace OrphanFilesTool
{
	public static class Extensions
	{
		public static bool Contains(this string target, string value, StringComparison comparison)
		{
			return target.IndexOf(value, comparison) >= 0;
		}
	}
}
