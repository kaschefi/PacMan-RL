namespace Ghosts
{
    public class PinkGhostAiMovement : GhostAiMovement
    {
        protected override void Chase()
        {
            if (chaseModeTarget == null) return;
            ChaseTarget(chaseModeTarget, runSpeed);
        }
    }
}
