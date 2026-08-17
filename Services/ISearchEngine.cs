using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreSearch.Models;

namespace CoreSearch.Services;

public interface ISearchEngine
{
    Task SearchAsync(
        SearchOptions options,
        IProgress<SearchResult> progress,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SearchResult> SearchStreamAsync(
        SearchOptions options,
        CancellationToken cancellationToken = default);
}
