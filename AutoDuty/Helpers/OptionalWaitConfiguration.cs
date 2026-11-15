using ECommons.Automation.NeoTaskManager;

namespace AutoDuty.Helpers;

// A short observation window may expire normally (e.g. an instant interaction never casts).
// Keep default exception handling; only the timeout is optional.
internal sealed class OptionalWaitConfiguration(int milliseconds) : TaskManagerConfiguration(milliseconds);
