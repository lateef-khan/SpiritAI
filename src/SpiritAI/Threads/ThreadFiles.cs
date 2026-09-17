using AgentCore.Application.Blobs;

namespace SpiritAI.Threads;

/// <summary>
/// The files a thread's replies produced, as the parts the browser restores them from.
/// </summary>
public static class ThreadFiles
{
    /// <summary>The part for each linked file, keyed by the name the model gave it.</summary>
    /// <param name="links">What <c>CallRepository.LinkFilesAsync</c> found for the thread.</param>
    /// <returns>One part per file that has a link. A file with none has nothing to draw.</returns>
    public static IReadOnlyDictionary<string, ThreadPart> PartsOf(IReadOnlyList<FileLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        Dictionary<string, ThreadPart> parts = new(StringComparer.Ordinal);

        foreach (var (blob, url) in links)
        {
            if (url is not null)
            {
                parts[blob.Name] = PartOf(blob, url);
            }
        }

        return parts;
    }

    /// <summary>The part for one linked file: its facts and the link, nothing decided.</summary>
    /// <param name="blob">The file's facts.</param>
    /// <param name="url">Where the browser fetches it from.</param>
    /// <returns>The part.</returns>
    public static ThreadFilePart PartOf(BlobRef blob, Uri url)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(url);

        return new ThreadFilePart(blob.Name, blob.MediaType, blob.Length, url.ToString());
    }
}
