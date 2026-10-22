using Ghosts;
using Player;
using UnityEngine;

namespace Teleporter
{
    public class Teleporter : MonoBehaviour
    {
        public GameObject target;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (target == null) return;

            var targetPos = target.transform.position;
            other.transform.position = targetPos;

            var rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = targetPos;
            }

            if (other.gameObject.CompareTag(TagsConstants.PlayerTag))
            {
                var pc = other.gameObject.GetComponent<PlayerController>();
                if (pc != null)
                    pc.NextDestination = targetPos;
            }
            else if (other.gameObject.CompareTag(TagsConstants.EnemyTag))
            {
                var ghost = other.gameObject.GetComponent<GhostAiMovement>();
                if (ghost != null)
                    ghost.NextTileDestination = targetPos;
            }
        }
    }
}
