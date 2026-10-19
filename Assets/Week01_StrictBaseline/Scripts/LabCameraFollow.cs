using UnityEngine;

namespace GDEV50024.Week01
{
    /// <summary>
    /// Keeps the character horizontally centred, without ever showing past the edges of the lab.
    /// The camera height never changes. There is no smoothing, so the camera adds no motion of its own.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LabCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("Left edge of the lab, in world units.")]
        [SerializeField] float labMinX = -3f;
        [Tooltip("Right edge of the lab, in world units.")]
        [SerializeField] float labMaxX = 61f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        // LateUpdate runs after the character has moved this frame.
        void LateUpdate()
        {
            float halfViewWidth = cam.orthographicSize * cam.aspect;
            float x;

            if (labMaxX - labMinX > 2f * halfViewWidth)
                x = Mathf.Clamp(target.position.x, labMinX + halfViewWidth, labMaxX - halfViewWidth);
            else
                x = (labMinX + labMaxX) * 0.5f; // the whole lab fits on screen

            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }
    }
}
