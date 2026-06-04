// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace NetcapMcp.Formats;

using NetcapMcp.Models;

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
            throw new NetcapMcpException(
                $"No formatter registered for '{kind.ToWireName()}'.");
        }
        return formatter;
    }
}
