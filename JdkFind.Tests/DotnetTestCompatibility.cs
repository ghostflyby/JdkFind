using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace JdkFind.Tests;

// The .NET SDK's "dotnet test" forwards its own --nologo option to every
// Microsoft.Testing.Platform test application, but the platform version this
// project restores (2.4.0) does not define such an option. Under the
// "--server dotnettestcli" launch the resulting "unknown option" parse error is
// meant to travel over the dotnet-test pipe, which was never established at
// parse time, so the message is dropped and the test host exits with code 5
// (invalid command-line arguments) before executing any test. Declaring the
// option here makes the forwarded argument valid; nothing reads its value.
internal static class DotnetTestCompatibilityBuilderHook
{
    // Signature must match the static AddExtensions(ITestApplicationBuilder, string[])
    // call that Microsoft.Testing.Platform.MSBuild emits into the generated
    // SelfRegisteredExtensions for every TestingPlatformBuilderHook item.
    public static void AddExtensions(ITestApplicationBuilder builder, string[] args) =>
        builder.CommandLine.AddProvider(static () => new DotnetTestCompatibilityOptions());
}

internal sealed class DotnetTestCompatibilityOptions : ICommandLineOptionsProvider
{
    private const string NologoOptionName = "nologo";

    // Stable literal per IExtension contract; never derive from nameof(...).
    public string Uid => "JdkFind.Tests.DotnetTestCompatibilityOptions";

    public string Version => "1.0.0";

    public string DisplayName => ".NET SDK dotnet-test compatibility";

    public string Description => "Accepts options forwarded by the SDK's dotnet test that the testing platform does not define.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new(NologoOptionName, "Forwarded by dotnet test; accepted for compatibility and ignored.", ArgumentArity.Zero, isHidden: true),
    ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments) => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => ValidationResult.ValidTask;
}
