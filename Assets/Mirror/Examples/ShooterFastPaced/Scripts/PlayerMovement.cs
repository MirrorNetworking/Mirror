// based on Unity's FirstPersonController & ThirdPersonController scripts
using UnityEngine;
using Controller2k;
using Random = UnityEngine.Random;

namespace Mirror.Examples.Shooter
{
    // MoveState as byte for minimal bandwidth (otherwise it's int by default)
    // note: distinction between WALKING and RUNNING in case we need to know the
    //       difference somewhere (e.g. for endurance recovery)
    public enum MoveState : byte {IDLE, WALKING, RUNNING, AIRBORNE, CLIMBING}

    [RequireComponent(typeof(CharacterController2k))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerMovement : NetworkBehaviour
    {
        // components to be assigned in inspector
        [Header("Components")]
        public Animator animator;
        public CharacterController2k controller;
        public AudioSource feetAudio;
        // the collider for the character controller. NOT the hips collider. this
        // one is NOT affected by animations and generally a better choice for state
        // machine logic.
        public CapsuleCollider controllerCollider;
    #pragma warning disable CS0109 // member does not hide accessible member
        new Camera camera;
    #pragma warning restore CS0109 // member does not hide accessible member

        [Header("State")]
        [SyncVar] public MoveState state = MoveState.IDLE;
        [HideInInspector] public Vector3 moveDir;

        [Header("Walking")]
        public float walkSpeed = 5;
        public float walkAcceleration = 15; // set to maxint for instant speed
        public float walkDeceleration = 20; // feels best if higher than acceleration

        [Header("Running")]
        public float runSpeed = 8;
        [Range(0f, 1f)] public float runStepLength = 0.7f;
        public float runStepInterval = 3;
        public float runCycleLegOffset = 0.2f; //specific to the character in sample assets, will need to be modified to work with others
        public KeyCode walkKey = KeyCode.LeftShift;
        float stepCycle;
        float nextStep;

        [Header("Jumping")]
        public float jumpSpeed = 7;
        [HideInInspector] public float jumpLeg;
        bool jumpKeyPressed;

        [Header("Falling")]
        public float airborneAcceleration = 15; // set to maxint for instant speed
        public float airborneDeceleration = 20; // feels best if higher than acceleration
        public float fallMinimumMagnitude = 9; // walking down steps shouldn't count as falling and play no falling sound.
        public float fallDamageMinimumMagnitude = 13;
        public float fallDamageMultiplier = 2;
        [HideInInspector] public Vector3 lastFall;
        bool sprintingBeforeAirborne; // don't allow sprint key to accelerate while jumping. decision has to be made before that.

        [Header("Climbing")]
        public float climbSpeed = 3;
        Collider ladderCollider;

        [Header("Physics")]
        public float gravityMultiplier = 2;

        // we need to remember the last accelerated xz speed without gravity etc.
        // (using moveDir.xz.magnitude doesn't work well with mounted movement)
        float horizontalSpeed;

        // helper property to check grounded with some tolerance. technically we
        // aren't grounded when walking down steps, but this way we factor in a
        // minimum fall magnitude. useful for more tolerant jumping etc.
        // (= while grounded or while velocity not smaller than min fall yet)
        public bool isGroundedWithinTolerance =>
            controller.isGrounded || controller.velocity.y > -fallMinimumMagnitude;

        [Header("Sounds")]
        public AudioClip[] footstepSounds;    // an array of footstep sounds that will be randomly selected from.
        public AudioClip jumpSound;           // the sound played when character leaves the ground.
        public AudioClip landSound;           // the sound played when character touches back on ground.

        void Awake()
        {
            camera = Camera.main;
        }

        // input directions ////////////////////////////////////////////////////////
        Vector2 GetInputDirection()
        {
            // get input direction while alive and while not typing in chat
            // (otherwise 0 so we keep falling even if we die while jumping etc.)
            // => 'Raw' because otherwise running takes almost a second to stop!
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            return new Vector2(horizontal, vertical).normalized;
        }

        Vector3 GetDesiredDirection(Vector2 inputDir)
        {
            // always move along the camera forward as it is the direction that is being aimed at
            return transform.forward * inputDir.y + transform.right * inputDir.x;
        }

        // movement state machine //////////////////////////////////////////////////
        bool EventJumpRequested()
        {
            // only while grounded, so jump key while jumping doesn't start a new
            // jump immediately after landing
            // => and not while sliding, otherwise we could climb slides by jumping
            // => not even while SlidingState.Starting, so we aren't able to avoid
            //    sliding by bunny hopping.
            // => grounded check uses min fall tolerance so we can actually still
            //    jump when walking down steps.
            return isGroundedWithinTolerance &&
                   controller.slidingState == SlidingState.NONE &&
                   jumpKeyPressed;
        }

        bool EventFalling()
        {
            // use minimum fall magnitude so walking down steps isn't detected as
            // falling! otherwise walking down steps would show the fall animation
            // and play the landing sound.
            return !isGroundedWithinTolerance;
        }

        bool EventLanded()
        {
            return controller.isGrounded;
        }

        bool EventLadderEnter()
        {
            return ladderCollider != null;
        }

        bool EventLadderExit()
        {
            // OnTriggerExit isn't good enough to detect ladder exits because we
            // shouldn't exit as soon as our head sticks out of the ladder collider.
            // only if we fully left it. so check this manually here:
            return ladderCollider != null &&
                   !ladderCollider.bounds.Intersects(controllerCollider.bounds);
        }

        // helper function to apply gravity based on previous Y direction
        float ApplyGravity(float moveDirY)
        {
            // apply full gravity while falling
            if (!controller.isGrounded)
                // gravity needs to be * Time.fixedDeltaTime even though we multiply
                // the final controller.Move * Time.fixedDeltaTime too, because the
                // unit is 9.81m/s²
                return moveDirY + Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            // if grounded then apply no force. the new OpenCharacterController
            // doesn't need a ground stick force. it would only make the character
            // slide on all uneven surfaces.
            return 0;
        }

        // helper function to get move or walk speed depending on key press & endurance
        float GetWalkOrRunSpeed()
        {
            bool walkRequested = Input.GetKey(walkKey);
            return walkRequested ? walkSpeed : runSpeed;
        }

        void ApplyFallDamage()
        {
            // measure only the Y direction. we don't want to take fall damage
            // if we jump forward into a wall because xz is high.
            float fallMagnitude = Mathf.Abs(lastFall.y);
            if(fallMagnitude >= fallDamageMinimumMagnitude)
            {
                int damage = Mathf.RoundToInt(fallMagnitude * fallDamageMultiplier);
                Debug.Log($"Fall with magnitude={fallMagnitude} damage={damage}");
            }
        }

        // acceleration can be different when accelerating/decelerating
        float AccelerateSpeed(Vector2 inputDir, float currentSpeed, float targetSpeed, float acceleration)
        {
            // desired speed is between 'speed' and '0'
            float desiredSpeed = inputDir.magnitude * targetSpeed;

            // accelerate speed
            return Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.deltaTime);
        }

        void EnterLadder()
        {
            // make player look directly at ladder forward.
            // note: even though we set the rotation perfectly here, there's
            //       still one frame where it seems to interpolate between the
            //       new and the old rotation, which causes 1 odd camera frame.
            //       this could be avoided by overwriting transform.forward once
            //       more in LateUpdate.
            transform.forward = ladderCollider.transform.forward;
        }

        MoveState UpdateIDLE(Vector2 inputDir, Vector3 desiredDir)
        {
            // decelerate from last move (e.g. from jump)
            // (moveDir.xz can be set to 0 to have an interruption when landing)
            horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, 0, walkDeceleration);
            moveDir.x = desiredDir.x * horizontalSpeed;
            moveDir.y = ApplyGravity(moveDir.y);
            moveDir.z = desiredDir.z * horizontalSpeed;

            if (EventFalling())
            {
                sprintingBeforeAirborne = false;
                return MoveState.AIRBORNE;
            }
            else if (EventJumpRequested())
            {
                // start the jump movement into Y dir, go to jumping
                // note: no endurance>0 check because it feels odd if we can't jump
                moveDir.y = jumpSpeed;
                sprintingBeforeAirborne = false;
                PlayJumpSound();
                return MoveState.AIRBORNE;
            }
            else if (EventLadderEnter())
            {
                EnterLadder();
                return MoveState.CLIMBING;
            }
            else if (inputDir != Vector2.zero)
            {
                return MoveState.WALKING;
            }

            return MoveState.IDLE;
        }

        MoveState UpdateWALKINGandRUNNING(Vector2 inputDir, Vector3 desiredDir)
        {
            // walk or run?
            float speed = GetWalkOrRunSpeed();

            // move with acceleration (feels better)
            horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, speed, inputDir != Vector2.zero ? walkAcceleration : walkDeceleration);
            moveDir.x = desiredDir.x * horizontalSpeed;
            moveDir.y = ApplyGravity(moveDir.y);
            moveDir.z = desiredDir.z * horizontalSpeed;

            if (EventFalling())
            {
                sprintingBeforeAirborne = speed == runSpeed;
                return MoveState.AIRBORNE;
            }
            else if (EventJumpRequested())
            {
                // start the jump movement into Y dir, go to jumping
                // note: no endurance>0 check because it feels odd if we can't jump
                moveDir.y = jumpSpeed;
                sprintingBeforeAirborne = speed == runSpeed;
                PlayJumpSound();
                return MoveState.AIRBORNE;
            }
            else if (EventLadderEnter())
            {
                EnterLadder();
                return MoveState.CLIMBING;
            }
            // go to idle after fully decelerating (y doesn't matter)
            else if (moveDir.x == 0 && moveDir.z == 0)
            {
                return MoveState.IDLE;
            }

            ProgressStepCycle(inputDir, speed);
            return speed == walkSpeed ? MoveState.WALKING : MoveState.RUNNING;
        }

        MoveState UpdateAIRBORNE(Vector2 inputDir, Vector3 desiredDir)
        {
            // max speed depends on what we did before jumping/falling
            float speed = sprintingBeforeAirborne ? runSpeed : walkSpeed;

            // move with acceleration (feels better)
            horizontalSpeed = AccelerateSpeed(inputDir, horizontalSpeed, speed, inputDir != Vector2.zero ? airborneAcceleration : airborneDeceleration);
            moveDir.x = desiredDir.x * horizontalSpeed;
            moveDir.y = ApplyGravity(moveDir.y);
            moveDir.z = desiredDir.z * horizontalSpeed;

            if (EventLanded())
            {
                // apply fall damage only in AIRBORNE->Landed.
                // (e.g. not if we run face forward into a wall with high velocity)
                ApplyFallDamage();
                PlayLandingSound();
                // transition to the idle/walk/run depending on state.
                // if we always transition to IDLE, it woul always play the IDLE
                // animation once after jumping, even while running, which
                // looks odd.
                if (moveDir.x == 0 && moveDir.z == 0)
                {
                    return MoveState.IDLE;
                }
                else
                {
                    return MoveState.WALKING;
                }
            }
            else if (EventLadderEnter())
            {
                EnterLadder();
                return MoveState.CLIMBING;
            }

            return MoveState.AIRBORNE;
        }

        MoveState UpdateCLIMBING(Vector2 inputDir, Vector3 desiredDir)
        {
            // finished climbing?
            if (EventLadderExit())
            {
                // player rotation was adjusted to ladder rotation before.
                // let's reset it, but also keep look forward
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
                ladderCollider = null;
                return MoveState.IDLE;
            }

            // interpret forward/backward movement as upward/downward
            // note: NO ACCELERATION, otherwise we would climb really fast when
            //       sprinting towards a ladder. and the actual climb feels way too
            //       unresponsive when accelerating.
            moveDir.x = inputDir.x * climbSpeed;
            moveDir.y = inputDir.y * climbSpeed;
            moveDir.z = 0;

            // make the direction relative to ladder rotation. so when pressing right
            // we always climb to the right of the ladder, no matter how it's rotated
            moveDir = ladderCollider.transform.rotation * moveDir;
            Debug.DrawLine(transform.position, transform.position + moveDir, Color.yellow, 0.1f, false);

            return MoveState.CLIMBING;
        }

        // usually it's best practice to apply physics in FixedUpdate.
        // however, since controller.Move() does not interpolate, using only
        // FixedUpdate would look noticably jitter in first person view.
        // let's try Update + Time.deltaTime instead of Time.fixedDeltaTime.
        void Update()
        {
            if (isLocalPlayer)
            {
                // get input and desired direction based on camera and ground
                Vector2 inputDir = GetInputDirection();
                Vector3 desiredDir = GetDesiredDirection(inputDir);
                Debug.DrawLine(transform.position, transform.position + desiredDir, Color.blue);
                Debug.DrawLine(transform.position, transform.position + desiredDir, Color.cyan);
                if (!jumpKeyPressed) jumpKeyPressed = Input.GetButtonDown("Jump");

                // update state machine
                if (state == MoveState.IDLE)                  state = UpdateIDLE(inputDir, desiredDir);
                else if (state == MoveState.WALKING)          state = UpdateWALKINGandRUNNING(inputDir, desiredDir);
                else if (state == MoveState.RUNNING)          state = UpdateWALKINGandRUNNING(inputDir, desiredDir);
                else if (state == MoveState.AIRBORNE)         state = UpdateAIRBORNE(inputDir, desiredDir);
                else if (state == MoveState.CLIMBING)         state = UpdateCLIMBING(inputDir, desiredDir);
                else Debug.LogError("Unhandled Movement State: " + state);

                // cache this move's state to detect landing etc. next time
                if (!controller.isGrounded) lastFall = controller.velocity;

                // move depending on latest moveDir changes
                Debug.DrawLine(transform.position, transform.position + moveDir * Time.deltaTime, Color.magenta);
                controller.Move(moveDir * Time.deltaTime); // note: returns CollisionFlags if needed

                // calculate which leg is behind, so as to leave that leg trailing in the jump animation
                // (This code is reliant on the specific run cycle offset in our animations,
                // and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5)
                if (animator != null)
                {
                    float runCycle = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime + runCycleLegOffset, 1);
                    jumpLeg = (runCycle < 0.5f ? 1 : -1);// * move.z;
                }

                // reset keys no matter what
                jumpKeyPressed = false;
            }

            // update animations for everyone
            SetAnimations();
        }

        void OnGUI()
        {
            // show data next to player for easier debugging. this is very useful!
            if (Debug.isDebugBuild)
            {
                // project player position to screen
                Vector3 center = controllerCollider.bounds.center;
                Vector3 point = camera.WorldToScreenPoint(center);

                // in front of camera and in screen?
                if (point.z >= 0 && Utils.IsPointInScreen(point))
                {
                    GUI.color = new Color(0, 0, 0, 0.5f);
                    GUILayout.BeginArea(new Rect(point.x, Screen.height - point.y, 150, 200));

                    // some info for all players, including local
                    GUILayout.Label("grounded=" + controller.isGrounded);
                    GUILayout.Label("groundedTol=" + isGroundedWithinTolerance);
                    GUILayout.Label("lastFall=" + lastFall);
                    GUILayout.Label("sliding=" + controller.slidingState);

                    GUILayout.EndArea();
                    GUI.color = Color.white;
                }
            }
        }

        void PlayLandingSound()
        {
            feetAudio.clip = landSound;
            feetAudio.Play();
            nextStep = stepCycle + .5f;
        }

        void PlayJumpSound()
        {
            feetAudio.clip = jumpSound;
            feetAudio.Play();
        }

        void ProgressStepCycle(Vector3 inputDir, float speed)
        {
            if (controller.velocity.sqrMagnitude > 0 && (inputDir.x != 0 || inputDir.y != 0))
            {
                stepCycle += (controller.velocity.magnitude + (speed*(state == MoveState.WALKING ? 1 : runStepLength)))*
                             Time.deltaTime;
            }

            if (stepCycle > nextStep)
            {
                nextStep = stepCycle + runStepInterval;
                PlayFootStepAudio();
            }
        }

        void PlayFootStepAudio()
        {
            if (!controller.isGrounded) return;

            // any configured sounds?
            if (footstepSounds == null || footstepSounds.Length == 0) return;

            // pick & play a random footstep sound from the array,
            // excluding sound at index 0
            int n = Random.Range(1, footstepSounds.Length);
            feetAudio.clip = footstepSounds[n];
            feetAudio.PlayOneShot(feetAudio.clip);

            // move picked sound to index 0 so it's not picked next time
            footstepSounds[n] = footstepSounds[0];
            footstepSounds[0] = feetAudio.clip;
        }

        void SetAnimations()
        {
            animator.SetBool("IDLE", state == MoveState.IDLE);
            animator.SetBool("WALKING", state == MoveState.WALKING);
            animator.SetBool("RUNNING", state == MoveState.RUNNING);
            animator.SetBool("AIRBORNE", state == MoveState.AIRBORNE);
        }
    }
}
