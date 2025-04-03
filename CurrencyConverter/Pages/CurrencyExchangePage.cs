using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CurrencyConverter.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CurrencyConverter;

internal sealed partial class CurrencyConverterPage : DynamicListPage, IDisposable
{
    // For resource management
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Converter _converter = new();

    // top 5 from https://coinmarketcap.com/tokens/
    private readonly string[] _popularCrypto = ["BTC", "ETH", "USDT", "XRP", "BNB"];

    // top 5 from https://en.wikipedia.org/wiki/Template:Most_traded_currencies
    private readonly string[] _popularCurrencies = ["USD", "EUR", "JPY", "GBP", "CNY"];

    // Subject to publish search text changes
    private readonly Subject<string> _searchTextSubject = new();
    private readonly SettingsManager _settings;

    // Store current results
    private IReadOnlyList<ListItem> _results = Array.Empty<ListItem>();

    public CurrencyConverterPage(SettingsManager settings)
    {
        Title = Resources.app_name;
        Icon = new IconInfo("\ue8af");
        _settings = settings;

        // Configure the reactive search pipeline
        _searchTextSubject
            .AsObservable()
            .DistinctUntilChanged() // Ignore duplicate search requests
            .Throttle(TimeSpan.FromMilliseconds(300)) // Debounce - wait for user to stop typing
            .Select(searchText => Observable.FromAsync(ct => ProcessSearchAsync(searchText, ct)))
            .Switch() // Cancel previous search when a new one comes in
            .Subscribe(
                results =>
                {
                    _results = results;
                    IsLoading = false;
                    RaiseItemsChanged(_results.Count);
                    if (_results.Count == 0) Title = Resources.app_name;
                },
                error =>
                {
                    Title = Resources.app_name;
                    // Handle errors
                    IsLoading = false;
                    _results = Array.Empty<ListItem>();
                    RaiseItemsChanged(0);
                },
                () =>
                {
                    // Handle completion
                    IsLoading = false;
                },
                _cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _searchTextSubject.Dispose();
        _converter.Dispose();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch == oldSearch) return;

        IsLoading = true;
        _searchTextSubject.OnNext(newSearch);
    }

    private async Task<IReadOnlyList<ListItem>> ProcessSearchAsync(string searchText,
        CancellationToken cancellationToken)
    {
        var input = Parser.Parse(searchText);
        cancellationToken.ThrowIfCancellationRequested();
        if (input == null) return Array.Empty<ListItem>();

        var targets = new List<string>();
        if (input.Target != null) targets.Add(input.Target);

        targets.AddRange(_popularCurrencies.Where(x => !x.Equals(input.Source, StringComparison.OrdinalIgnoreCase)
                                                       && !x.Equals(input.Target, StringComparison.OrdinalIgnoreCase)));
        targets.AddRange(_popularCrypto.Where(x => !x.Equals(input.Source, StringComparison.OrdinalIgnoreCase)
                                                   && !x.Equals(input.Target, StringComparison.OrdinalIgnoreCase)));

        var result = await _converter.Exchange(Convert.ToDecimal(input.Value), input.Source, targets.ToArray(),
            cancellationToken);

        if (result == null) return Array.Empty<ListItem>();

        cancellationToken.ThrowIfCancellationRequested();

        var items = result.Rates
            .Select(x => new ListItem(new CopyTextCommand(x.RateHumanized))
            {
                Title = $"{x.RateHumanized} {x.Symbol.ToUpperInvariant()}"
            }).ToArray();
        Title = string.Format(Resources.updated_at, result.Date);
        return items;
    }

    public override IListItem[] GetItems()
    {
        return _results.ToArray<IListItem>();
    }
}