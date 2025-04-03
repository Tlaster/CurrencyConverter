// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace CurrencyConverter;

[Guid("84494d38-93ca-4ca5-ae03-69904afa1e28")]
public sealed partial class CurrencyConverter : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly CurrencyConverterCommandsProvider _provider = new();

    public CurrencyConverter(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null
        };
    }

    public void Dispose()
    {
        _extensionDisposedEvent.Set();
    }
}