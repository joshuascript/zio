// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using System.Text;
using System.Text.RegularExpressions;

namespace Zio;

/// <summary>
/// Filter pattern compiler used for <see cref="Zio.IFileSystemWatcher"/> implementation.
/// Use the method <see cref="Parse(string)"/> to create a pattern.
/// </summary>
public readonly struct FilterPattern
{
    private static readonly char[] SpecialChars = {'.', '*', '?'};

    private readonly string? _exactMatch;
    private readonly Regex? _regexMatch;

    private readonly StringComparison _comparisonType;

    public static FilterPattern Parse(string filter)
    {
        return Parse(filter, true);
    }

    /// <summary>
    /// Parses the specified filter (e.g. <c>*.txt</c>) to match file names by pattern.
    /// </summary>
    /// <param name="filter">The filter to parse (e.g. <c>*.txt</c>).</param>
    /// <param name="isCaseSensitive">Specifies if file names should be matched case-sensitively.</param>
    /// <returns>A <see cref="FilterPattern"/> instance in order to use <see cref="Match(string)"/> on a file name.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static FilterPattern Parse(string filter, bool isCaseSensitive)
    {
        return new FilterPattern(filter, isCaseSensitive);
    }
    
    /// <summary>
    /// Tries to match the specified path with this instance.
    /// </summary>
    /// <param name="path">The path to match.</param>
    /// <returns><c>true</c> if the path was matched, <c>false</c> otherwise.</returns>
    public bool Match(UPath path)
    {
        path.AssertNotNull();
        var name = path.GetName();
        // if _execMatch is null and _regexMatch is null, we have a * match
        return _exactMatch != null ? string.Equals(_exactMatch, name, _comparisonType) : _regexMatch is null || _regexMatch.IsMatch(name);
    }

    /// <summary>
    /// Tries to match the specified file name with this instance.
    /// </summary>
    /// <param name="fileName">The file name to match.</param>
    /// <returns><c>true</c> if the file name was matched, <c>false</c> otherwise.</returns>
    public bool Match(string fileName)
    {
        if (fileName is null) throw new ArgumentNullException(nameof(fileName));
        // if _execMatch is null and _regexMatch is null, we have a * match
        return _exactMatch != null ? string.Equals(_exactMatch, fileName, _comparisonType) : _regexMatch is null || _regexMatch.IsMatch(fileName);
    }

    public FilterPattern(string filter)
    : this(filter, true)
    {
    }

    /// <summary>
    /// Initializes an instance of the filter pattern struct from a specified filter (e.g. <c>*.txt</c>).
    /// </summary>
    /// <param name="filter">The filter to parse (e.g. <c>*.txt</c>).</param>
    /// <param name="isCaseSensitive">Specifies if file names should be matched case-sensitively.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public FilterPattern(string filter, bool isCaseSensitive)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (filter.IndexOf(UPath.DirectorySeparator) >= 0)
        {
            throw new ArgumentException("Filter cannot contain directory parts.", nameof(filter));
        }

        _exactMatch = null;
        _regexMatch = null;
        _comparisonType = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Optimized path, most common cases
        if (filter is "" or "*" or "*.*")
        {
            return;
        }

        bool appendSpecialCaseForWildcardExt = false;
        var startIndex = 0;
        StringBuilder? builder = null;

        try
        {
            int nextIndex;
            while ((nextIndex = filter.IndexOfAny(SpecialChars, startIndex)) >= 0)
            {
                if (builder is null)
                {
                    builder = UPath.GetSharedStringBuilder();
                    builder.Append("^");
                }

                var lengthToEscape = nextIndex - startIndex;
                if (lengthToEscape > 0)
                {
                    var toEscape = Regex.Escape(filter.Substring(startIndex, lengthToEscape));
                    builder.Append(toEscape);
                }

                var c = filter[nextIndex];

                // special case for wildcard file extension to allow blank extensions as well
                if (c == '.' && nextIndex == filter.Length - 2 && filter[nextIndex + 1] == '*')
                {
                    appendSpecialCaseForWildcardExt = true;
                    break;
                }

                var regexPatternPart =
                    c == '.' ? "\\." : c == '*' ? ".*?" : ".";
                builder.Append(regexPatternPart);

                startIndex = nextIndex + 1;
            }
            if (builder is null)
            {
                _exactMatch = filter;
            }
            else
            {
                if (appendSpecialCaseForWildcardExt)
                {
                    builder.Append("(\\.[^.]*)?");
                }
                else
                {
                    var length = filter.Length - startIndex;
                    if (length > 0)
                    {
                        var toEscape = Regex.Escape(filter.Substring(startIndex, length));
                        builder.Append(toEscape);
                    }
                }

                builder.Append("$");

                var regexPattern = builder.ToString();
                _regexMatch = isCaseSensitive
                    ? new Regex(regexPattern)
                    : new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }
        finally
        {
            if (builder != null)
            {
                builder.Length = 0;
            }
        }
    }
}
