// Optional progress diagnostics are deliberately kept out of the package runtime.
// The migrated contract tests can enable them without coupling to production code.
internal static class HarnessDiagnostics
{
    public static void Start(string _) { }
    public static void Complete() { }
}
