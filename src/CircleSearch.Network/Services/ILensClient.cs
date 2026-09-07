using System;
using System.Threading;
using System.Threading.Tasks;
using CircleSearch.Network.Models;

namespace CircleSearch.Network.Services;

public interface ILensClient : IDisposable
{
    Task<SearchResult> UploadAndSearchAsync(SearchPayload payload, CancellationToken cancellationToken = default);
}