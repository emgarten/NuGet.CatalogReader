// Shared source file

using System.Text.RegularExpressions;

namespace Emgarten.Common
{
    internal static class PatternUtils
    {
        /// <summary>
        /// Wildcard pattern match
        /// </summary>
        internal static Regex WildcardToRegex(string pattern, bool ignoreCase)
        {
            if (pattern == null)
            {
                throw new System.ArgumentNullException(nameof(pattern));
            }

            var s = "^" + Regex.Escape(pattern).
                                Replace("\\*", ".*").
                                Replace("\\?", ".") + "$";

            return new Regex(s, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }
    }
}
