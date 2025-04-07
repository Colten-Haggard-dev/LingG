// See https://aka.ms/new-console-template for more information

using LingTools;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: <tool> <args>");
    Environment.Exit(-1);
}

GenerateAstTool generateAstTool = new();

ToolBelt.Instance.Tools[generateAstTool.ToolName] = generateAstTool;

ToolBelt.Instance.Call(args);