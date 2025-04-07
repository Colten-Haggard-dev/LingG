using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingTools;

internal class ToolBelt
{
    public static ToolBelt Instance { get; private set; } = new();

    public readonly Dictionary<string, Tool> Tools = [];

    private ToolBelt()
    {

    }

    public void Call(string[] args)
    {
        Tools[args[0]].Call(args);
    }
}
