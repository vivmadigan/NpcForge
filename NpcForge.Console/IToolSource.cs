using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace NpcForge
{
    // The seam between the loop and wherever tools come from. The loop needs exactly two
    // things from tools: what exists, and run this one. Step 4 implements it with a fake,
    // step 5 with MCP, and the loop does not change between the two. That is its whole job.
    public interface IToolSource
    {
        // Everything the model is allowed to ask for. Goes straight into ChatOptions.Tools.
        Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct);

        // Run one call the model asked for and hand back the result as text. Failures come
        // back as text too, never as an exception: the model can recover from "Tool failed".
        Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct);
    }
}
