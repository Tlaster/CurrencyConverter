using System;
using System.Threading;
using System.Threading.Tasks;

namespace CurrencyConverter;

/// <summary>
///     Asynchronous search handler for processing search text changes and managing related async operations
/// </summary>
/// <typeparam name="TResult">Type of the search results</typeparam>
public class AsyncSearchHandler<TResult> : IDisposable
{
    private readonly Action<TResult> _onResultsChanged;
    private readonly Lock _resultLock = new();
    private readonly Func<string, CancellationToken, Task<TResult>> _searchFunction;
    private readonly Lock _searchLock = new();
    private readonly Action<bool> _setLoadingState;
    private CancellationTokenSource? _cancellationTokenSource;
    private TResult _results;

    /// <summary>
    ///     Creates a new instance of the asynchronous search handler
    /// </summary>
    /// <param name="searchFunction">Function that performs the actual search operation</param>
    /// <param name="onResultsChanged">Callback invoked when results change</param>
    /// <param name="setLoadingState">Callback to set the loading state</param>
    /// <param name="initialResults">Initial result value</param>
    public AsyncSearchHandler(
        Func<string, CancellationToken, Task<TResult>> searchFunction,
        Action<TResult> onResultsChanged,
        Action<bool> setLoadingState,
        TResult initialResults)
    {
        _searchFunction = searchFunction ?? throw new ArgumentNullException(nameof(searchFunction));
        _onResultsChanged = onResultsChanged ?? throw new ArgumentNullException(nameof(onResultsChanged));
        _setLoadingState = setLoadingState ?? throw new ArgumentNullException(nameof(setLoadingState));
        _results = initialResults;
    }

    /// <summary>
    ///     Releases resources
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Updates the search text and executes the search
    /// </summary>
    /// <param name="oldSearch">Previous search text</param>
    /// <param name="newSearch">New search text</param>
    public void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch == oldSearch) return;

        if (string.IsNullOrEmpty(newSearch))
        {
            lock (_resultLock)
            {
                _results = default!;
            }

            _onResultsChanged(_results);
            return;
        }

        _ = Task.Run(async () =>
        {
            CancellationTokenSource? oldCts, currentCts;
            lock (_searchLock)
            {
                oldCts = _cancellationTokenSource;
                currentCts = _cancellationTokenSource = new CancellationTokenSource();
            }

            _setLoadingState(true);
            oldCts?.Cancel();

            var task = Task.Run(
                () =>
                {
                    // Check if already canceled
                    currentCts.Token.ThrowIfCancellationRequested();
                    return _searchFunction(newSearch, currentCts.Token);
                },
                currentCts.Token);

            try
            {
                var results = await task;
                lock (_resultLock)
                {
                    _results = results;
                }

                _setLoadingState(false);
                _onResultsChanged(_results);
            }
            catch (OperationCanceledException)
            {
                // Search was canceled, no action needed
            }
            catch (Exception e)
            {
                _setLoadingState(false);
                // Exception handling logic can be added here
            }
            finally
            {
                lock (_searchLock)
                {
                    currentCts?.Dispose();
                }
            }
        });
    }

    /// <summary>
    ///     Gets the current search results
    /// </summary>
    /// <returns>Current search results</returns>
    public TResult GetResults()
    {
        lock (_resultLock)
        {
            return _results;
        }
    }
}