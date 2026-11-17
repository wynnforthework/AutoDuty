namespace AutoDuty.Helpers;

// Track the duty whose execution state is retained, including while paused/stopped.
// Territory 0 is a loading placeholder, not proof of leaving an instance.
internal sealed class DutyExecutionBoundary
{
    private uint territory;
    internal void Enter(uint value) { if (value != 0) this.territory = value; }
    internal bool Leave(uint destination, bool isDuty)
    {
        if (this.territory == 0 || destination == 0 || isDuty) return false;
        this.territory = 0;
        return true;
    }
}
