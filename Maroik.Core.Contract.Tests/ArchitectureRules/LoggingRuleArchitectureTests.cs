using System.Text.RegularExpressions;
using Maroik.Core.Contract.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// The logging rules of CLAUDE.md ("Logging — written and tested with the code") that a syntax tree can decide, checked over
/// every production source (the <c>Maroik.Core.PostgreSQL</c> scaffold aside) with the Roslyn parser:
/// <list type="number">
///   <item>Every <c>catch</c> logs, rethrows, or carries a comment saying why it may be silent (an expected condition turned
///         into a result, a shutdown cancellation).</item>
///   <item>A silent <c>catch</c> does not swallow every exception: it names an exception type more specific than
///         <c>Exception</c>, filters with <c>when</c>, or hands the exception on (a health check's
///         <c>HealthCheckResult.Unhealthy(..., ex)</c>). A comment cannot excuse dropping any exception unseen.</item>
///   <item>An <c>Error</c> / <c>Critical</c> log inside a <c>catch</c> that names its exception passes that exception as the
///         first argument, so the stack trace is kept.</item>
///   <item>No log call passes the caught exception's <c>Message</c> (the exception object is logged instead).</item>
///   <item>No log template placeholder is named for a secret (password, token, hash, secret, connection string, cookie),
///         and no value passed to a log is a variable or member named for one (<c>account.HashedPassword</c>).</item>
///   <item><c>Maroik.Core.Repository</c> does not log: the exception propagates to the service that owns the transaction.</item>
/// </list>
/// The template shape itself (constant template, matching PascalCase placeholders) is a compiler error already (CA2254,
/// CA2017, CA1727), and <c>Maroik.Core.Domain</c>'s ban on logging is <c>DomainArchitectureTests.Domain_ShouldNot_Log</c>.
/// What a log must say, at which level, and that it is there at all, stays with each use case's own tests.
/// </summary>
public class LoggingRuleArchitectureTests
{
    /// <summary>
    /// A placeholder named for a secret: its name ends in the secret's noun ({Password}, {NewPassword}, {ResetPasswordToken},
    /// {PasswordHash}, {ConnectionString}, ...). A name that only mentions one ({PasswordReset}: whether a reset happened) is a
    /// fact about the secret, not the secret, and passes.
    /// </summary>
    private static readonly Regex _secretPlaceholder = new(
        @"\{@?[A-Za-z0-9]*(Password|Token|Secret|Hash|ConnectionString|Cookie)(,[^}]*)?(:[^}]*)?\}",
        RegexOptions.IgnoreCase);

    /// <summary>The nouns a secret's name ends in.</summary>
    private static readonly string[] _secretNouns = ["Password", "Token", "Secret", "Hash", "ConnectionString", "Cookie"];

    /// <summary>The <c>ILogger</c> extension methods and Serilog <c>Log.*</c> methods that write at Error or Critical / Fatal.</summary>
    private static readonly HashSet<string> _errorLevelMethods = ["LogError", "LogCritical", "Error", "Fatal"];

    /// <summary>Every production source file, parsed once.</summary>
    private static readonly Lazy<IReadOnlyList<SyntaxTree>> _productionSources = new(LoadProductionSources);

    [Fact]
    public void EveryCatch_LogsOrRethrows_OrSaysWhyItIsSilent()
    {
        AssertNone(_productionSources.Value.SelectMany(SilentCatches));
    }

    [Fact]
    public void ASilentCatch_IsNarrow()
    {
        AssertNone(_productionSources.Value.SelectMany(BroadSilentCatches));
    }

    [Fact]
    public void AnErrorLogInACatch_PassesTheCaughtException()
    {
        AssertNone(_productionSources.Value.SelectMany(ErrorLogsWithoutTheException));
    }

    [Fact]
    public void NoLog_PassesTheCaughtExceptionsMessage()
    {
        AssertNone(_productionSources.Value.SelectMany(LoggedExceptionMessages));
    }

    [Fact]
    public void NoLogTemplate_NamesASecret()
    {
        AssertNone(_productionSources.Value.SelectMany(SecretPlaceholders));
    }

    [Fact]
    public void NoLog_PassesASecretValue()
    {
        AssertNone(_productionSources.Value.SelectMany(SecretValues));
    }

    [Fact]
    public void TheRepositoryLayer_DoesNotLog()
    {
        string[] loggers =
        [
            .. _productionSources.Value
                .Where(tree => tree.FilePath.StartsWith("Maroik.Core.Repository" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Where(name => name.Identifier.ValueText is "ILogger" or "ILoggerFactory" or "Log")
                    .Select(name => Where(name))),
        ];

        Assert.Empty(loggers);
        Assert.Contains(_productionSources.Value, tree => tree.FilePath.StartsWith("Maroik.Core.Repository", StringComparison.Ordinal));
    }

    /// <summary>
    /// Each rule fires on the code it forbids and stays quiet on the code it allows. Without this, a reader that silently found
    /// nothing would pass every rule above.
    /// </summary>
    [Fact]
    public void TheRules_FireOnWhatTheyForbid_AndOnlyOnThat()
    {
        SyntaxTree bad = Parse("Sample/Bad.cs", """
            class Bad
            {
                void Swallows() { try { } catch (Exception) { } }
                void LosesTheStack(ILogger logger) { try { } catch (Exception ex) { logger.LogError("Failed {Id}", 1); } }
                void LogsTheMessage(ILogger logger) { try { } catch (Exception ex) { logger.LogWarning(ex, "Failed {Reason}", ex.Message); } }
                void LeaksASecret(ILogger logger) { logger.LogInformation("Reset {Email} with {ResetPasswordToken}", "a", "b"); }
                void Nested(ILogger logger) { try { } catch (Exception ex) { try { } catch (Exception inner) { logger.LogError(ex, "x"); } } }
                void SwallowsAll() { try { } catch { /* commented, still everything */ } }
                void DropsIt() { try { } catch (Exception ex) { /* commented, ex unused */ } }
                void LogsAHash(ILogger logger, Account account) { logger.LogInformation("Hash of {Email}: {Value}", account.Email, account.HashedPassword); }
            }
            """);
        SyntaxTree good = Parse("Sample/Good.cs", """
            class Good
            {
                void Logs(ILogger logger) { try { } catch (Exception ex) { logger.LogError(ex, "Failed {Id}", 1); } }
                void Rethrows() { try { } catch (Exception) { throw; } }
                void Wraps() { try { } catch (Exception ex) { throw new InvalidOperationException("x", ex); } }
                void Expected() { try { } catch (OperationCanceledException) { /* shutting down: nothing to report */ } }
                void SerilogStatic() { try { } catch (Exception ex) { Log.Fatal(ex, "Host terminated"); } }
                void Names(ILogger logger) { logger.LogInformation("Locked {Email} after {Attempts}", "a", 3); }
                void AFact(ILogger logger) { logger.LogInformation("Updated {Email}, password reset {PasswordReset}", "a", true); }
                void CommentBefore() { try { }
                    // Not valid input: no result to give.
                    catch (ArgumentException) { } }
                void Nested(ILogger logger) { try { } catch (Exception ex) { try { } catch (Exception inner) { logger.LogError(inner, "x"); } } }
                void Filtered() { try { } catch (Exception ex) when (ex is IOException) { /* expected: retried below */ } }
                void Bool(ILogger logger, string? newHashedPassword) { logger.LogInformation("Reset {PasswordReset}", newHashedPassword != null); }
                HealthCheckResult Health() { try { return HealthCheckResult.Healthy(); } catch (Exception ex) { /* the check reports it */ return HealthCheckResult.Unhealthy("down", ex); } }
                void Optional(ILogger? logger) { try { } catch (Exception e) { logger?.LogWarning(e, "Cache unavailable"); } }
            }
            """);

        Assert.Equal(["Sample/Bad.cs:3"], SilentCatches(bad));
        Assert.Equal(["Sample/Bad.cs:3", "Sample/Bad.cs:8", "Sample/Bad.cs:9"], BroadSilentCatches(bad));
        Assert.Equal(["Sample/Bad.cs:10 account.HashedPassword"], SecretValues(bad));
        Assert.Equal(["Sample/Bad.cs:4", "Sample/Bad.cs:7"], ErrorLogsWithoutTheException(bad));
        Assert.Equal(["Sample/Bad.cs:5"], LoggedExceptionMessages(bad));
        Assert.Equal(["Sample/Bad.cs:6 {ResetPasswordToken}"], SecretPlaceholders(bad));
        Assert.Empty(SilentCatches(good));
        Assert.Empty(BroadSilentCatches(good));
        Assert.Empty(SecretValues(good));
        Assert.Empty(ErrorLogsWithoutTheException(good));
        Assert.Empty(LoggedExceptionMessages(good));
        Assert.Empty(SecretPlaceholders(good));
        Assert.Contains(_productionSources.Value,
            tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<CatchClauseSyntax>().Any());
    }

    /// <summary>Fails listing every violation, one per line (Assert.Empty would cut the list short).</summary>
    private static void AssertNone(IEnumerable<string> violations)
    {
        string[] all = [.. violations];
        Assert.True(all.Length == 0, string.Join(Environment.NewLine, all));
    }

    /// <summary>Catches that neither throw, nor log, nor carry a comment (in the block, or just before <c>catch</c>).</summary>
    private static IEnumerable<string> SilentCatches(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<CatchClauseSyntax>()
            .Where(c => !Throws(c.Block) && !LogCalls(c.Block).Any() && !HasComment(c))
            .Select(c => Where(c));

    /// <summary>
    /// Catches that neither throw nor log, catch everything (no exception type, or <c>Exception</c> itself, with no
    /// <c>when</c> filter), and do not hand the exception on (its variable is never used in the block).
    /// </summary>
    private static IEnumerable<string> BroadSilentCatches(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<CatchClauseSyntax>()
            .Where(c => !Throws(c.Block) && !LogCalls(c.Block).Any())
            .Where(c => c.Filter == null && (c.Declaration == null || c.Declaration.Type.ToString() is "Exception" or "System.Exception"))
            .Where(c => !UsesTheException(c))
            .Select(c => Where(c));

    /// <summary>True when <paramref name="catchClause"/> names its exception and its block uses that variable.</summary>
    private static bool UsesTheException(CatchClauseSyntax catchClause) =>
        catchClause.Declaration?.Identifier.ValueText is { Length: > 0 } exception
        && catchClause.Block.DescendantNodes().OfType<IdentifierNameSyntax>().Any(name => name.Identifier.ValueText == exception);

    /// <summary>Values passed to a log call that are a variable or member named for a secret (the name ends in its noun).</summary>
    private static IEnumerable<string> SecretValues(SyntaxTree tree) =>
        from call in LogCalls(tree.GetRoot())
        from argument in call.ArgumentList.Arguments
        let name = argument.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        }
        where name != null && _secretNouns.Any(noun => name.EndsWith(noun, StringComparison.OrdinalIgnoreCase))
        select $"{Where(call)} {argument.Expression}";

    /// <summary>
    /// Error / Critical logs whose nearest enclosing catch names its exception, and whose first argument is not that exception.
    /// </summary>
    private static IEnumerable<string> ErrorLogsWithoutTheException(SyntaxTree tree) =>
        from call in LogCalls(tree.GetRoot())
        let exception = CaughtException(call)
        where !string.IsNullOrEmpty(exception)
        where _errorLevelMethods.Contains(MethodName(call))
        where call.ArgumentList.Arguments is not [{ Expression: IdentifierNameSyntax first }, ..]
              || first.Identifier.ValueText != exception
        select Where(call);

    /// <summary>Log calls inside a catch that pass that catch's exception's <c>Message</c>.</summary>
    private static IEnumerable<string> LoggedExceptionMessages(SyntaxTree tree) =>
        from call in LogCalls(tree.GetRoot())
        let exception = CaughtException(call)
        where !string.IsNullOrEmpty(exception)
        where call.ArgumentList.Arguments.Any(a => a.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax target, Name.Identifier.ValueText: "Message",
        } && target.Identifier.ValueText == exception)
        select Where(call);

    /// <summary>Placeholders named for a secret in the string-literal templates of log calls.</summary>
    private static IEnumerable<string> SecretPlaceholders(SyntaxTree tree) =>
        from call in LogCalls(tree.GetRoot())
        from argument in call.ArgumentList.Arguments
        where argument.Expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
        from Match match in _secretPlaceholder.Matches(((LiteralExpressionSyntax)argument.Expression).Token.ValueText)
        select $"{Where(call)} {match.Value}";

    /// <summary>
    /// The log calls under <paramref name="node"/>: an <c>ILogger</c> extension (<c>logger.LogError(...)</c>,
    /// <c>logger?.LogWarning(...)</c>) or a Serilog static (<c>Log.Fatal(...)</c>).
    /// </summary>
    private static IEnumerable<InvocationExpressionSyntax> LogCalls(SyntaxNode node) =>
        node.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            MethodName(call) is { Length: > 3 } name && name.StartsWith("Log", StringComparison.Ordinal)
            || call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Log" } });

    /// <summary>The exception variable of the innermost catch whose block holds <paramref name="call"/>, if it names one.</summary>
    private static string? CaughtException(InvocationExpressionSyntax call) =>
        call.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault(c => c.Block.Span.Contains(call.Span))
            ?.Declaration?.Identifier.ValueText;

    /// <summary>The name of the method an invocation calls (<c>a.Name(...)</c> or <c>a?.Name(...)</c>).</summary>
    private static string MethodName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => "",
    };

    /// <summary>True when <paramref name="block"/> holds a <c>throw</c> statement or expression.</summary>
    private static bool Throws(BlockSyntax block) =>
        block.DescendantNodes().Any(n => n is ThrowStatementSyntax or ThrowExpressionSyntax);

    /// <summary>True when <paramref name="catchClause"/> carries a comment: inside its block, or on the lines just before it.</summary>
    private static bool HasComment(CatchClauseSyntax catchClause) =>
        catchClause.GetLeadingTrivia().Concat(catchClause.Block.DescendantTrivia())
            .Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));

    /// <summary>"path:line" of <paramref name="node"/>, for a readable failure.</summary>
    private static string Where(SyntaxNode node) =>
        $"{node.SyntaxTree.FilePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    /// <summary>Parses one source text under the given repository-relative path.</summary>
    private static SyntaxTree Parse(string relativePath, string source) =>
        CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), relativePath);

    /// <summary>Every <c>.cs</c> file of the production projects (not <c>*.Tests</c>, not the PostgreSQL scaffold), parsed.</summary>
    private static IReadOnlyList<SyntaxTree> LoadProductionSources()
    {
        string root = RepositoryPaths.Root;

        return
        [
            .. Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .OfType<string>()
                .Where(dir => !RepositoryPaths.HasSegment(dir, "bin") && !RepositoryPaths.HasSegment(dir, "obj"))
                .Where(IsProductionProject)
                .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                .Where(path => !RepositoryPaths.HasSegment(path, "bin") && !RepositoryPaths.HasSegment(path, "obj"))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => Parse(Path.GetRelativePath(root, path), File.ReadAllText(path))),
        ];

        static bool IsProductionProject(string dir)
        {
            string name = Path.GetFileName(dir);
            return name.StartsWith("Maroik.", StringComparison.Ordinal)
                   && !name.EndsWith(".Tests", StringComparison.Ordinal)
                   && name != "Maroik.Core.PostgreSQL";
        }
    }
}
