using System;
using System.Collections;
using UnityEngine;

namespace U3Gear.Playground.Scripts.Physics
{
    /// <summary>
    ///     Alternate movement of a platform between two points.
    ///     This script should be attached to the object that will be used as a mobile platform.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class SimplePlatform : MonoBehaviour
    {
        // Tolerance value between twos positions.
        private const float Tolerance = .1f;

        /// ==================================================
        /// Private Visible Variables
        /// ==================================================
        [SerializeField] [Tooltip("Starting point of the platform, its origin.")]
        private Transform origin;

        [SerializeField] [Tooltip("Point of destination of the platform.")]
        private Transform destination;

        [SerializeField] [Tooltip("Platform movement speed.")]
        private float speed = 0.03f;

        [SerializeField] [Tooltip("Hold time before the platform initiates movement.")]
        private float holdTime = 3.0f;

        /// ==================================================
        /// Private Variables
        /// ==================================================

        // Change of direction of movement of the platform when it reaches its destination.
        private bool _switch;

        /// ==================================================
        /// Unity Methods
        /// ==================================================
        /// <summary>
        ///     This function is called every fixed framerate frame, if the MonoBehaviour is enabled.
        ///     FixedUpdate should be used instead of Update when dealing with Rigidbody.
        ///     For example when adding a force to a rigidbody, you have to apply the force every
        ///     fixed frame inside FixedUpdate instead of every frame inside Update.
        /// </summary>
        private void FixedUpdate()
        {
            // Start the coroutine to hold time before starting the move.
            StartCoroutine(nameof(HoldTime));

            // Changing the direction of movement of the platform.
            transform.position = Vector3.MoveTowards(transform.position,
                _switch ? origin.position : destination.position, speed);
        }

        /// <summary>
        ///     Coroutine to implement hold time, pause, before starting the the movement of the platform.
        /// </summary>
        /// <returns>holdTime</returns>
        private IEnumerator HoldTime()
        {
            // When it reaches the destination, the hold time is activated.
            if (Math.Abs(transform.position.y - destination.position.y) < Tolerance)
            {
                // Uses the holdTime variable value.
                yield return new WaitForSeconds(holdTime);
                // After the hold time has elapsed it activates the change of direction and starts the movement.
                _switch = true;
            }

            // When it reaches the origin, the hold time is activated.
            if (!(Math.Abs(transform.position.y - origin.position.y) < Tolerance)) yield break;
            // Uses the holdTime variable value.
            yield return new WaitForSeconds(holdTime);
            // After the hold time has elapsed it starts the movement.
            _switch = false;
        }
    }
}