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
                    if (result == null)
                    {
                        Title = string.Empty;
                        Subtitle = string.Empty;
                        Command = new NoOpCommand();
                    }
                    else
                    {
                        Title = $"{result.RateHumanized} {result.Symbol.ToUpperInvariant()}";
                        if (result.Date != null) Subtitle = string.Format(Resources.updated_at, result.Date);
                        Command = new CopyTextCommand(result.RateHumanized);
                    }
                },
                error =>
                {
                    Title = string.Empty;
                    Subtitle = string.Empty;
                    Command = new NoOpCommand();
                },
                () =>
                {
                },
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
        if (string.IsNullOrEmpty(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
        }

        // Simply pass the query to the reactive subject
        _querySubject.OnNext(query);
    }

    private async Task<CurrencyRate?> DoSearchAsync(string query, CancellationToken cancellationToken)
    {
        var input = Parser.Parse(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (input == null)
            return null;

        var targets = input.Target == null
            ? new[] { _settings.DefaultTargetCurrency.Value ?? "USD" }
            : new[] { input.Target };

        var result = await _converter.Exchange(Convert.ToDecimal(input.Value), input.Source, targets,
            cancellationToken);

        if (result == null) return null;

        cancellationToken.ThrowIfCancellationRequested();
        return result.Rates.FirstOrDefault();
    }
}