using System;
using UnityEngine;

public class Movements : MonoBehaviour
{
    //Assingables
    public Transform PlayerCam;
    public Transform Orientation;
    public Transform WallCheckL;
    public Transform WallCheckR;

    //Other
    private Rigidbody _rb;

    //Rotation and look
    private float _xRotation;
    private float _sensitivity = 50f;
    private float _sensMultiplier = 1f;

    //Movement
    public float MoveSpeed = 4500;
    public float MaxSpeed = 20;
    private float _startMaxSpeed;
    public bool Grounded;
    public LayerMask Ground;

    public float CounterMovement = 0.175f;
    private float _threshold = 0.01f;
    public float MaxSlopeAngle = 35f;

    //Crouch & Slide
    private Vector3 _crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 _playerScale;
    public float SlideForce = 400;
    public float SlideCounterMovement = 0.2f;
    public float CrouchGravityMultiplier;

    //Jumping
    private bool _readyToJump = true;
    private float _jumpCooldown = 0.25f;
    public float JumpForce = 550f;

    public int StartDoubleJumps = 1;
    int doubleJumpsLeft;

    //WallRun
    public LayerMask Wall;
    public float WallRunForce, MaxWallRunTime, MaxWallSpeed;
    bool isWallRight, isWallLeft, isWallAtRange;
    bool isWallRunning;
    public float MaxWallRunCameraTilt, WallRunCameraTilt;

    //Input
    public float x, y;
    bool jumping, sprinting, crouching;

    //AirDash
    public float DashForce;
    public float DashCooldown;
    public float DashTime;
    bool allowDashForceCounter;
    bool readyToDash;
    int wTapTimes = 0;
    Vector3 dashStartVector;

    //RocketBoost
    public float MaxRocketTime;
    public float RocketForce;
    bool rocketActive, readyToRocket;
    bool alreadyInvokedRockedStop;
    float rocketTimer;

    //Sliding
    private Vector3 _normalVector = Vector3.up;

    //SonicSpeed
    public float MaxSonicSpeed;
    public float SonicSpeedForce;
    public float TimeBetweenNextSonicBoost;
    float timePassedSonic;

    //flash
    public float FlashCooldown, FlashRange;
    public int MaxFlashesLeft;
    bool alreadySubtractedFlash;
    public int FlashesLeft = 3;

    //Climbing
    public float ClimbForce, MaxClimbSpeed;
    public LayerMask Ladder;
    bool alreadyStoppedAtLadder;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startMaxSpeed = MaxSpeed;
    }

    void Start()
    {
        _playerScale = transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    private void FixedUpdate()
    {
        Movement();
    }

    private void Update()
    {
        MyInput();
        Look();
        CheckForWall();
        SonicSpeed();
        WallRunInput();
    }

    private void WallRunInput() //make sure to call in void Update
    {
        if (!Grounded && isWallAtRange && !isWallRunning && !crouching)
        {
            StartWallrun();
        }
    }

    private void StartWallrun()
    {
        //If wallRunning while falling/jumping reset y velocity.
        Vector3 vel = _rb.velocity;
        if (_rb.velocity.y != 0) { _rb.velocity = new Vector3(vel.x, 0, vel.z ); }

        isWallRunning = true;

        _rb.useGravity = false;

        _rb.AddForce(Orientation.forward * WallRunForce * Time.deltaTime);

        if (isWallLeft) { _rb.AddForce(-WallCheckL.right * WallRunForce * Time.deltaTime); }
        if (isWallRight) { _rb.AddForce(WallCheckL.right * WallRunForce * Time.deltaTime); }

    }
    private void StopWallRun()
    {
        isWallRunning = false;
        _rb.useGravity = true;
    }

    private void CheckForWall() //make sure to call in void Update
    {
        isWallRight = Physics.Raycast(Orientation.position, Orientation.right, 1f, Wall);
        isWallLeft = Physics.Raycast(Orientation.position, -Orientation.right, 1f, Wall);

        isWallAtRange = false;

        if (isWallRight || isWallLeft) { isWallAtRange = true; }

        if (!isWallAtRange || crouching) { StopWallRun(); }
    }

    private void MyInput()
    {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetKey(KeyCode.LeftShift);

        //Crouching
        if (Input.GetKeyDown(KeyCode.LeftShift))
            StartCrouch();
        if (Input.GetKeyUp(KeyCode.LeftShift))
            StopCrouch();

        //Double Jumping
        if (Input.GetButtonDown("Jump") && !Grounded && doubleJumpsLeft >= 1)
        {
            Jump();
            doubleJumpsLeft--;
        }

        //Dashing
        if (Input.GetKeyDown(KeyCode.W) && wTapTimes <= 1)
        {
            wTapTimes++;
            Invoke("ResetTapTimes", 0.3f);
        }
        if (wTapTimes == 2 && readyToDash) Dash();

        //SideFlash
        if (Input.GetKeyDown(KeyCode.Mouse1) && FlashesLeft > 0 && x > 0) SideFlash(true);
        if (Input.GetKeyDown(KeyCode.Mouse1) && FlashesLeft > 0 && x < 0) SideFlash(false);

        //RocketFlight
        if (Input.GetKeyDown(KeyCode.LeftControl) && readyToRocket)
        {
            //Dampens velocity
            _rb.velocity = _rb.velocity / 3;
        }
        if (Input.GetKey(KeyCode.LeftControl) && readyToRocket)
            StartRocketBoost();

        //climbing
    }

    private void ResetTapTimes()
    {
        wTapTimes = 0;
    }

    private void StartCrouch()
    {
        transform.localScale = _crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
        if (_rb.velocity.magnitude > 0.5f)
        {
            if (Grounded)
            {
                _rb.AddForce(Orientation.transform.forward * SlideForce);
            }
        }
    }

    private void StopCrouch()
    {
        transform.localScale = _playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Movement()
    {
        //Extra gravity
        //Needed that the Ground Check works better!
        float gravityMultiplier = 10f;

        if (crouching) gravityMultiplier = CrouchGravityMultiplier;

        _rb.AddForce(Vector3.down * Time.deltaTime * gravityMultiplier);

        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        counterMovement(x, y, mag);

        //If holding jump && ready to jump, then jump
        if (_readyToJump && jumping && (Grounded || isWallRunning) && !rocketActive) { Jump(); }

        //ResetStuff when touching ground
        if (Grounded || isWallRunning)
        {
            readyToDash = true;
            readyToRocket = true;
            doubleJumpsLeft = StartDoubleJumps;
        }

        //Set max speed
        float MaxSpeed = this.MaxSpeed;

        //If sliding down a ramp, add force down so player stays Grounded and also builds speed
        if (crouching && Grounded && _readyToJump)
        {
            _rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }

        //If speed is larger than MaxSpeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > MaxSpeed) x = 0;
        if (x < 0 && xMag < -MaxSpeed) x = 0;
        if (y > 0 && yMag > MaxSpeed) y = 0;
        if (y < 0 && yMag < -MaxSpeed) y = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        // Movement in air
        if (!Grounded && !isWallRunning)
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Movement while sliding
        if (Grounded && crouching) multiplierV = 0f;

        //Apply forces to move player
        _rb.AddForce(Orientation.transform.forward * y * MoveSpeed * Time.deltaTime * multiplier * multiplierV);
        _rb.AddForce(Orientation.transform.right * x * MoveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump()
    {
        if (Grounded)
        {
            _readyToJump = false;

            //Add jump forces
            _rb.AddForce(Vector2.up * JumpForce * 1.5f);
            _rb.AddForce(_normalVector * JumpForce * 0.5f);

            //If jumping while falling, reset y velocity.
            Vector3 vel = _rb.velocity;
            if (_rb.velocity.y < 0.5f)
                _rb.velocity = new Vector3(vel.x, 0, vel.z);
            else if (_rb.velocity.y > 0)
                _rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);

            Invoke(nameof(ResetJump), _jumpCooldown);
        }
        if (!Grounded)
        {
            _readyToJump = false;

            //Add jump forces
            _rb.AddForce(Orientation.forward * JumpForce * 1f);
            _rb.AddForce(Vector2.up * JumpForce * 1.5f);
            _rb.AddForce(_normalVector * JumpForce * 0.5f);

            //Reset Velocity
            _rb.velocity = Vector3.zero;

            //Disable DashForceCounter if doublejumping while dashing
            allowDashForceCounter = false;

            Invoke(nameof(ResetJump), _jumpCooldown);
        }

        //Walljump
        if (isWallRunning)
        {
            //WallHop
            if (isWallLeft && !Input.GetKeyDown(KeyCode.A))
            {
                _rb.AddForce(Orientation.right * JumpForce * 3.2f);
            }
            if (isWallRight)
            {
                _rb.AddForce(-Orientation.right * JumpForce * 3.2f);
            }

            Vector3 vel = _rb.velocity;
            _rb.velocity = new Vector3(vel.x, -5.5f, vel.z);

            //Always add forward force
            _rb.AddForce(Orientation.forward * JumpForce * 1.25f);

            //Disable DashForceCounter if doublejumping while dashing
            allowDashForceCounter = false;

            Invoke(nameof(ResetJump), _jumpCooldown);
        }
    }

    private void ResetJump()
    {
        _readyToJump = true;
    }

    private void Dash()
    {
        //saves current velocity
        dashStartVector = Orientation.forward;

        allowDashForceCounter = true;

        readyToDash = false;
        wTapTimes = 0;

        //Deactivate gravity
        _rb.useGravity = false;

        //Add force
        _rb.velocity = Vector3.zero;
        _rb.AddForce(Orientation.forward * DashForce);

        Invoke("ActivateGravity", DashTime);
    }
    private void ActivateGravity()
    {
        _rb.useGravity = true;

        //Counter currentForce
        if (allowDashForceCounter)
        {
            _rb.AddForce(dashStartVector * -DashForce * 0.5f);
        }
    }
    private void SonicSpeed()
    {
        //If running builds up speed
        if (Grounded && y >= 0.99f)
        {
            timePassedSonic += Time.deltaTime;
        }
        else
        {
            timePassedSonic = 0;
            MaxSpeed = _startMaxSpeed;
        }

        if (timePassedSonic >= TimeBetweenNextSonicBoost)
        {
            if (MaxSpeed <= MaxSonicSpeed)
            {
                MaxSpeed += 5;
                _rb.AddForce(Orientation.forward * Time.deltaTime * SonicSpeedForce);
            }
            timePassedSonic = 0;
        }
    }
    private void SideFlash(bool isRight)
    {
        RaycastHit hit;
        //Flash Right
        if (Physics.Raycast(Orientation.position, Orientation.right, out hit, FlashRange) && isRight)
        {
            transform.position = hit.point;
        }
        else if (!Physics.Raycast(Orientation.position, Orientation.right, out hit, FlashRange) && isRight)
            transform.position = new Vector3(transform.position.x + FlashRange, transform.position.y, transform.position.z);

        //Flash Left
        if (Physics.Raycast(Orientation.position, -Orientation.right, out hit, FlashRange) && !isRight)
        {
            transform.position = hit.point;
        }
        else if (!Physics.Raycast(Orientation.position, -Orientation.right, out hit, FlashRange) && !isRight)
            transform.position = new Vector3(transform.position.x - FlashRange, transform.position.y, transform.position.z);

        //Dampen falldown
        Vector3 vel = _rb.velocity;
        if (_rb.velocity.y < 0.5f && !alreadyStoppedAtLadder)
        {
            _rb.velocity = new Vector3(vel.x, 0, vel.z);
        }

        FlashesLeft--;
        if (!alreadySubtractedFlash)
        {
            Invoke("ResetFlash", FlashCooldown);
            alreadySubtractedFlash = true;
        }
    }
    private void ResetFlash()
    {
        alreadySubtractedFlash = false;
        Invoke("ResetFlash", FlashCooldown);

        if (FlashesLeft < MaxFlashesLeft)
            FlashesLeft++;
    }
    private void StartRocketBoost()
    {
        if (!alreadyInvokedRockedStop)
        {
            Invoke("StopRocketBoost", MaxRocketTime);
            alreadyInvokedRockedStop = true;
        }

        rocketTimer += Time.deltaTime;

        rocketActive = true;

        //Disable DashForceCounter if doublejumping while dashing
        allowDashForceCounter = false;

        /*Boost all Forces
        Vector3 vel = velocityToBoost;
        Vector3 velBoosted = vel * rocketBoostMultiplier;
        _rb.velocity = velBoosted;
        */

        //Boost forwards and upwards
        _rb.AddForce(Orientation.forward * RocketForce * Time.deltaTime * 1f);
        _rb.AddForce(Vector3.up * RocketForce * Time.deltaTime * 2f);

    }
    private void StopRocketBoost()
    {
        alreadyInvokedRockedStop = false;
        rocketActive = false;
        readyToRocket = false;

        if (rocketTimer >= MaxRocketTime - 0.2f)
        {
            _rb.AddForce(Orientation.forward * RocketForce * -.2f);
            _rb.AddForce(Vector3.up * RocketForce * -.4f);
        }
        else
        {
            _rb.AddForce(Orientation.forward * RocketForce * -.2f * rocketTimer);
            _rb.AddForce(Vector3.up * RocketForce * -.4f * rocketTimer);
        }

        rocketTimer = 0;
    }
    private void Climb()
    {
        //Makes possible to climb even when falling down fast
        Vector3 vel = _rb.velocity;
        if (_rb.velocity.y < 0.5f && !alreadyStoppedAtLadder)
        {
            _rb.velocity = new Vector3(vel.x, 0, vel.z);
            //Make sure char get's at wall
            alreadyStoppedAtLadder = true;
            _rb.AddForce(Orientation.forward * 500 * Time.deltaTime);
        }

        //Push character up
        if (_rb.velocity.magnitude < MaxClimbSpeed)
            _rb.AddForce(Orientation.up * ClimbForce * Time.deltaTime);

        //Doesn't Push into the wall
        if (!Input.GetKey(KeyCode.S)) y = 0;
    }

    private float desiredX;
    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * _sensitivity * Time.fixedDeltaTime * _sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * _sensitivity * Time.fixedDeltaTime * _sensMultiplier;

        //Find current look rotation
        Vector3 rot = PlayerCam.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;

        //Rotate, and also make sure we dont over- or under-rotate.
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        //Perform the rotations
        PlayerCam.transform.localRotation = Quaternion.Euler(_xRotation, desiredX, WallRunCameraTilt);
        Orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);

        //While Wallrunning
        //Tilts camera in .5 second
        if (Math.Abs(WallRunCameraTilt) < MaxWallRunCameraTilt && isWallRunning && isWallRight)
            WallRunCameraTilt += (8 * Time.deltaTime) * MaxWallRunCameraTilt;
        if (Math.Abs(WallRunCameraTilt) < MaxWallRunCameraTilt && isWallRunning && isWallLeft)
            WallRunCameraTilt -= (8 * Time.deltaTime) * MaxWallRunCameraTilt;

        //Tilts camera back again
        if (WallRunCameraTilt > 0 && !isWallRight && !isWallLeft)
            WallRunCameraTilt -= (4 * Time.deltaTime) * MaxWallRunCameraTilt;
        if (WallRunCameraTilt < 0 && !isWallRight && !isWallLeft)
            WallRunCameraTilt += (4 * Time.deltaTime) * MaxWallRunCameraTilt;
    }
    private void counterMovement(float x, float y, Vector2 mag)
    {
        if (!Grounded || jumping) return;

        //Slow down sliding
        if (crouching)
        {
            _rb.AddForce(MoveSpeed * Time.deltaTime * -_rb.velocity.normalized * SlideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > _threshold && Math.Abs(x) < 0.05f || (mag.x < -_threshold && x > 0) || (mag.x > _threshold && x < 0))
        {
            _rb.AddForce(MoveSpeed * Orientation.transform.right * Time.deltaTime * -mag.x * CounterMovement);
        }
        if (Math.Abs(mag.y) > _threshold && Math.Abs(y) < 0.05f || (mag.y < -_threshold && y > 0) || (mag.y > _threshold && y < 0))
        {
            _rb.AddForce(MoveSpeed * Orientation.transform.forward * Time.deltaTime * -mag.y * CounterMovement);
        }

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(_rb.velocity.x, 2) + Mathf.Pow(_rb.velocity.z, 2))) > MaxSpeed)
        {
            float fallspeed = _rb.velocity.y;
            Vector3 n = _rb.velocity.normalized * MaxSpeed;
            _rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    public Vector2 FindVelRelativeToLook()
    {
        float lookAngle = Orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(_rb.velocity.x, _rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = _rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v)
    {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < MaxSlopeAngle;
    }

    private bool cancellingGrounded;

    /// <summary>
    /// Handle ground detection
    /// </summary>
    private void OnCollisionStay(Collision other)
    {
        //Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (Ground != (Ground | (1 << layer))) return;

        //Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;
            //FLOOR
            if (IsFloor(normal))
            {
                Grounded = true;
                cancellingGrounded = false;
                _normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingGrounded)
        {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded()
    {
        Grounded = false;
    }
}
