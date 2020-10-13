using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Edge {

    public Node from;
    public Node to;
    public int cost;
    public Vector2Int direction;
    public EdgeAction edgeAction;

    public enum EdgeAction {
        Stop, // This is used only by the Brain. When we met the last node, we reset the edge into this one and the move has switch case to stop.
        Move, // left or right, just walk or run
        JumpMove, // Jump and move left <> right
        Jump, // A.I Jump only above!
        Fall, // A.I needs to fall down to get next node (bridge)
        FallMove,
        CrouchMove // Crouch to get next node. (Tunnels)
    }

    /// Edge made from Node:From into Node:To. _Cost given here is jump, move, crouch etc amounts. 
    /// Method then adds directional cost also. 
    public Edge(Node _from, Node _to, int _cost) {
        from = _from;
        to = _to;
        direction = CalculateDirection(_from, _to);
        cost = _cost + Mathf.Abs(direction.x) + Mathf.Abs(direction.y); 
        //cost =+ direction.y >= 0 ? 4 : 2; // if the dir y is a jump, make cost double, fall only 1.
        
        // Left or Right
        if(direction == Vector2Int.right || direction == Vector2Int.left) {
            if(_to.nodeType == Node.NodeType.Crouchable) {
                edgeAction = EdgeAction.CrouchMove;
            } else {
                edgeAction = EdgeAction.Move;
            }
        }
        // Straight Down... 
        else if(direction.y < 0 && direction.x == 0) {
            edgeAction = EdgeAction.Fall;
        } 
        // Fall & Move 
        else if(Mathf.Abs(direction.x) == 1 && direction.y <= -1) {
            // if(to.nodeType == Node.NodeType.Bridge) { // next node is above the bridge, so we dont want to set the A.I to fall through.
            //     edgeAction = EdgeAction.Move;
            //     Debug.Log("We Changed the movement fall to move at: " + _from.worldPosition + " -> " + _to.worldPosition);
            // }
            edgeAction = EdgeAction.Move;   // this was fall Move before but it had problems when falling into a bridge.
                                            // if it will have problems on some cases, we add the some sort of if statement to fix it. 
        } 
        // Jump
        else if(direction.x == 0 && direction.y >= 1) {
            edgeAction = EdgeAction.Jump;
        }
        // Jump & Move 
        else if(Mathf.Abs(direction.x) >= 1 && direction.y >= 1) {
            edgeAction = EdgeAction.JumpMove;
        }
        // Jump Move Horizontally.
        else if(Mathf.Abs(direction.x) >= 2 && direction.y == 0) {
            edgeAction = EdgeAction.JumpMove;
            Debug.Log("Horizontal Jump");
        } 
        // Fall with jump.. 
        else if(Mathf.Abs(direction.x) > 1 && direction.y <= -1) { // "FallJump" aka x gap more than 1 tile + y fall.
            edgeAction = EdgeAction.JumpMove;
            // Debug.LogError("There is an edge with a jumpfall between: " + _from.worldPosition + " to " + _to.worldPosition + 
            //                 " Dir(" + direction.x + "," + direction.y + ")" +
            //                 " cost: " + cost);
        }
        // Failure, shouldnt go here? 
        else {
            edgeAction = EdgeAction.Stop;
            Debug.LogError("There is an edge with a stop at between: " + _from.worldPosition + " to " + _to.worldPosition + 
            " Dir(" + direction.x + "," + direction.y + ")" + 
             " cost: " + cost);
        }
        
    }

    Vector2Int CalculateDirection(Node _from, Node _to) {
        // var heading = target.position - player.position;
        Vector2Int dir = new Vector2Int();
        dir.x = _to.worldPosition.x - _from.worldPosition.x;
        dir.y = _to.worldPosition.y - _from.worldPosition.y;
        return dir;
    }

    public bool CompareThisEdge(Node _from, Node _target) {
        if(_from == from && _target == to) {
            return true;
        } else {
            return false;
        }
    }
}
