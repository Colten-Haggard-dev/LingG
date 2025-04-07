using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingTools;

internal class GenerateAstTool : Tool
{
    private readonly string _interfaceSnippet =
        "    public interface IVisitor<T>\n" +
        "    {{\n" +
        "{0}" +
        "    }}\n";

    private readonly string _abstractSnippet =
        "public abstract class {0}\n" +
        "{{\n" +
        "{1}\n" +
        "{2}" +
        "    public abstract T Accept<T>(IVisitor<T> visitor);\n" +
        "}}";

    private readonly string _typeSnippet =
        "    public class {0}({1}) : {2}\n" +
        "    {{\n" +
        "{3}\n" +
        "        public override T Accept<T>(IVisitor<T> visitor)\n" +
        "        {{\n" +
        "            return visitor.Visit(this);\n" +
        "        }}\n" +
        "    }}\n\n";

    private readonly string _varSnippet = "        public readonly {0} {1} = {2};\n";

    private readonly string _visitorFuncSnippet = "        T Visit({0} {1});\n";

    public GenerateAstTool() : base("generate_ast")
    {

    }

    public override void Call(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: generate_ast <output path>");
            Environment.Exit(-1);
        }

        string outputPath = args[1];
        DefineAst(outputPath, "Expression",
            [
                "Assign : Token name, Expression value",
                "Binary : Expression left, Token op, Expression right",
                "Call : Expression callee, Token paren, List<Expression> arguments",
                "Get : Expression obj, Token name",
                "Grouping : Expression expression",
                "Literal  : object value",
                "Logical : Expression left, Token op, Expression right",
                "Set : Expression obj, Token name, Expression value",
                "Super : Token keyword, Token method",
                "This : Token keyword",
                "Unary : Token op, Expression right",
                "Variable : Token name"
            ]
        );

        Console.WriteLine();

        DefineAst(outputPath, "Statement",
            [
                "Block : List<Statement> statements",
                "Break : Token origin",
                "Class : Token name, Expression.Variable superclass, List<Statement.Function> methods",
                "Continue : Token origin",
                "ExpressionStatement : Expression expr",
                "Function : Token name, List<Token> parameters, List<Statement> body",
                "If : Expression condition, Statement thenBranch, Statement elseBranch",
                "Print : Expression expr",
                "Return : Token keyword, Expression value",
                "Variable : Token name, Expression initializer",
                "While : Expression condition, Statement body"
            ]
        );
    }

    private void DefineAst(string outputDir, string baseName, List<string> types)
    {
        string path = Path.Combine(outputDir, baseName + ".cs");
        string classCombine = "";
        string interfaceCombine = DefineVisitor(baseName, types);

        for (int i = 0; i < types.Count; ++i)
        {
            string type = types[i];
            string[] split = type.Split(":");
            string className = split[0].Trim();
            string fields = split[1].Trim();
            classCombine += DefineType(baseName, className, fields);
        }

        string fileContents = string.Format(_abstractSnippet, baseName, interfaceCombine, classCombine);
        File.WriteAllText(path, fileContents);
        Console.WriteLine(fileContents);
    }

    private string DefineType(string baseName, string className, string fields)
    {
        string[] variables = fields.Split(", ", StringSplitOptions.RemoveEmptyEntries);
        string varCombineString = "";

        for (int i = 0; i < variables.Length; ++i)
        {
            string variable = variables[i];
            string[] split = variable.Split(" ");
            varCombineString += string.Format(_varSnippet, split[0], $"{split[1][0].ToString().ToUpper()}{split[1][1..]}", split[1]);
        }

        return string.Format(_typeSnippet, className, fields, baseName, varCombineString);
    }

    private string DefineVisitor(string baseName, List<string> types)
    {
        string interfaceCombine = "";

        for (int i = 0; i < types.Count; ++i)
        {
            string type = types[i];
            string typeName = type.Split(":")[0].Trim();

            interfaceCombine += string.Format(_visitorFuncSnippet, typeName, baseName.ToLower());
        }

        return string.Format(_interfaceSnippet, interfaceCombine);
    }
}
