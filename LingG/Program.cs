// See https://aka.ms/new-console-template for more information
using LingG;

Console.WriteLine("Weclome to LingG...");

if (args.Length < 1)
{
    Console.WriteLine("Usage: LingG <script>");
}

Console.WriteLine("Running script " + args[0] + "...");

LingRuntime.RunFile(args[0]);

Console.WriteLine("Finished running script.");