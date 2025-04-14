using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CurrencyConverter.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CurrencyConverter;

internal sealed partial class FallbackCurrencyConverterItem : FallbackCommandItem, IDisposable
{
    // For resource management
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Converter _converter = new();

    // Subject to publish query changes
    private readonly Subject<string> _querySubject = new();
    private readonly SettingsManager _settings;

    public FallbackCurrencyConverterItem(SettingsManager settings) : base(new NoOpCommand(), Resources.app_name)
    {
        Title = string.Empty;
        Subtitle = Resources.app_desc;
        Icon = new IconInfo("\ue8af");
        _settings = settings;

        // Configure the reactive query pipeline
        _querySubject
            .AsObservable()
            .DistinctUntilChanged() // Ignore duplicate queries
            .Throttle(TimeSpan.FromMilliseconds(300)) // Debounce - wait for user to stop typing
            .Select(query => Observable.FromAsync(ct => DoSearchAsync(query, ct)))
            .Switch() // Cancel previous search when a new one comes in
            .Subscribe(
                result =>
                {
                    Title = result.Title;
                    if (result.Subtitle != null) Subtitle = result.Subtitle;
                    if (!string.IsNullOrEmpty(result.Title))
                        Command = new CopyTextCommand(result.Title);
                    else
                        Command = new NoOpCommand();
                },
                error =>
                {
                    Title = string.Empty;
                    Subtitle = string.Empty;
                    Command = new NoOpCommand();
                },
                () => { },
                _cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _querySubject.Dispose();
        _converter.Dispose();
    }

    public override void UpdateQuery(string query)
    {
        _querySubject.OnNext(query);
        if (query.Length > 0 && char.IsDigit(query[0]))
        {
            Title = query;
        } else
        {
            Title = string.Empty;
        }
    }

    private async Task<UiData> DoSearchAsync(string query, CancellationToken cancellationToken)
    {
        if (query.Length > 0 && char.IsDigit(query[0]))
        {
            var input = Parser.Parse(query);
            cancellationToken.ThrowIfCancellationRequested();

            if (input?.Value == null)
                return new UiData(query, Resources.app_desc);

            var targets = input.Target == null
                ? new[] { _settings.DefaultTargetCurrency.Value ?? "USD" }
                : new[] { input.Target };

            var result = await _converter.Exchange(Convert.ToDecimal(input.Value), input.Source, targets,
                cancellationToken);

            if (result == null) return new UiData(query, Resources.app_desc);

            cancellationToken.ThrowIfCancellationRequested();
            var item = result.Rates.FirstOrDefault();
            if (item == null) return new UiData(query, Resources.app_desc);
            return new UiData($"{item.RateHumanized} {item.Symbol.ToUpperInvariant()}",
                string.Format(Resources.updated_at, item.Date));
        }

        return new UiData(string.Empty, string.Empty);
    }
}

internal record UiData(string Title, string? Subtitle);