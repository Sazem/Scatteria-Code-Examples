using UnityEngine;
using System.Collections;
using System;
using Prime31;

// This script done years ago, needs some refactoring & clean up.
public class CharacterControls : Bolt.EntityBehaviour<IPlayerState> {

    public int playerNumber; // base number! Täältä muihin.. ja tänne GM:ltä
    public int aimStates = 0; // tärkeä kuin myös...
    public bool aimingMode = false; // aimingMode. Eli tähtääkö vai ei. Täältä WeaponScriptille ja sieltä CameraTargetille (positio)
    public CameraTarget cameraTargetScript;
    public Vector3 cameraTargetPos; // this is the vector between crosshair and player
    public bool playerDead = false;
    public Camera playerCamera;

    public bool isFacingRight = true;
    public bool isCrouched = false;
    public bool pressingDown = false;
    public int angle; // important number for weapon etc. Only positive numbers.
    public Inventory inventory; // määritellään nämäkin käsin, vaikka niin, että ne on aina olemassa vaikkei aseita olisikaan.. 
    private SpriteRenderer spriteRender;
    private Animator animator;
    public BoxCollider2D boxCollider;
    [Header("Movement Values")]
    // Movement scripts
    public float gravity = -25f;
    public float runSpeed = 8f;
    public float crouchSpeed = 5f;
    public float aimModeSpeed = 1.5f; // 
    public float currentSpeed;
    public float hipSpeed;
    public float groundDamping = 20f; // how fast we can change direction? // higher means faster
    public float inAirDamping = 5f;
    public float jumpHeight = 6f;
    public float extraJumpBoostMultiplier; // jump is hold, how much we add to the extra jump.
	public float airTimer = 0f;
    public float maxJumpTime = 1f; // if space is hold, falling begins after this.
    public float minExtraJumpTime = 0.25f; // when jump is hold, after this time the extra jumps begins
    public float fallMultiplier; // how much the gravity increases when falling starts.
    public bool hasMaxJumped = false;
    public bool playerIsOnWater = false;

    public CharacterController2D _controller;
    private RaycastHit2D _lastControllerColliderHit;
    [SerializeField]
    private Vector3 _velocity;
    public Vector3 _Velocity { get {return _velocity; } }
    private float normalizedHorizontalSpeed = 0;
    public bool jumpReleased = true;
    public Shooter shooter;
    public ThroughGrenade currentGrenadeScript;
    public BoxCollider2D boxCol;
    public Vector2 boxColOrgSize;
    public Vector2 boxColOrgOffset;

    public LayerMask groundLayerMask;
    public LayerMask debrisMask; // objects that are moved by player falling hard enought and close to these. These are tagged PhysicItems.
    public SpriteRenderer hands, backpack, pants, torso; // New body parts for flipping
	public GameObject backpackGo;
    // the special state checkers, ex: character is crouched & undercover & aiming down.. send state to take cover.
	private Vector2 duckAndCoverStartPosUpper = new Vector2(0f, 0.25f);
	private Vector2 duckAndCoverstartPosLower = new Vector2(0f, -0.5f);
    public bool duckAndCover = false;
    public LayerMask coverLayerMask;
    [Header("FX")]
    public GameObject groundBuff;
    public AudioClip[] jumpSfx;
    public AudioClip[] groundBuffSounds;
	public Animator weaponStateMachine;
	public GameObject aimingAssistPixel;
    public Vector2 aimingAssistAimPositio;
    public AudioSource audioSource;
	// state changers, these are for the parts that use sprites in array to look different dir
	public StateChanger headChanger, helmetChanger;
    
    void Start () {
        groundLayerMask.value = 256; // set groundlayermaks value to groundLayer
        animator = GetComponent<Animator>();
        currentSpeed = runSpeed;

        boxCol = GetComponent<BoxCollider2D>();
        boxColOrgSize = boxCol.size;
        boxColOrgOffset = boxCol.offset;
        audioSource = GetComponent<AudioSource>();	
    }

	void Update () {
	 	// calculate states
		aimStates = State();

		// DuckAndCover
		DuckAndCover();

		// Rotate the aiming assist pixel
		aimingAssistPixel.transform.rotation = Quaternion.Euler(0,0, angle);
    }
    
	public int State() {
		// If full angle is 90". 90 / 12 gets the states.
		int state = angle / 24;
		int index = 0;
		// up states goes from 0 - 3. So if its smaller than 4.. its looking up and right.
		if (state < 4) {
			index = state;
		} 
		// States right and down. Goes from to 11 -> 13.
		else if (state > 10 && state < 14) {
			// if ex, state is 11.. -7 = 5-1 indx.
			index = state - 7; // -1 is the menus index in array.
		}
		// Left side of states. up
		else if (state == 4) { // my brain wont work for basic algebra. hence many ifs.
			index = 3;
		} else if (state == 5) {
			index = 2;
		} else if (state == 6) {
			index = 1;
		} else if (state == 7) {
			index = 0;
		}
		// left side down
		else if (state == 8) {
			index = 6;
		} else if (state == 9) {
			index = 5;
		} else if (state == 10) {
			index = 4;
		}

		inventory.shooter.RotateWeapon(angle); // lets set angle there.
		if (aimingMode) {
			// headChanger.changeState (index);
			// helmetChanger.changeState (index);
		}
	
		return index;
	}

    int CalculateFlippedAngle(int currentAngle) {
        int newAngle = 0;
        // between zero and 90.
        if(currentAngle >= 0 && currentAngle <= 180) {
            newAngle = 180 - currentAngle;
        } 
        else if (currentAngle >= 181 && currentAngle <= 270) 
        { 
            newAngle = 270 - currentAngle; 
            newAngle += 270;
        } else if(currentAngle >= 271 && currentAngle <= 359) {
            newAngle = currentAngle - 270;
            newAngle = 270 - newAngle;
        }
        return newAngle;
    }

	public void Direction(int angle) {
		// if angle is on the left side and player is still looking right.
		if (angle > 90 && angle < 269 && isFacingRight) {
			ChangeDirection (false);
		} 
		// change back to right
		if (angle <= 90 && !isFacingRight) {
			ChangeDirection (true);
		}
		if (angle >= 270 && !isFacingRight) {
			ChangeDirection (true);
		}

	}

	void ChangeDirection(bool lookRight) {
		
		isFacingRight = lookRight;
		// we are looking left.
		if (isFacingRight == false) {
			headChanger.FlipX(true);
			torso.flipX = true;
			pants.flipX = true;
			backpack.flipX = true;
		    //helmetChanger.FlipX(true); // head is now made with -1, so it changes the helmet too.
			inventory.shooter.flipTheWeaponSprite (true);
            inventory.shooter.lookingRight = false;
			// crouch position aiming has x 0.1. if its looking left we have to changed it into 0.1;
			if (aimingMode) {
				Vector2 adjustedWeaponX = new Vector2 ();
				adjustedWeaponX.x = shooter.transform.localPosition.x * -1;
				adjustedWeaponX.y = shooter.transform.localPosition.y;
				shooter.transform.localPosition = adjustedWeaponX;
				aimingAssistPixel.transform.localPosition = adjustedWeaponX;
			}	


		} else if(isFacingRight){
			headChanger.FlipX(false);
			torso.flipX = false;
			pants.flipX = false;
			backpack.flipX = false;
			inventory.shooter.flipTheWeaponSprite (false);
            inventory.shooter.lookingRight = true;
			helmetChanger.FlipX(false);
			// crouch position aiming has x 0.1. we have to make sure it is not -0.1
			if (aimingMode) {
				Vector2 adjustedWeaponX = new Vector2 ();
				adjustedWeaponX.x = shooter.transform.localPosition.x * -1;
				adjustedWeaponX.y = shooter.transform.localPosition.y;
				shooter.transform.localPosition = adjustedWeaponX;
				aimingAssistPixel.transform.localPosition = adjustedWeaponX;
			}	
		}
	}
    
	void DuckAndCover() {
		// sends Raycasts to see if there is a one tile in front of the player which he can cover.
		Vector2 direction = new Vector2 ();
		if (isFacingRight) {
			direction.x = 1f;
		} else if (isFacingRight == false) {
			direction.x = -1f;
		}
		// these are the two lower points from player casting side to the player.
		Vector2 lowerStartPoint = new Vector2(transform.position.x, transform.position.y + duckAndCoverstartPosLower.y); 
		Vector2 upperStartPoint = new Vector2(transform.position.x, transform.position.y + duckAndCoverStartPosUpper.y);

		// these two are pretty much only for the drawline because it needs and end point.
		Vector2 lowerEndPoint = new Vector2(transform.position.x + 0.25f * direction.x, lowerStartPoint.y);
		Vector2 upperEndPoint = new Vector2(transform.position.x + 0.25f * direction.x, upperStartPoint.y);
		Debug.DrawLine (lowerStartPoint, lowerEndPoint, Color.white);
		Debug.DrawLine (upperStartPoint, upperEndPoint, Color.white);

		// if we are crouched and next to a tile. send to animator duck and cover state. Also we could set the max angles too.
		RaycastHit2D hitLower = Physics2D.Raycast(lowerStartPoint, direction, 0.25f, coverLayerMask);
		RaycastHit2D hitUpper = Physics2D.Raycast(upperStartPoint, direction, 0.25f, coverLayerMask);
		if (isCrouched) {
			// so if our upper ray isnt having a tile and lower is, then duck and cover.
			if (hitUpper.collider == null && hitLower.collider != null) {
				animator.SetBool ("TakeCover", true);
                weaponStateMachine.SetBool("Cover", true); 
				duckAndCover = true;
                headChanger.HeadToFixedXinCoverPose(); // multiply the position of the head by localscale to get the head/helmet into cover state
				if (aimingMode == false) {
					boxCollider.offset = new Vector2(0f, -0.49f);
					boxCollider.size = new Vector2(0.45f, 0.97f);
					_controller.recalculateDistanceBetweenRays();
				} else if(aimingMode) {
					boxCollider.offset = new Vector2(0f, -0.25f);
					boxCollider.size = new Vector2(0.45f, 1.48f);
					_controller.recalculateDistanceBetweenRays();
				}
			} else {
				animator.SetBool ("TakeCover", false);
                headChanger.HeadToDefaultPos();
                weaponStateMachine.SetBool("Cover", false);
				boxCollider.offset = new Vector2(0f, -0.5f);
				boxCollider.size = new Vector2(0.45f, 0.99f);
				_controller.recalculateDistanceBetweenRays();
				duckAndCover = false;
			}
			return;
		}
	
		// if the player starts standing..
		if (!isCrouched) {
			duckAndCover = false;
            weaponStateMachine.SetBool("Cover", false);
		}
	}

    public void Move(float moveValue, bool jumped, PlayerInputs.ControllerType controllerType, bool overrideFlip)
    {
        if(jumped) {
            GetDownLadders();
        }

        if(_controller.isGrounded == false) {
            airTimer += Time.deltaTime;
            if(!jumped) {
                jumpReleased = true;
            }
        }

        if (_controller.isGrounded) {
            _velocity.y = 0;
            airTimer = 0f;
            hasMaxJumped = false;
            //invertGravity = gravity * airTimer;
			//jumpHoldTimer = 0f;
            animator.SetBool("Grounded", true);
        }
        // velocity into animator..
        animator.SetFloat("vSpeed", _velocity.y);

        // gives running speed to animator
        animator.SetFloat("Speed", Mathf.Abs(_velocity.x));

        if (moveValue > 0)
            normalizedHorizontalSpeed = moveValue;
        else if (moveValue < 0)
            normalizedHorizontalSpeed = moveValue;
        else
            normalizedHorizontalSpeed = 0;
        
		// jump
        if (_controller.isGrounded && jumped) {
            if(isCrouched)
                StandUp();

            //GetDownLadders(); // we set the bridge off for a second..
            if( entity.IsOwner && audioSource != null) // only play jumping sound on the controller. So server wont hear every jump sound..
            {
                int randomSoundIndx = UnityEngine.Random.Range(0, jumpSfx.Length);
                if(audioSource.isPlaying == false)
                {
                    audioSource.PlayOneShot(jumpSfx[randomSoundIndx]);
                }
            }
            //jumpIsPressed = true;
            //canMaxJump = true;
            _velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity); // +11.5.
            //print("vel.y: " + _velocity.y);
			jumpReleased = false;
            animator.SetTrigger("Jumped");
        }
		// higher jump. Muista jumped release. Eli ei voi näpäyttää uudelleen lentoon.
		if (jumped && !jumpReleased && _velocity.y != 0 && airTimer < maxJumpTime && hasMaxJumped == false)
        {
            if(airTimer > minExtraJumpTime)
            {
                _velocity.y += Mathf.Sqrt(2f * extraJumpBoostMultiplier * -gravity); 
                hasMaxJumped = true;
            }
        } 

        // if we are falling, lets but the bridge back on after jumped.. 
        if(_velocity.y < 0.0f && pressingDown == false) 
            DontGetDownLadders();

        // apply horizontal speed smoothing it. dont really do this with Lerp. Use SmoothDamp or something that provides more control
        var smoothedMovementFactor = _controller.isGrounded ? groundDamping : inAirDamping; // how fast do we change direction?

        if (_controller.isGrounded == false)
            animator.SetBool("Grounded", false);
        // tässä lasketaan lopullinen juoksunopeus,
        //currentWeaponScript.setSpreaderRange(Mathf.Abs(_velocity.x)); // annetaan aseelle juoksun vauhti, jonka kanssa lasketaan aseen spread.
        _velocity.x = Mathf.Lerp(_velocity.x, normalizedHorizontalSpeed * currentSpeed, Time.deltaTime * smoothedMovementFactor);
       
        // apply gravity before moving. if velocity higher than 0, add normal. When smaller, drop faster.
        _velocity.y = _velocity.y > -0.5f || !jumpReleased && airTimer < maxJumpTime ? // && jump Released. here also, that when player releases the jump, falls begings faster.
                    _velocity.y += gravity * Time.deltaTime 
                    : 
                    _velocity.y += gravity * fallMultiplier * Time.deltaTime;

        _controller.move(_velocity * Time.deltaTime);

        // for controller and "keyboard-only", auto flipping the directions.
        if(controllerType  ==   PlayerInputs.ControllerType.Controller || 
                                controllerType == PlayerInputs.ControllerType.KeyboardOnly || 
                                controllerType == PlayerInputs.ControllerType.KeyboardOnlyPlayerTwo) { 
            // if player is not using rightStick to override the flip. => Flip..
            // and is not already looking that direction where he is running.
            if(isFacingRight && _velocity.x < -0.25f && !overrideFlip || !isFacingRight && _velocity.x > 0.25f && !overrideFlip) {
                // lets just calculate the angles, and let that change the direction of the player.
                angle = CalculateFlippedAngle(angle);
                isFacingRight =! isFacingRight;
                ChangeDirection(isFacingRight);
            }
        }

        // if player is falling down fast...
        if(_velocity.y < -22f && _controller.isGrounded)
        {
            InstantiateGroundBuff();
        }

        // grab our current _velocity to use as a base for all calculations
        _velocity = _controller.velocity;
    }
    
    public void Reload() {
        if(shooter.weapon.maxBulletsPerClip != state.Clip)
            shooter.StartReload();
    }

    void onControllerCollider(RaycastHit2D hit)
    {
        // bail out on plain old ground hits cause they arent very interesting
        if (hit.normal.y == 1f)
            return;
    }

    public void Shoot()
    {
        //if(!shooter.isCurrentlyReloading) {
            //shooter.Shoot();
            Debug.LogError("Piu piu! Seriously thou, the sp bullet was removed so.. there is nothing to shoot.");
        //}
    }

    public void TriggerWasReleased() {
        shooter.triggerReleased = true;
    }

    public void Jump() {
        // Aiming and crouching off from weapon..
        weaponStateMachine.SetBool("Crouched", false);
        weaponStateMachine.SetBool("Aiming", false);
    
        if (isCrouched) {
            StandUp();
        }

        if(aimingMode) {
            aimingMode = false;
            currentSpeed = runSpeed;
        }
			
    }

    public void NextWeapon()
    {
        if(inventory.changingWeapon == false) // if we are not already changing weapon..
            inventory.StartCoroutine("NextWeapon");
        CheckAndCorrectDirectionOfWeapon();
    }

    public void PreviousWeapon() {
        if(inventory.changingWeapon == false)
            inventory.StartCoroutine("PreviousWeapon");
        CheckAndCorrectDirectionOfWeapon();
    }
    
    public void NextGrenade()
    {
    }
    
    /// Change weapon by its index on weapons inventory list
    public void ChangeWeapon(int index) {
        if(inventory.changingWeapon == false) {
            inventory.StartCoroutine("ChangeWeaponWithIndex", index);
        }
        CheckAndCorrectDirectionOfWeapon();
    }

    public void Crouch(PlayerInputs.ControllerType controllerType) // toggle
    {
        // keyboard-only: If player crouched over bridge, it drops player.
        if(controllerType == PlayerInputs.ControllerType.KeyboardOnly || controllerType == PlayerInputs.ControllerType.KeyboardOnlyPlayerTwo)
        {
            // we check with raycast if there is a platformer under the player.
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.1f, _controller.oneWayPlatformMask);
            if(hit.collider != null)
            {
                // we drop the player
                // GetDownLadders();
                PressingDown();
                Invoke("PressingDownReleased", .1f);
                // and return
                return;
            }

        }

        if(isCrouched) { // if already crouched => standup!
            StandUp();

            // vanha.. tehdään tsekki nyt tuolla standupissa koska A.I tulee suoraan sinne.
            // RaycastHit2D standupCheckRay = Physics2D.Raycast(transform.position, Vector2.up, 0.5f, groundLayerMask); 
            // if(!standupCheckRay) {
            //     StandUp();
            // }

        } else if(!isCrouched) { // if not crouched already => Crouch!
			weaponStateMachine.SetBool ("Crouched", true);

			if (!aimingMode) {
				currentSpeed = crouchSpeed;
			}
			if (aimingMode) {
				currentSpeed = crouchSpeed / aimModeSpeed;
			}
			boxCollider.offset = new Vector2(0f, -0.5f);
            boxCollider.size = new Vector2(0.45f, 0.99f);
            _controller.recalculateDistanceBetweenRays();
            
            animator.SetBool("Crouched", true);
            isCrouched = true;
        }
    }

    public void PressingDown() {
        pressingDown = true;
        GetDownLadders();
    }

    public void PressingDownReleased() {
        pressingDown = false;
        DontGetDownLadders();
    }

    public void GetDownLadders(){
         _controller.platformMask = 0 << 12 | 1 << 8;
    }

    public void DontGetDownLadders() {
        _controller.platformMask = 1 << 12 | 1 << 8;
    }
    
    public void StandUp ()
    {
        RaycastHit2D standupCheckRay = Physics2D.Raycast(transform.position, Vector2.up, 0.5f, groundLayerMask); 
        if(!standupCheckRay) {
            animator.SetBool("Crouched", false);
            boxCollider.size = new Vector2(0.45f, 1.67f);
            boxCollider.offset = new Vector2(0.0f, -0.15f);
            if (aimingMode) {
                currentSpeed = runSpeed / aimModeSpeed;
            }
            if (!aimingMode) {
                currentSpeed = runSpeed;
            }
            isCrouched = false;
            weaponStateMachine.SetBool ("Crouched", false);
            boxCol.offset = boxColOrgOffset;
            boxCol.size = boxColOrgSize;
            _controller.recalculateDistanceBetweenRays();
        }
    }  
    
    public void AimMode(bool aimingModeOn)
    {
        aimingMode = !aimingModeOn;
        // tämä on siis toggle. AimMode näppäintä juuri painettu.
        if(aimingMode) // jos aiming mode on jo päällä, niin ota se pois. 
        {
            weaponStateMachine.SetBool("Aiming", false);
            if(isCrouched)
            {
                currentSpeed = crouchSpeed;
            } else
            {
                currentSpeed = runSpeed;
             
            }
			animator.SetBool ("Aiming", false);
            aimingMode = false;
          
        } else if (aimingMode == false) { // if aiming mode was off, turn it on (if weapon has aim mode) 
            if (isCrouched) {
                currentSpeed = crouchSpeed / aimModeSpeed;
            } else {
                currentSpeed = runSpeed / aimModeSpeed;
            }
            aimingMode = true;
            weaponStateMachine.SetBool("Aiming", true);
			animator.SetBool ("Aiming", true);
        }
        shooter.ZoomCameraTarget(aimingMode);
    }

    public void ChargeGrenade() {
		if(currentGrenadeScript != null)
        	currentGrenadeScript.charge();
    }

    public void ThrouGrenade() {
		if(currentGrenadeScript != null)
        	currentGrenadeScript.release();
    }  

    public void TakeOffAiming() {
        aimingMode = false;
        weaponStateMachine.SetBool("Aiming", false);
    }
    
    public void InstantiateGroundBuff() {
        // instantiate Buff fx on feet.
        Vector2 InstantPos =  this.gameObject.transform.position;
        float x = -0.005f;
        float y = -1f;
        InstantPos += new Vector2(x, y);
        Instantiate(groundBuff, InstantPos, Quaternion.identity);
        if(_velocity.y < -15f) {
            int randomSoundIndx = UnityEngine.Random.Range(0, groundBuffSounds.Length);
            if(audioSource.isPlaying == false) // we dont want to many instances to be played same time.
            {
                audioSource.PlayOneShot(groundBuffSounds[randomSoundIndx]);
            }

            // Make a small physic objects (debris) to push a side little.
            Collider2D[] colliders = Physics2D.OverlapCircleAll(InstantPos, .8f, debrisMask);
            foreach(Collider2D collider in colliders) {
                Rigidbody2D rb = collider.GetComponent<Rigidbody2D>();
                if(rb != null)
                {
                    var dir = rb.transform.position - transform.position;
                    float calc = 1 - dir.magnitude / 3f; // 0.4f = radius of the "force", or explosion etc.
                    if(calc <= 0)
                        calc = 0;
                    rb.AddForce(dir.normalized * 1000 * calc); // 200 = push force;
                }
            }
        }
    }

    public void ChangedWeaponSetToBasics() {
        currentSpeed = runSpeed;
        //shooter.EnableSetups();
        CheckAndCorrectDirectionOfWeapon();
    }

    public void CheckAndCorrectDirectionOfWeapon() {
         if(!isFacingRight) {
            inventory.shooter.flipTheWeaponSprite(true);
        } else {
            inventory.shooter.flipTheWeaponSprite(false);
        }
    }
}
