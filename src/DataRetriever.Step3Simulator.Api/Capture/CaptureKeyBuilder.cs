using System.Security.Cryptography;
using System.Text;

namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed class CaptureKeyBuilder
{
    public string Build(string method, string pathAndQuery, byte[] requestBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(pathAndQuery);
        ArgumentNullException.ThrowIfNull(requestBody);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, method);
        Append(hash, "\n");
        Append(hash, pathAndQuery);
        Append(hash, "\n");
        hash.AppendData(requestBody);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
    }
}
