using System.Runtime.CompilerServices;
using DiffEngine;

namespace ToDo.Tests.Infrastructure;

/// <summary>
/// Snapshot testing setup. Path assertions prove the fields we named are right; snapshots catch
/// the field somebody accidentally <em>added</em>, which is the contract break a browser client
/// actually feels.
/// </summary>
internal static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyHttp.Initialize();

        // Ids are UUIDv7 and timestamps are real, so without scrubbing every snapshot would fail on
        // every run. These replace them with stable placeholders while still proving that a value
        // of the right shape was in that position.
        VerifierSettings.ScrubInlineGuids();
        VerifierSettings.ScrubInlineDateTimes("O");

        // Verify drops empty collections by default, which would hide an "items" array vanishing
        // from the contract -- exactly the regression these snapshots exist to catch.
        VerifierSettings.DontIgnoreEmptyCollections();

        // Content-Length changes whenever any string in the fixture does, so it is churn, not signal.
        VerifierSettings.IgnoreMember("Content-Length");

        Verifier.UseProjectRelativeDirectory("Snapshots");

        // Nothing should try to open a diff tool during a test run.
        DiffRunner.Disabled = true;
    }
}
