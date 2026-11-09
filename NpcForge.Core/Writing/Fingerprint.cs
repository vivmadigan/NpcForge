using System.Security.Cryptography;     // SHA256
using System.Text;                      // Encoding

namespace NpcForge;

public static class Fingerprint
{
    // The short hash in every [skill] line. The count beside it is the readable half: 2097 to 2239
    // says it grew by 142. The hash is the reliable half, because two same-length edits have the
    // same count and different hashes, and telling skill versions apart is the whole premise of
    // docs/runs. Hash what the model is sent, not the files on disk, so a BOM or line-ending change
    // on its own does not read as a different skill.
    public static string Of(string sent) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sent)))[..8].ToLowerInvariant();
}
