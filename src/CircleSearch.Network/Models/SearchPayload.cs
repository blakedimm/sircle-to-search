using System;

namespace CircleSearch.Network.Models;

public sealed record SearchPayload(
    byte[] ImageBytes,
    string MimeType = "image/bmp",
    string FileName = "capture.bmp",
    int Width = 0,
    int Height = 0
);