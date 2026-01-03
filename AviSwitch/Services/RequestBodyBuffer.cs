namespace AviSwitch.Services;

public sealed class BufferedRequestBody
{
    public BufferedRequestBody(byte[]? body, bool canRetry)
    {
        Body = body;
        CanRetry = canRetry;
    }

    public byte[]? Body { get; }
    public bool CanRetry { get; }
}

public static class RequestBodyBuffer
{
    public static async Task<BufferedRequestBody> TryBufferAsync(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (!request.ContentLength.HasValue)
        {
            if (!request.Headers.ContainsKey("Transfer-Encoding"))
            {
                return new BufferedRequestBody(Array.Empty<byte>(), true);
            }

            return new BufferedRequestBody(null, false);
        }

        if (request.ContentLength.Value == 0)
        {
            return new BufferedRequestBody(Array.Empty<byte>(), true);
        }

        if (request.ContentLength.Value > maxBytes)
        {
            return new BufferedRequestBody(null, false);
        }

        using var ms = new MemoryStream((int)request.ContentLength.Value);
        await request.Body.CopyToAsync(ms, cancellationToken);
        return new BufferedRequestBody(ms.ToArray(), true);
    }
}
