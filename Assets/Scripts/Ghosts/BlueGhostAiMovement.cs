using GameHandler;
using UnityEngine;

namespace Ghosts
{
    public class BlueGhostAiMovement : GhostAiMovement
    {
        public GameObject redGhost;

        public override void ResolveDependencies(GameHandler.GameHandler gh = null, ScoreHandler.ScoreHandler sh = null)
        {
            base.ResolveDependencies(gh, sh);
            if (redGhost == null)
            {
                Transform areaRoot = PacManArea.FindAreaRoot(transform);
                Transform searchRoot = areaRoot != null ? areaRoot : (transform.parent != null ? transform.parent : transform);
                var red = searchRoot.GetComponentInChildren<RedGhostAiMovement>(true) ??
                          (transform.parent == null ? FindFirstObjectByType<RedGhostAiMovement>(FindObjectsInactive.Include) : null);
                if (red != null)
                    redGhost = red.gameObject;
            }
        }

        protected override void Chase()
        {
            if (chaseModeTarget == null || redGhost == null) return;
            var targetPos = chaseModeTarget.transform.position;
            Vector2 directionVector = targetPos - redGhost.transform.position;
            ChaseTarget((Vector2)targetPos + directionVector, runSpeed);
        }
    }
}
