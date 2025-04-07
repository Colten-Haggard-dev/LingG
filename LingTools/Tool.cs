using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingTools;

internal abstract class Tool(string name)
{
    public string ToolName { get; private set; } = name;

    public abstract void Call(string[] args);
}