using UnityEngine;
using UnityEngine.InputSystem;

namespace GDEV50024.Week01
{
    /// <summary>
    /// Puts the character back at the start of the movement lab when Restart is pressed.
    /// </summary>
    public class LabRestart : MonoBehaviour
    {
        [SerializeField] InputActionReference restartAction;
        [SerializeField] StrictPlatformerController player;
        [SerializeField] Transform spawnPoint;

        [Tooltip("Safety net: if the character ever ends up below this height, restart automatically.")]
        [SerializeField] float fallLimitY = -10f;

        void OnEnable()
        {
            if (restartAction == null || player == null || spawnPoint == null)
            {
                Debug.LogError("Assign the Restart action, player and spawn point on LabRestart.", this);
                enabled = false;
                return;
            }
            restartAction.action.Enable();
        }

        void OnDisable()
        {
            if (restartAction != null) restartAction.action.Disable();
        }

        void Update()
        {
            bool fellOutOfTheLab = player.transform.position.y < fallLimitY;

            if (restartAction.action.WasPressedThisFrame() || fellOutOfTheLab)
                player.ResetTo(spawnPoint.position);
        }
    }
}
