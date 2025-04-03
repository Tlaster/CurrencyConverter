// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CurrencyConverter.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CurrencyConverter;

public partial class CurrencyConverterCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbackCommands;
    private readonly SettingsManager _settings = SettingsManager.Instance;

    public CurrencyConverterCommandsProvider()
    {
        DisplayName = Resources.app_name;
        Icon = new IconInfo("\ue8af");
        Settings = _settings.Settings;
        _commands =
        [
            new ListItem(new CurrencyConverterPage(_settings))
            {
                Title = DisplayName,
                Icon = new IconInfo("\ue8af"),
                Subtitle = Resources.app_desc
            }
        ];
        _fallbackCommands =
        [
            new FallbackCurrencyConverterItem(_settings)
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override IFallbackCommandItem[]? FallbackCommands()
    {
        return _fallbackCommands;
    }
}