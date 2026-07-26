using Depa.Datalog;
using Depa.Datalog.Cozo;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var datalogSource = """
calls("a", "b").
calls("b", "c").
reachable(X, Y) :- calls(X, Y).
reachable(X, Z) :- reachable(X, Y), calls(Y, Z).
?- reachable($start, Target), Target != "blocked".
""";

var parse = PortableDatalogParser.Parse(datalogSource);
Assert(parse.Success && parse.Program is not null, "Portable Datalog parser should parse facts, rules, recursive rules, parameters, and comparisons");
var parsedProgram = parse.Program!;
Assert(parsedProgram.Rules.Count == 4 && parsedProgram.Query is not null, "Portable Datalog parser should preserve rules and query");

var validation = new PortableDatalogValidator().Validate(parsedProgram);
Assert(validation.Success && validation.Program is not null, "Portable Datalog validator should accept safe recursive rules");
Assert(validation.Program!.Query!.Projection.Contains("Target"), "Portable Datalog normalized query should project variables from query atoms");

var unsafeParse = PortableDatalogParser.Parse("bad(X) :- calls(Y, Z). ?- bad(X).");
Assert(unsafeParse.Success && unsafeParse.Program is not null, "unsafe program should still parse");
var unsafeValidation = new PortableDatalogValidator().Validate(unsafeParse.Program!);
Assert(!unsafeValidation.Success && unsafeValidation.Diagnostics.Any(d => d.Code == "DL3001"), "validator should reject unsafe head variables");

var malformedParse = PortableDatalogParser.Parse("? calls(X).");
Assert(!malformedParse.Success && malformedParse.Diagnostics.Any(d => d.Code == "DL1001"), "parser should report malformed query markers without hanging");

var compilerSource = """
reachable(X, Y) :- calls(X, Y).
reachable(X, Z) :- reachable(X, Y), calls(Y, Z).
?- reachable($start, Target), Target != "blocked".
""";
var compilerValidation = new PortableDatalogValidator().ParseAndValidate(compilerSource);
Assert(compilerValidation.Success && compilerValidation.Program is not null, "compiler input should validate");
var compiler = new CozoDatalogCompiler();
var compileResult = compiler.Compile(
    compilerValidation.Program!,
    new CozoDatalogCompileOptions(
        StoredRelations: new Dictionary<string, CozoStoredRelationMapping>
        {
            ["calls"] = new("calls", "code_calls", ["from", "to"])
        },
        Parameters: new Dictionary<string, object?> { ["start"] = "a\"quoted\nvalue" }));
Assert(compileResult.Success, "Cozo compiler should compile supported Portable Datalog IR");
Assert(compileResult.Script.Contains("*code_calls{from: X, to: Y}", StringComparison.Ordinal), "Cozo compiler should map stored relations by named fields");
Assert(compileResult.Script.Contains("reachable[X, Z] := reachable[X, Y], *code_calls{from: Y, to: Z}", StringComparison.Ordinal), "Cozo compiler should compile recursive rules");
Assert(compileResult.Script.Contains("?[Target] := reachable[$start, Target], Target != \"blocked\"", StringComparison.Ordinal), "Cozo compiler should compile parameterized query without inlining parameter values");
Assert((string)compileResult.Parameters["start"]! == "a\"quoted\nvalue", "Cozo compiler should preserve parameter dictionary");

var unsupportedCompile = compiler.Compile(
    compilerValidation.Program!,
    new CozoDatalogCompileOptions(
        StoredRelations: new Dictionary<string, CozoStoredRelationMapping>
        {
            ["calls"] = new("calls", "code_calls", ["from"])
        }));
Assert(!unsupportedCompile.Success && unsupportedCompile.Script.Length == 0, "Cozo compiler should not emit partial executable script when backend diagnostics contain errors");

Console.WriteLine("Depa.Datalog tests passed.");
