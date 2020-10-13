using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AI_SoldierBrain : MonoBehaviour {
    
    public Vector2 moveTarget; // next movement node! Never anything further
    public Transform attackTarget;

    private AI_SoldierInputs soldierInputs;
    public CharacterControls characterControls; // this is used here only for look direction & characterGrounded 
    public AI_SoldierDebug aI_SoldierDebug;
    // pathfinding
    private EnemyWayPoint current; // waypoints from the level.
    private EnemyWayPoint previous; 
    public bool isFindingNewTarget = false;
    
    public PathFinder pathFinder;
    public float waitAtTargetTime = .3f;
    Queue<Pathway> currentPath = new Queue<Pathway>(); // current path. Dequeue when A.I gets to that position.
    public Node.NodeType currentNodeType;
    public Edge.EdgeAction currentAction; // Move, Jump, Fall etc. Comes from Edge.
    public enum JumpSize { None, Small, Medium, High }
    public float[] jumpHoldsAmounts; // 0 small, 1 medium, 2 high 
    public JumpSize nextJumpSize;
    public bool jump = false;
    IEnumerator jumpHold; 

    public Vector2Int currentEdgeDir; // amount for the jumps and general direction.
    public float currentEdgeDirMagnitude;
    public int currentCost; 
    public int currentWayPointIndex = 0;
    public Vector2 moveDirection; // final input direction
    public Vector2 targetDistance;

    // public bool thereIsTileInOurDirection;
    // public bool moving = false;
    [SerializeField] private bool stuck = false;
    [SerializeField] private float stuckTimer = 3.5f;
    [SerializeField] private float stuckTime; 

    // Weapon Logic
    [Header("Weapon Logic")]
    public bool isShooting = false;
    public int angle; // set from vision, when target is found and inside range.
    public bool attackTargetOnSight = false; // Also set straight from vision.
    public float enemyDistance; // comes from Vision
    public bool aiming;

    IEnumerator shooting;
    public bool overrideAutomaticPathFinding = false; // this is debugging only. Mouse click overrides the waypoints and A.I stuck etc. 

    void Start() {
        current = EnemyWayPointManager.Instance.GetClosestWayPoint(this.transform.position);
        if(OptionsData.debugMode) aI_SoldierDebug.stateText.text = "Waypoints";
        soldierInputs = GetComponent<AI_SoldierInputs>();
        characterControls = GetComponent<CharacterControls>();
        characterControls.runSpeed = Random.Range(5.5f, 8f); // make the A.I have a slightly dif run speed so they dont overlap so easily.
        //Deb
        GameObject player = GameObject.FindGameObjectWithTag("Player"); 
        if(player != null) {
            if(OptionsData.debugMode) Debug.Log("A.I spawned, and we found the player", this);
            GoToPosition(player.transform.position); 
        } else {
            if(OptionsData.debugMode) Debug.Log("A.I spawned,no player found. Going to waypoints", this);
            FindNextMovementTarget(); // normal waypoint or attack target search.
        }
        
    }

    void Update() {
          // if enemy on sight or not, aim & shoot etc
        AttackOnSight();
        
        // Current Movement Target sets the next: moveTarget, currentAction and currentEdgeDir when we reached the current one.
        CurrentMovementTargetNode(); 
        
        // Movement states chosen by different nodes / edges & actions.
        CurrentMovement();
    }

    void CurrentMovementTargetNode() {
        // Calculate the distance to next node
        // make Vec3 into vec2.
        Vector2 pathFinderPos = pathFinder.transform.position;
        targetDistance = moveTarget - pathFinderPos;
        //targetDistance = Vector2.Distance(pathFinder.transform.position, moveTarget);
        
        // if we are falling straight down. Dont change the node until we are on ground. 
        // Remember: PathFinder checks always straight down and might get next one before we are on ground.
        // # Problem: A.I is falling and its bridge... it will fall through and get stuck on the ground.
        // Fix: Check that the current node is bridge, then pass over this??
        if(currentAction == Edge.EdgeAction.Fall && !characterControls._controller.isGrounded) {
            aI_SoldierDebug.WarningText("Falling and not grounded, waiting to be on ground, not bridge", 0.2f);
            return;
        }

        // Edge case:
        // when A.I has to fall from edge of the tile, but it resets into move and goes back over the tile.
        // this should take care of that.
        // PathFinder goes -30, but here we check only for the max jump height distance.
        if(currentAction == Edge.EdgeAction.FallMove && transform.position.y - pathFinderPos.y > 1.5f) {
            if(OptionsData.debugMode) Debug.LogError("We were move Falling and pathfinder was too far to reset the next node..");
            return;
        } 

        // Edge Case:
        // A.I is on JumpMove and the next node before jump is straight under, we have to land before make the jump!
        // also we have to check that this isnt bridge, because then it wont reset the bridge nodes.
        if( currentAction == Edge.EdgeAction.JumpMove
            && characterControls._controller.isGrounded == false
            && IsNextActionJump() ) // phiuf
        {
            //if(OptionsData.debugMode) Debug.Log("Now We Should Wait For tHe Ground tO Jump");
            if(OptionsData.debugMode) aI_SoldierDebug.waitingForGroundJumpToggle.enabled = true;
            return;
        } 
        
        // Edge Case: A.I is between tile and bridge and we set fall, but A.I is in the corner. 
        // fix: Resize pathfinder threshold, so the A.I moves more towards center
        if(currentAction == Edge.EdgeAction.Fall && currentNodeType == Node.NodeType.Walkable) {
            // pathFinder.targetThresholdDistance = 0.3f;
        } 

        if(OptionsData.debugMode) aI_SoldierDebug.waitingForGroundJumpToggle.enabled = false;

        // we met the next node. DeQueue, change the node...
        if(targetDistance.magnitude <= pathFinder.targetThresholdDistance) {
            NextMovementNode(); // get the next target, action and take it out from the queue.
        }
        
    }

    void CurrentMovement() {
        //direction = Vector2.zero;
        // Reset the jump hold when A.I hits the ground.
        if(characterControls._controller.isGrounded && jumpHold != null) {
            StopCoroutine(jumpHold);
            jump = false;
        }

        // ... ok here is an idea: if the next node distance is something very far.. we most likely have got stuck
        // So make a method that checks this and reset the target.
        // # ClosestNodeWayTooFar();

        // Stuck timer. A.I hasnt moved for a while in X. Why? 
        if( IsEntityHorizontallyInsideMoveTargetThreshold() && // We dont calculate on only vertical movements.
                                                    currentAction != Edge.EdgeAction.Stop || 
                                                    currentAction != Edge.EdgeAction.Jump ||
                                                    currentAction != Edge.EdgeAction.Fall) 
        {
            stuckTime += Time.deltaTime;
            if(OptionsData.debugMode) aI_SoldierDebug.stuckTimer.text = stuckTime.ToString();
            if(stuckTime >= stuckTimer) {
                if(OptionsData.debugMode) aI_SoldierDebug.WarningText("A.I been stuck enought, new plan");
                // lets makes some checks and create act accordingly.
                // if we are standing on a bridge, and the target is below:
                if(pathFinder.IsPathFinderAboveBridge() && IsMoveTargetStraightUnderUs() ) {
                    currentAction = Edge.EdgeAction.Fall;
                    stuckTime = 0;
                } else if( isFindingNewTarget == false) {
                    FindNextMovementTarget();
                } 
            }
        }  

        if( stuckTime > 20f) { // doesnt matter what edge.action is, if the A.I has been stuck over 30secs, force reset.
            StartCoroutine( ForceReset() );
        }

        // target is already very close, stop moving.
        if(attackTargetOnSight && enemyDistance < 2f) {
            currentAction = Edge.EdgeAction.Stop;
        }

        // Stop if we are at the end of the path., or there is no path at all..
        if(currentPath == null) {
            //Debug.LogError("current Path == null");
            currentAction = Edge.EdgeAction.Stop;
        } else if(currentPath.Count == 0) {
            if(OptionsData.debugMode) Debug.LogError("Path not a null, but count 0");
            currentAction = Edge.EdgeAction.Stop;
        }

        // Bread and butter of the movement.
        switch (currentAction) 
        {
            case Edge.EdgeAction.Stop:
                moveDirection = Vector2.zero;
                // ok, so enemy stopped, because enemyDistance is less 2. But lets make him move slightly another direction, bullets went throu
                if(enemyDistance < 1.5f && attackTarget != null) {
                    if(moveTarget.x < transform.position.x) {
                        moveDirection = Vector2.right;
                    } else if(moveTarget.x > transform.position.x) {
                        moveDirection = Vector2.left;
                    }
                }

                if(jumpHold != null) {
                    StopCoroutine(jumpHold);
                }
                break;

            case Edge.EdgeAction.Move:
                if(characterControls.isCrouched && GetCurrentNode().nodeType != Node.NodeType.Crouchable) {
                    characterControls.StandUp();
                }
                moveDirection = MoveDirection();
                break;
            
            case Edge.EdgeAction.Jump:
                if( !IsMoveTargetBelowY() )
                    Jump();

                // sometimes A.I got stuck, because its under a tile corner and jumping. So lets move ... slightly more.
                if( IsEntityHorizontallyInsideMoveTargetThreshold() == false ) {
                    if(moveTarget.x < transform.position.x) {
                        moveDirection = Vector2.right;
                    } else if(moveTarget.x > transform.position.x) {
                        moveDirection = Vector2.left;
                    } 
                } else { // we are inside the threshold, dont move horizontally.
                    moveDirection = Vector2.zero;
                }
                break;
            
            case Edge.EdgeAction.JumpMove:
                if(characterControls._controller.isGrounded)
                    Jump();
                
                // Move if we did hit the ground and jump...
                if(IsMoveTargetStraightUnderUs()) {
                    moveDirection = Vector2.zero;
                } else {
                    moveDirection = MoveDirection();
                }
                // If we are on Air, land before jumping.

                break;

            case Edge.EdgeAction.Fall:
                if(jumpHold != null) {
                    StopCoroutine(jumpHold);
                }
                // if target straight above fall,
                soldierInputs.GetDownLadder();  
                if(IsEntityHorizontallyInsideMoveTargetThreshold() == false) {
                    moveDirection = MoveDirection(); 
                } else 
                    moveDirection = Vector2.zero;
                // else fall & move.
                break;

            case Edge.EdgeAction.FallMove:
                moveDirection = MoveDirection();
                soldierInputs.GetDownLadder();
                break;
        
            case Edge.EdgeAction.CrouchMove:
                if(soldierInputs.IsCrouched() == false) {
                    soldierInputs.Crouch();
                }
                moveDirection = MoveDirection();
                break;

            
            default:
                Debug.LogError("Defaulted Case at currentAction", this);
                moveDirection = Vector2.zero;
                break;
        }

        if(OptionsData.debugMode) aI_SoldierDebug.actionText.text = currentAction.ToString();
        if(OptionsData.debugMode) aI_SoldierDebug.moveTargetSprite.transform.position = moveTarget;
        if(OptionsData.debugMode) aI_SoldierDebug.moveTargetText.text = moveTarget.ToString();
        
        soldierInputs.Move(moveDirection, jump, aiming); 
    }

    Vector2 MoveDirection() {
        Vector2 dir = Vector2.zero;

        if(moveTarget.x > pathFinder.transform.position.x + .1f /* targetThresholdDistance */ ) { // target is right, move rigth
                dir = Vector2.right;
        } else if(moveTarget.x < pathFinder.transform.position.x - .1 /* - targetThresholdDistance */) { // target is is left, move left.
                dir = Vector2.left;
        } 
        return dir;
    }

    bool IsEntityHorizontallyInsideMoveTargetThreshold() {
        return Mathf.Abs(targetDistance.x) <= pathFinder.targetThresholdDistance ? true : false;
    }

    bool IsMoveTargetStraightUnderUs() {
        if( IsEntityHorizontallyInsideMoveTargetThreshold() ) {
            if( IsMoveTargetBelowY() )
                return true;
        }
        return false;
    }

    bool IsMoveTargetBelowY() {
        return targetDistance.y <= -1f ? true : false;
    }

    // check from next from current, 0 + 1 action, if that is a jumping.. we have to wait to make the jump.
    bool IsNextActionJump() {
        if(currentPath == null) 
            return false; 

        if(currentPath.Count() > 1) {
            Pathway next = currentPath.ElementAt(1);
            if(next != null) {
                if(next.Action() == Edge.EdgeAction.JumpMove)
                    return true;
                else if(next.Action() == Edge.EdgeAction.Jump) {
                    return true;
                }
            }   
        }
        return false;
    }



    bool Stuck() {
        stuckTime += Time.deltaTime;
        if(stuckTime >= stuckTimer) {
            print("A.I has been stuck: " + stuckTime);
            stuckTime = 0;
            return true;
        }
        return false;
    }

    void AttackOnSight() {
        if(attackTarget == null) { // A.I Was stuck at shooting if player died and shoot was on. This should now reset when player == null
            attackTargetOnSight = false;
        }
        // if target distance is really close, always aim and dont flip player direction
        // else if distance is far or there is a tiles between target, stop aiming & flipping.
        if(attackTargetOnSight) {
            soldierInputs.AimAngle(angle);
            characterControls.AimMode(true);
            aiming = true;
            if(isShooting == false) {
                StartCoroutine("Shoot");
            }
        } else if(attackTargetOnSight == false) { // There might be tile between.
            if(enemyDistance < 6f && attackTarget != null ) {
                characterControls.AimMode(true);
                soldierInputs.AimAngle(angle);
                aiming = true;
            }
            else { // Player behind tile and far away.
                characterControls.AimMode(false);
                soldierInputs.ResetAim(); // reset the angle
                aiming = false;
            }
            isShooting = false; // this should be enough to "stop" the coroutine. It just breaks out.
        }
    }

    void Jump() {
        if(jump == true) // only start the coroutines once. So dont start them 50x per frame.
            return;

        currentEdgeDirMagnitude = currentEdgeDir.magnitude;
        if( currentEdgeDirMagnitude < 2.0) {
            nextJumpSize = JumpSize.Small;
        } 
        else if(currentEdgeDirMagnitude >= 2.0 && currentEdgeDirMagnitude <= 3) {
            nextJumpSize = JumpSize.Medium;
        } 
        else if(currentEdgeDirMagnitude > 3) {
            nextJumpSize = JumpSize.High;
        }
        
        switch (nextJumpSize)
        {
            case JumpSize.None:
                jump = false;
                break;

            case JumpSize.Small:
                jumpHold = JumpWithHold(jumpHoldsAmounts[0]);
                if(OptionsData.debugMode) aI_SoldierDebug.jumpSizeText.text = "S: " +  jumpHoldsAmounts[0];
                StartCoroutine(jumpHold); // test and find out the correct values for these.
                break;

            case JumpSize.Medium:
                jumpHold = JumpWithHold(jumpHoldsAmounts[0]);
                if(OptionsData.debugMode) aI_SoldierDebug.jumpSizeText.text = "M: " +  jumpHoldsAmounts[1];
                StartCoroutine(jumpHold);
                break;

            case JumpSize.High:
                jumpHold = JumpWithHold(jumpHoldsAmounts[2]);
                if(OptionsData.debugMode) aI_SoldierDebug.jumpSizeText.text = "H: " +  jumpHoldsAmounts[2];
                StartCoroutine(jumpHold);
                break;

        }
    }
    
    IEnumerator JumpWithHold(float holdTime) {
        if(OptionsData.debugMode) aI_SoldierDebug.jumpToggleImage.enabled = true;
        jump = true;
        yield return new WaitForSeconds(holdTime);
        jump = false;
        if(OptionsData.debugMode) aI_SoldierDebug.jumpToggleImage.enabled = false;
    }

    IEnumerator Shoot() {
        isShooting = true; 
        yield return new WaitForSeconds(Random.Range(.3f, 0.8f));
        while(isShooting) {
            soldierInputs.Shoot();
            yield return null;
        }
    }

    // get the next movement target to go and find out the path into it.
    // If player OnSight that will be our target. If not, find a some waypoint from the level.
    public void FindNextMovementTarget() {
        if(OptionsData.debugMode && overrideAutomaticPathFinding)
            return;

        isFindingNewTarget = true;
        if(attackTarget == null) { // no target in vision. Get next waypoint from world arraylist.
            pathFinder.maxSteps = 500;
            GetNextWayPoint();
            if(OptionsData.debugMode) {
                aI_SoldierDebug.stateText.text = "Waypoints"; 
                aI_SoldierDebug.stateText.color = Color.green;
            }
        } else if(attackTarget != null) {
            pathFinder.maxSteps = 80;
            // calculate the way to the player.
            GetPathToTarget(attackTarget.position); // added 2.3: Not tested..
            if(OptionsData.debugMode) {
                aI_SoldierDebug.stateText.text = "Attacking";
                aI_SoldierDebug.stateText.color = Color.yellow;
            } 
        }
    }

    public void GoToPosition(Vector2 position) {
        GetPathToTarget(position);
    }

    // makes always a movement path for given target
    public void GetPathToTarget(Vector2 movePositionTarget) {
        if(OptionsData.debugMode) aI_SoldierDebug.MovePositionTarget(movePositionTarget);

        if(currentPath != null)
            currentPath.Clear();
        
        currentPath = pathFinder.CreatePath(movePositionTarget);
        
        if(currentPath != null) {
            isFindingNewTarget = false;    
            NextMovementNode(); // get first node from the path and make it as a target.
        } else if(currentPath == null) {
            // So what we do now? movePosTarget is out of reach, pretty much.
            // wait until the player is at reach?
            if(attackTarget != null) {
                // wait until the attack target is reachable, so check again in few seconds.
                Invoke("FindNextMovementTarget", 2f);
            }

            if(OptionsData.debugMode) {
                aI_SoldierDebug.WarningText("Tried to make new path but returned null");
                Debug.LogError("GetPathToTarget: Tried to make a new path but finally returned");
            }

        }
    }

    // lost the attack target, get path to its last know place!
    public void LostAttackTarget() {
        GetPathToTarget(attackTarget.position);
        soldierInputs.ResetAim();
        attackTarget = null;
    }

    [ContextMenu("Get NextWayPoint")]
    public void GetNextWayPoint() {
        if(current == null) 
            current = EnemyWayPointManager.Instance.GetClosestWayPoint(this.transform.position);
        
        if(previous == null) // added this, because at the start we dont have nothing.
            previous = EnemyWayPointManager.Instance.GetClosestWayPoint(this.transform.position);

        EnemyWayPoint tempPrev = current; // to save the previous, so we dont choose it when are getting the next one.
        current = current.GetNext(previous);
        previous = tempPrev;
        GetPathToTarget(current.transform.position);
       // Invoke("SetFindingTargetToFalse", 0.5f); // reset timer, so update wont looppi loop until the stuck timer has been reset.
    }
    
    public IEnumerator ForceReset() {
        if(OptionsData.debugMode) Debug.LogWarning("A.I Force reset started");
        stuckTimer = 0f;
        currentAction = Edge.EdgeAction.Fall; // usually stuck over bridge? 
        attackTarget = null;
        isFindingNewTarget = false;
        yield return new WaitForSeconds(0.5f);
        currentAction = Edge.EdgeAction.JumpMove; // do this have an effect?
        FindNextMovementTarget();
    }

    public void SetFindingTargetToFalse() {
        print("IsFinding Invoked to false");
        isFindingNewTarget = false;
    }

    // get next movement node, set the current action, dequeu it.
    void NextMovementNode() {
        if(isFindingNewTarget) 
            return;

        if(currentPath == null) {
            if(OptionsData.debugMode) Debug.LogError("Current Path == null, finding new one");
            if(!isFindingNewTarget) {
                FindNextMovementTarget();
            }
        } else if(currentPath.Count > 0) {
            stuckTime = 0f;
            Vector3Int nextTargetNodePos = currentPath.Peek().Current.worldPosition;
            moveTarget = new Vector2(nextTargetNodePos.x + .5f, nextTargetNodePos.y + .5f); // and when this is met at Move(), pop it out: Also fix the pivot to center of the tile.
            currentAction = currentPath.Peek().Action();
            currentEdgeDir = currentPath.Peek().EdgeDirection();
            currentCost = currentPath.Peek().Cost;
            currentNodeType = currentPath.Peek().Current.nodeType;
            if(OptionsData.debugMode) aI_SoldierDebug.nodeText.text = currentPath.Peek().Current.nodeType.ToString();
            currentPath.Dequeue(); 
        } else if(currentPath.Count == 0) { // if we arrived to the last node. Stop and clear the path. And find something else to do.
            print("NextMovementNode: Path count == 0");
            if(isFindingNewTarget == false) {
                isFindingNewTarget = true;
                currentPath.Clear();
                Invoke("FindNextMovementTarget", waitAtTargetTime);
            }
        }
    }
    
    Node GetCurrentNode() {
        return currentPath.Peek().Current;
    }

    // private void OnTriggerEnter2D(Collider2D other) {
    //     if(OptionsData.debugMode) Debug.Log("Enemy triggered with " + other.name, gameObject);
    //     if(other.CompareTag("WayPoint")) {
    //         if(OptionsData.debugMode) Debug.LogWarning("AI touched waypoint. ");
    //         // if this way point was our waypoint target. Find Next one.
            
    //     }
    //     if( other.CompareTag("Node") ) {
    //         if(OptionsData.debugMode) Debug.LogError("AI is on movement node");
    //     }
    // }

    // private void OnDrawGizmos() {
        //ScatteriaUtility.DrawString("X", moveTarget, Color.green, Color.black);
        
    // }
}