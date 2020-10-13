using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// only knows the parent and itself.
public class Pathway : IComparable<Pathway> {
    
    int cost;
    Node parent; // make the pathways as parenting. The next node with edge will always be child of the last one. 
    Node current;
    public Edge connectionEdge;

    public Node Parent{ get { return parent;} }
    public Node Current {get{ return current;} }
    //public Vector2Int nextDirection { get { return connectionEdge.direction; } } // edge connection between this current and the parent node: the direction.

    public Edge.EdgeAction Action() {
        return connectionEdge.edgeAction;
    }
    
    public string CurrentAction() {
        return connectionEdge.edgeAction.ToString();
    }
    
    public Vector2Int EdgeDirection() {
        return connectionEdge.direction;
    }

    public int CompareTo(Pathway other)
    {
        // if(this.Cost < other.cost) return -1;
        // if(this.cost == other.cost) return 0;
        // return 1;
        return this.Cost.CompareTo(other.cost); 
    }

    public int Cost { 
        get{return cost;} 
        set{cost = value;} 
    }

    /// Parent Node = Where the node came from
    /// Current node = This node.
    /// Edge is between parent and this.
    public Pathway(Node parentNode, Node currentNode, Edge _connectionEdge, int _cost) {
        parent = parentNode;
        current = currentNode;
        connectionEdge = _connectionEdge;
        cost = _cost;
    }

}
