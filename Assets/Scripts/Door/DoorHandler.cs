using UnityEngine;

namespace Door
{
    public class DoorHandler : MonoBehaviour
    {
        public Animator anim;

        // public SpriteRenderer spriteRenderer;

        // public Sprite doorOpen;
        // public Sprite doorClosed;

        // public bool isOpen = true;

        private void Start()
        {
            // In RL: Open the door immediately without 4s delay
            OpenDoor();
        }

        public void OpenDoor()
        {
            CancelInvoke();
            if (anim != null)
                anim.SetBool(AnimationsConstants.DoorIsOpen, true);
        }

        public void CloseDoor()
        {
            CancelInvoke();
            if (anim != null)
                anim.SetBool(AnimationsConstants.DoorIsOpen, false);
        }
    }
}
