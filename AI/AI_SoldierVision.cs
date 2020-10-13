using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class AI_SoldierVision : MonoBehaviour {

    private AI_SoldierBrain brain;
    [SerializeField] private Vector2 visionSize = new Vector2(10, 10);
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask targetsLayerMask;
    [SerializeField] private float tileDistanceMaxCheck = 3f;
    [SerializeField] private float tileDistanceCheck;
    private Vector2 tileCheckerUpper = new Vector2(0f, 0.25f); // these are starting positions for groundtile check. Aka Jump or crouch if we have one.
	private Vector2 tileCheckerLower = new Vector2(0f, -0.5f);
    private int checkInterval = 15; // modulo, check every x frame to save cpu
    
    private void Start() {
        brain = GetComponentInParent<AI_SoldierBrain>();
        tileDistanceCheck = tileDistanceMaxCheck;
    }

    private void Update() {
        // moving, send rays to check tiles so A.I will jump if so.
        // Edit: 12.2.2020. We are not checking tiles anymore. Using Dijkstra pathfinding.
        // if(brain.moving) {
        //     if(Time.frameCount % checkInterval == 0) {
        //         CheckIfTilesFront();
        //     }
        // }
        if(brain.attackTarget == null) {
            if(Time.frameCount % checkInterval == 0) {
                CheckEnemies();
            }
        }

        // attack target is in the area.
        if(brain.attackTarget != null) { 
            brain.enemyDistance = Vector2.Distance(transform.position, brain.attackTarget.position);

            CheckClearAim();

            if(brain.enemyDistance > 15) {
                brain.LostAttackTarget(); // lost the target, go to last target pos.
                brain.aI_SoldierDebug.attackTargetText.text = " - ";
            }
        }
    }

    void CheckIfTilesFront() {
        CalculateRayLenght(); // send the rays to maximum distance or until the target
        // Profiler.BeginSample("RayCasting");
        Vector2 lowerCheckPoint = new Vector2(transform.position.x, transform.position.y + tileCheckerLower.y);
        Vector2 upperCheckPoint = new Vector2(transform.position.x, transform.position.y + tileCheckerUpper.y);
        Debug.DrawRay(lowerCheckPoint, brain.moveDirection * tileDistanceCheck, Color.yellow);
        Debug.DrawRay(upperCheckPoint, brain.moveDirection * tileDistanceCheck, Color.yellow);
        RaycastHit2D lowerGroundHit = Physics2D.Raycast(lowerCheckPoint, brain.moveDirection, tileDistanceCheck, groundLayer);
        RaycastHit2D upperGroundHit = Physics2D.Raycast(upperCheckPoint, brain.moveDirection, tileDistanceCheck, groundLayer);
        //lowerPointCheckPoint, direction, tileDistanceCheck, groundLayer);
    
        if(lowerGroundHit.collider != null || upperGroundHit.collider != null) {
            //brain.thereIsTileInOurDirection = true;
        }
        else {
            //brain.thereIsTileInOurDirection = false;
        }
        // Profiler.EndSample();
    }

    void CalculateRayLenght() {
        if(brain.moveTarget != null) {
            if(tileDistanceMaxCheck >= Mathf.Abs( brain.moveTarget.x - transform.position.x) ) {
                tileDistanceCheck = Mathf.Abs(brain.moveTarget.x - transform.position.x);
            } else {
                tileDistanceCheck = tileDistanceMaxCheck;
            }
        } else if(brain.moveTarget == null) {
            tileDistanceCheck = 0f;
        }
    }

    void CheckEnemies() {
        Collider2D hitCollider = Physics2D.OverlapBox(transform.position, visionSize, 0f, targetsLayerMask);
        if(hitCollider != null) {
            brain.attackTarget = hitCollider.transform;
            brain.GetPathToTarget(brain.attackTarget.position);
            //brain.moveTarget = brain.attackTarget.position; // this we have to fix.. so the move target dont swap there from our nodes. Instead calculate path again
            if(OptionsData.debugMode) brain.aI_SoldierDebug.attackTargetText.text = hitCollider.gameObject.name;
        }
    }

    void CheckClearAim() {
        Vector2 upperAimPoint = new Vector2(transform.position.x, transform.position.y + tileCheckerUpper.y);
        Vector2 targetDirection = brain.attackTarget.position;
        targetDirection -= upperAimPoint; // to get the dir
        targetDirection.Normalize();

        Vector2 dir = brain.attackTarget.transform.InverseTransformDirection(targetDirection);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        brain.angle = (int)angle;

        int ground = 1 << LayerMask.NameToLayer("Ground"); 
        int target = 1 << LayerMask.NameToLayer("Player");
        int mask = ground | target; // combine these two layers, so the target checks if it hits ground or player.
        RaycastHit2D hit = Physics2D.Raycast(upperAimPoint, targetDirection, 10, mask);

        Color checkColor = Color.clear;
        if(hit.collider != null) {
            if(hit.collider.CompareTag("Player")) {
                brain.attackTargetOnSight = true;
                checkColor = Color.red;
            } else if(hit.collider.CompareTag("Tiles")) {
                checkColor = Color.yellow;
                brain.attackTargetOnSight = false;
            } 
        } else {
            brain.attackTargetOnSight = false;
            checkColor = Color.clear;
        }
        
        Debug.DrawRay(upperAimPoint, targetDirection * 10, checkColor);  
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;    
        Gizmos.DrawWireCube(transform.position, visionSize);
    }

    
}

