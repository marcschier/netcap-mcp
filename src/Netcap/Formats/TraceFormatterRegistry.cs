// Licensed under the Apache License, Version 2.0.

namespace Netcap.Formats;

using Netcap.Models;

/// <summary>
/// Registry that resolves an <see cref="ITraceFormatter"/> by
/// <see cref="FormatKind"/>.
/// </summary>
internal sealed class TraceFormatterRegistry
{
    private readonly Dictionary<FormatKind, ITraceFormatter> _formatters;

    public TraceFormatterRegistry(IEnumerable<ITraceFormatter> formatters)
    {
        _formatters = formatters.ToDictionary(f => f.Kind);
    }

    public ITraceFormatter Get(FormatKind kind)
    {
        if (!_formatters.TryGetValue(kind, out var formatter))
        {
            throw new NetcapException(
                $"No formatter registered for '{kind.ToWireName()}'.");
        }
        return formatter;
    }
}
