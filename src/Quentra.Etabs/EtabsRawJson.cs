using System.Security.Cryptography;
using System.Text;
using Quentra.Infrastructure;

namespace Quentra.Etabs;

// Raw captures use the same strict JSON conventions as snapshots and runs: camelCase, string enums,
// unknown or repeated properties rejected. The hash is of the exact bytes written to the file.
public static class EtabsRawJson
{
    public static string Serialize(EtabsRawModel raw) => TakeoffJson.Serialize(raw);

    public static EtabsRawModel Parse(string json)
    {
        var raw = TakeoffJson.Parse<EtabsRawModel>(json);
        if (raw.Format != EtabsRawModel.CurrentFormat)
            throw new InvalidDataException($"This is not a Quentra ETABS raw capture (format '{raw.Format}', expected {EtabsRawModel.CurrentFormat}).");
        return raw;
    }

    public static string Sha256(EtabsRawModel raw) =>
        Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(Serialize(raw)))).ToLowerInvariant();
}
