using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node {

    public List<Edge> edges; // all of the edges a node has. Up, down, left right etc.
    //public HashSet<Edge> edges;
    public NodeType nodeType;
    public Vector3Int worldPosition; //Position is stored at the key in dictionary.
    
    public enum NodeType {
        Walkable,
        Crouchable,
        Tile,
        DestructibleTile,
        Air,
        Water,
        Bridge,
        Cover,
    }

    public Node(NodeType _nodeType, Vector3Int _worldPos) {
        nodeType = _nodeType;
        worldPosition = _worldPos;
    }

    public bool HasThisEdge(Edge edge) {
        if(edges == null) {
            return false;
        } else {
            foreach (Edge e in edges)
            {
                if(e.CompareThisEdge(edge.from, edge.to))
                    return true; 
            } 
            return false;
        }
    }

    public void AddEdge(Edge edge) { // Edges are added in the world script but this help method for when we add the reversed edge.
        if(edges == null) // When adding reverse edge, the node still might not have edge list so make it. 
            edges = GetEdges();
        
        if(edge != null) {
            if(!HasThisEdge(edge)) {
                edges.Add(edge);
            } else if(HasThisEdge(edge)) {
                Debug.LogError("Reverse edge already had this edge: from: " + edge.from + " to: " + edge.to);   
            }
        }
        
    }

    public List<Edge> GetEdges() {
        if(edges == null) {
            return edges = new List<Edge>();
        } else {
            return edges;
        }
    }

    /// loop throu all of the neighbours throu edges, adds all "to" nodes into a list and returns it.
    public List<Node> GetNeighbours() {
        if(edges != null) {
            List<Node> n = new List<Node>();
            foreach (Edge e in edges)
            {
                n.Add(e.to);                
            }
            return n;
        }
        return null;
    }
}
