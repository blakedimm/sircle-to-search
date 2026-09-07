using System;

namespace CircleSearch.Network.Models;

public sealed record SearchResult(
    bool Success,
    string? LensUrl = null,
    int StatusCode = 0,
    string? ErrorMessage = null
)
{
    public static SearchResult Ok(string url, int statusCode = 200) =>
        new(true, url, statusCode);

    public static SearchResult Fail(string error, int statusCode = 0) =>
        new(false, null, statusCode, error);
}