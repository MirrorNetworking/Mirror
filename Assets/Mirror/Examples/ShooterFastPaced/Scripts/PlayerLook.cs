using System;
using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class PlayerLook : NetworkBehaviour
    {
        [Header("Components")]
    #pragma warning disable CS0109 // member does not hide accessible member
        [HideInInspector] public new Camera camera;
    #pragma warning restore CS0109 // member does not hide accessible member

        [Header("Camera")]
        public float XSensitivity = 2;
        public float YSensitivity = 2;
        public float MinimumX = -90;
        public float MaximumX = 90;

        // head position is useful for raycasting etc.
        [Tooltip("Camera will be mounted to this in first person view. Highly recommended to position this at (0,Y,0), otherwise at (0,Y,0.5) etc. there might be jitter since the camera is not only rotated, but also moved.")]
        public Transform firstPersonParent;
        public Vector3 headPosition => firstPersonParent.position;
        Vector3 originalCameraPosition;

        // the layer mask to use when trying to detect view blocking
        // (this way we dont zoom in all the way when standing in another entity)
        // (-> create a entity layer for them if needed)
        public LayerMask viewBlockingLayers;
        public float zoomSpeed = 0.5f;
        public float distance = 0;
        public float minDistance = 0;
        public float maxDistance = 7;

        [Header("Physical Interaction")]
        [Tooltip("Layers to use for raycasting. Check Default, Walls, Player, Zombie, Doors, Interactables, Item, etc. Uncheck IgnoreRaycast, AggroArea, Water, UI, etc.")]
        public LayerMask raycastLayers = Physics.DefaultRaycastLayers;

        // camera offsets. Vector2 because we only want X (left/right) and Y (up/down)
        // to be modified. Z (forward/backward) should NEVER be modified because
        // then we could look through walls when tilting our head forward to look
        // downwards, etc. This can be avoided in the camera positioning logic, but
        // is way to complex and not worth it at all.
        [Header("Offsets - Standing")]
        public Vector2 firstPersonOffsetStanding = Vector2.zero;
        public Vector2 thirdPersonOffsetStanding = Vector2.up;
        public Vector2 thirdPersonOffsetStandingMultiplier = Vector2.zero;

        // look directions /////////////////////////////////////////////////////////
        // * for first person, all we need is the camera.forward
        //
        // * for third person, we need to raycast where the camera looks and then
        //   calculate the direction from the eyes.
        //   BUT for animations we actually only want camera.forward because it
        //   looks strange if we stand right in front of a wall, camera aiming above
        //   a player's head (because of head offset) and then the players arms
        //   aiming at that point above his head (on the wall) too.
        //     => he should always appear to aim into the far direction
        //     => he should always fire at the raycasted point
        //   in other words, if we want 1st and 3rd person WITH camera offsets, then
        //   we need both the FAR direction and the RAYCASTED direction
        //
        // * we also need to sync it over the network to animate other players.
        //   => we compress it as far as possible to save bandwidth. syncing it via
        //      rotation bytes X and Y uses 2 instead of 12 bytes per observer(!)
        //
        // * and we can't only calculate and store the values in Update because
        //   ShoulderLookAt needs them live in LateUpdate, Update is too far behind
        //   and would cause the arms to be lag behind a bit.
        //
        public Vector3 lookDirectionFar
        {
            get
            {
                return camera.transform.forward;
            }
        }

        //[SyncVar, HideInInspector] Vector3 syncedLookDirectionRaycasted; not needed atm, see lookPositionRaycasted comment
        public Vector3 lookDirectionRaycasted
        {
            get
            {
                // same for local and other players
                // (positionRaycasted uses camera || syncedDirectionRaycasted anyway)
                return (lookPositionRaycasted - headPosition).normalized;
            }
        }

        // the far position, directionFar projected into nirvana
        public Vector3 lookPositionFar
        {
            get
            {
                Vector3 position = camera.transform.position;
                return position + lookDirectionFar * 9999f;
            }
        }

        // the raycasted position is needed for lookDirectionRaycasted calculation
        // and for firing, so we might as well reuse it here
        public Vector3 lookPositionRaycasted
        {
            get
            {
                // raycast based on position and direction, project into nirvana if nothing hit
                // (not * infinity because might overflow depending on position)
                return LookPositionRaycasted(out RaycastHit hit) ? hit.point : lookPositionFar;
           }
        }

        public bool LookPositionRaycasted(out RaycastHit hit) =>
            Utils.RaycastWithout(camera.transform.position, camera.transform.forward, out hit, Mathf.Infinity, gameObject, raycastLayers);

        void Awake()
        {
            camera = Camera.main;
        }

        public override void OnStartLocalPlayer()
        {
            Cursor.lockState = CursorLockMode.Locked;

            // set camera parent to player
            camera.transform.SetParent(transform, false);

            // look into player forward direction, which was loaded from the db
            camera.transform.rotation = transform.rotation;

            // set camera to head position
            camera.transform.position = headPosition;

            // remember original camera position
            originalCameraPosition = camera.transform.localPosition;
        }

        void OnDestroy()
        {
            // LateUpdate may lock the cursor.
            // always free it in OnDestroy so players can use the UI after disconnects.
            if (isLocalPlayer)
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        // Update camera position after everything else was updated
        void LateUpdate()
        {
            if (!isLocalPlayer) return;

            // escape unlocks cursor
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
            }
            // mouse click locks cursor
            else if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            // only while alive and  while cursor is locked, otherwise we are in a UI
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // calculate horizontal and vertical rotation steps
                float xExtra = Input.GetAxis("Mouse X") * XSensitivity;
                float yExtra = Input.GetAxis("Mouse Y") * YSensitivity;

                // use mouse to rotate character
                // set to player parent already?
                if (camera.transform.parent != transform)
                    InitializeForcedLook();

                // rotate character for horizontal, rotate camera for vertical
                transform.Rotate(new Vector3(0, xExtra, 0));
                camera.transform.Rotate(new Vector3(-yExtra, 0, 0));
            }

            // clamp camera rotation automatically. this way we can rotate it to
            // whatever we like in Update, and LateUpdate will correct it.
            camera.transform.localRotation = Utils.ClampRotationAroundXAxis(camera.transform.localRotation, MinimumX, MaximumX);

            // zoom after rotating, otherwise it won't be smooth and would overwrite
            // each other.

            // zoom should only happen if not in a UI right now
            if (!Utils.IsCursorOverUserInterface())
            {
                float step = Utils.GetZoomUniversal() * zoomSpeed;
                distance = Mathf.Clamp(distance - step, minDistance, maxDistance);
            }

            // calculate target and zoomed position
            if (distance == 0) // first person
            {
                // we use the current head bone position as origin here
                // -> gets rid of the idle->run head change effect that was odd
                // -> gets rid of upper body culling issues when looking downwards
                Vector3 headLocal = transform.InverseTransformPoint(headPosition);
                Vector3 origin = headLocal;
                Vector3 offset = firstPersonOffsetStanding;

                // set final position
                Vector3 target = transform.TransformPoint(origin + offset);
                camera.transform.position = target;
            }
            else // third person
            {
                Vector3 origin = originalCameraPosition;
                Vector3 offsetBase = thirdPersonOffsetStanding;
                Vector3 offsetMult = thirdPersonOffsetStandingMultiplier;

                Vector3 target = transform.TransformPoint(origin + offsetBase + offsetMult * distance);
                Vector3 newPosition = target - (camera.transform.rotation * Vector3.forward * distance);

                // avoid view blocking (only third person, pointless in first person)
                // -> always based on original distance and only overwrite if necessary
                //    so that we dont have to zoom out again after view block disappears
                // -> we cast exactly from cam to target, which is the crosshair position.
                //    if anything is inbetween then view blocking changes the distance.
                //    this works perfectly.
                float finalDistance = distance;
                RaycastHit hit;
                Debug.DrawLine(target, camera.transform.position, Color.white);
                if (Physics.Linecast(target, newPosition, out hit, viewBlockingLayers))
                {
                    // calculate a better distance (with some space between it)
                    finalDistance = Vector3.Distance(target, hit.point) - 0.1f;
                    Debug.DrawLine(target, hit.point, Color.red);
                }
                else Debug.DrawLine(target, newPosition, Color.green);

                // set final position
                camera.transform.position = target - (camera.transform.rotation * Vector3.forward * finalDistance);
            }
        }

        public bool InFirstPerson()
        {
            return distance == 0;
        }

        // free look mode //////////////////////////////////////////////////////////
        public void InitializeForcedLook()
        {
            camera.transform.SetParent(transform, false);
        }

        // debugging ///////////////////////////////////////////////////////////////
        void OnDrawGizmos()
        {
            if (camera == null) return;

            // draw camera forward
            Gizmos.color = Color.white;
            Gizmos.DrawLine(headPosition, camera.transform.position + camera.transform.forward * 9999f);

            // draw all the different look positions
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(headPosition, lookPositionFar);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(headPosition, lookPositionRaycasted);
        }
    }
}
