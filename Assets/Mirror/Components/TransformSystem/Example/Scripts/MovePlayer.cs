using UnityEngine;

namespace Mirror.TransformSyncing.Example
{
    public class MovePlayer : NetworkBehaviour
    {
        [SerializeField] float speed = 1;
        [SerializeField] Vector3 min;
        [SerializeField] Vector3 max;

        private void Update()
        {
            if (!hasAuthority) { return; }

            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 delta = new Vector3(x, 0, z) * speed * Time.deltaTime;
            Vector3 newPos = transform.position + delta;

            Vector3 inBoundsPos = ClampPositionInBounds(newPos);
            transform.position = inBoundsPos;
        }

        private Vector3 ClampPositionInBounds(Vector3 newPos)
        {
            return new Vector3(
                Mathf.Clamp(newPos.x, min.x, max.x),
                Mathf.Clamp(newPos.y, min.y, max.y),
                Mathf.Clamp(newPos.z, min.z, max.z)
                );
        }
    }
}
