using Microsoft.Extensions.AI;
using System.Text.Json;                 // JsonSerializer

namespace NpcForge;

// Middleware. Every call passes through to the real client unchanged, and what the app sent
// and what came back are written to a file, one line per model call. Everything around it still
// sees one IChatClient, so neither ChatAgent nor AgentLoop knows it is there.
public sealed class RecordingChatClient : DelegatingChatClient
{
    private readonly string _path;
    private readonly string[] _flags;

    // False until the first reply is written. That write creates the file.
    private bool _started;

    // flags: this run's answers as command-line flags. They go on line 1, so the replay test can
    // make the same run again without anyone typing them twice. Nothing touches the disk here:
    // the file is made when the first reply arrives, so a run that stops before the model (a load,
    // a bad difficulty, a server that will not start) never empties the last good recording.
    public RecordingChatClient(IChatClient inner, string path, string[] flags) : base(inner)
    {
        _path = path;
        _flags = flags;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Taken before the call, not after. A tool that cannot be recorded stops the run here,
        // before anything is paid for. And the line holds what the app sent, whatever the client
        // does to the list or the options while it works.
        var request = RecordedRequest.From(messages, options);

        // The real model call. The base class hands it to the client this one wraps.
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // Written as soon as it arrives, so a run that fails later still keeps what it paid for.
        var line = JsonSerializer.Serialize(new RecordedCall(request, response), Recording.Json) + "\n";

        if (_started)
        {
            File.AppendAllText(_path, line);
        }
        else
        {
            // The first reply replaces any old recording: flags on line 1, then this call.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_flags, Recording.Json) + "\n" + line);
            _started = true;
        }

        return response;
    }
}
