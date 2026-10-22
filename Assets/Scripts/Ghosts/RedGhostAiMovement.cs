namespace Ghosts
{
    public class RedGhostAiMovement : GhostAiMovement
    {
        protected override void Chase()
        {
            if (chaseModeTarget == null) return;
            ChaseTarget(chaseModeTarget, runSpeed);
        }
    }
}
