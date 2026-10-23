namespace FolderScope;

public static class NaturalSort
{
    public static int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;

        var i = 0;
        var j = 0;
        while (i < left.Length && j < right.Length)
        {
            if (char.IsDigit(left[i]) && char.IsDigit(right[j]))
            {
                var cmp = CompareNumericRun(left, ref i, right, ref j);
                if (cmp != 0)
                    return cmp;
                continue;
            }

            var a = char.ToUpperInvariant(left[i]);
            var b = char.ToUpperInvariant(right[j]);
            if (a != b)
                return a.CompareTo(b);
            i++;
            j++;
        }

        if (i == left.Length && j == right.Length)
            return 0;

        return (left.Length - i).CompareTo(right.Length - j);
    }

    private static int CompareNumericRun(string left, ref int i, string right, ref int j)
    {
        var iStart = i;
        var jStart = j;
        while (i < left.Length && char.IsDigit(left[i]))
            i++;
        while (j < right.Length && char.IsDigit(right[j]))
            j++;

        var a = TrimZeros(left.AsSpan(iStart, i - iStart));
        var b = TrimZeros(right.AsSpan(jStart, j - jStart));
        var lengthCmp = a.Length.CompareTo(b.Length);
        if (lengthCmp != 0)
            return lengthCmp;

        return a.CompareTo(b, StringComparison.Ordinal);
    }

    private static ReadOnlySpan<char> TrimZeros(ReadOnlySpan<char> value)
    {
        var trimmed = value.TrimStart('0');
        return trimmed.Length == 0 ? "0".AsSpan() : trimmed;
    }
}
