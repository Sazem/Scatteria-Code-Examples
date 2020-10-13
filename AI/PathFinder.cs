using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

public class PathFinder : MonoBehaviour {
    
    public enum Algorithm
    {
        BreadthFirst, Diksjtra, Astar    
    };
    
    public Algorithm algorithm;
    public int maxSteps = 500; // Change this number debending if the target is close, or far. I mean is player is visible, no need 500, instead 100 or less?
    Node startNode; // status node
    Node targetNode;
    public float targetThresholdDistance = .32f;
    public Transform targetTest; // was v3? target but this cames from brain so I changed this to debug
    public World world;
    Dictionary<Vector3Int, Node> nodes; // all nodes in the grid.

    Queue<Pathway> currentPath = new Queue<Pathway>(); // This is the final path.
    HashSet<Node> closetSet;

    // fixing the pathfinder to move above the ground.
    Vector2 pathFinderOrgPosition;
    public LayerMask pathfinderCheckLayer; // bridge & ground
    public int currentPathFinderIsAboveLayer; // 8 ground, 12 bridge.

    private void Start() {
        pathFinderOrgPosition = transform.localPosition;
        algorithm = Algorithm.Diksjtra;
        GetCurrentNodes(); // find the world and all of the nodes.
    }

    private void Update() {
        PathFinderPosition();

        if(OptionsData.debugMode) { // slowdown for debugging to see better the node changes.
            if(Input.GetKeyDown(KeyCode.B)) {
                Time.timeScale *= 0.5f;
            } 
            
            if(Input.GetKeyDown(KeyCode.N)) {
                Time.timeScale *= 2f;
            } 

            if(Input.GetKeyDown(KeyCode.M)) {
                Time.timeScale = 1.0f;
            }
        }
    }

    void PathFinderPosition() {
        if(transform.parent == null) {
            return;
        }
        Vector2 parentFixedPos = new Vector2(transform.parent.position.x, transform.parent.position.y - 0.5f);
        RaycastHit2D hit = Physics2D.Raycast(parentFixedPos, -Vector2.up, 30f, pathfinderCheckLayer);
        if(hit.collider != null) {
            //print("HitP: " + hit.point + " its: " + hit.collider.name);
            Vector2 fixedPoint = hit.point;
            fixedPoint.y = hit.point.y + 0.5f;
            transform.position = fixedPoint;
            currentPathFinderIsAboveLayer = hit.collider.gameObject.layer;
        } else {
            transform.position = parentFixedPos;
            currentPathFinderIsAboveLayer = -1;
           // currentPathFinderIsAboveLayer =
        }
    }

    public bool IsPathFinderAboveBridge() {
        // if(OptionsData.debugMode) {
        //     string perkle = currentPathFinderIsAboveLayer == 12 ? "Tämä oli siltä" : "Ei ollut silta";
        //     print(perkle);
        // }
        return currentPathFinderIsAboveLayer == 12 ? true : false;
    }

    public void MakePathInspector() {
        Vector3? target = null;
        if(targetTest != null)
            target = targetTest.position;

        if(target != null)
            CreatePath(target);
        else
            Debug.LogError("No target to make a path from");
    }

    // find a starting and target node from world nodes
    Node FindNodeFromGrid(Vector3Int nodePos) {
        if(nodes.ContainsKey(nodePos)) {
            return nodes[nodePos];
        } else {
            return FindClosestNodeFromGrid(nodePos);
        }
    }

    Node FindClosestNodeFromGrid(Vector3Int nodePos) {
        Vector3Int nextCheckPos = nodePos;
        for (int i = 1; i < 100; i++) { // loop down until we meet the node
            nextCheckPos.y--;
            if(nodes.ContainsKey(nextCheckPos)) { //
                return nodes[nextCheckPos];
            }
        }
        if(OptionsData.debugMode) Debug.LogError(nodePos + "FindClosestNodeFromGrid: position was not found in the Grid, returning null.", this);
        return null;
    }

    [ContextMenu("Find Path")]
    public Queue<Pathway> CreatePath(Vector3? _target) {
        // Target == null, return error
        if(_target == null) {
            if (OptionsData.debugMode == true) Debug.LogError("CreatePath: target == null. Break out" );
            return null;
        }

        // get the target values
        Vector3 target = _target.Value;

        // get the nodes from the world, or error if null
        if(nodes == null) {
            GetCurrentNodes(); // Get current nodes from World.
            if(nodes == null) {
                if(OptionsData.debugMode) Debug.LogError("Tried to find nodes, but they were still null. Pathfinder returns null path");
                return null;
            }
        }

        // get current position in grid
        Vector3Int currentPos = Vector3Int.FloorToInt(transform.position);
        // find the closest position from the nodes.
        startNode = FindNodeFromGrid(currentPos);

        if(target != null) {
            Vector3Int targetPos = new Vector3Int();
            targetPos = Vector3Int.FloorToInt(target);
            targetNode = FindNodeFromGrid(targetPos);
        }

        // fail check: StartNode or Target = null?
        if(targetNode == null || startNode == null) {
            if(target == null)
                Debug.LogError("Target Node == null");
            if(startNode == null)
                Debug.LogError("Start Node == null");
            return null;
        }

        // switch (algorithm)
        // {
        //     case Algorithm.BreadthFirst:
        //         return BreadthFirst(startNode, targetNode);

        //     case Algorithm.Diksjtra:
        //         return Diksjtra(startNode, targetNode);

        //     case Algorithm.Astar:
        //         Debug.LogError("Astar Not Implemented");
        //         return null;
            
        //     default:
        //         return Diksjtra(startNode, targetNode);
        // }

        //return BreadthFirst(startNode, targetNode);
        //return AStar(startNode, targetNode);
        return Diksjtra(startNode, targetNode);
    }

    // Dikjstra worked good enough. Still no need for A*
    Queue<Pathway> AStar(Node startNode, Node targetNode) {
        return null;
    }

    Queue<Pathway> Diksjtra(Node startNode, Node targetNode) {
        closetSet = new HashSet<Node>(); // The nodes we checked already.
        List<Pathway> openSet = new List<Pathway>(); // save the edges into "tree" and backtrack from there.
        int previousPathCost = 0; // this is the lowest cost that we are comparing at the nodes. Add all the open nodes and compare the costs and choose lowest.
        int steps = 0;
        currentPath.Clear();

        Node next = startNode;
        while(next != null) {
            // target found, make the path using pathway
            if(next == targetNode) {
                if(OptionsData.debugMode) print("Diksjtra: found the target in: " + steps);
                MakePath(targetNode, startNode, openSet);
                break;
            }

            // it was most likely in closet set and there wasnt any new ones.
            if(next == null) {
               if(OptionsData.debugMode) Debug.Log("Next returned null, no target found");
                break;
            }

            // Find all neighbours from the next and add them to the openSet
            foreach(Edge e in next.edges) {
                if(closetSet.Contains(e.to)) 
                    continue;
                
                Pathway path = new Pathway(next, e.to, e, e.cost + previousPathCost);
                openSet.Add(path);
            }

            // what to do with the total cost?.. this needs to be added to the path, 
            // so the algorithm can choose best next one.
            // Close the one we now checked, and get the next node
            closetSet.Add(next);
            openSet.Sort((Pathway a, Pathway b) => a.CompareTo(b));

            Pathway nextPath = GetNextCheapestUnvisited(openSet, closetSet);
            if(nextPath != null) {               
                previousPathCost = nextPath.Cost;
                next = nextPath.Current;
            }
            // failsave, break out.
            if(steps >= maxSteps) {
                if(OptionsData.debugMode) Debug.LogError("Dikjstra didnt find afte " + maxSteps + " steps, breaking out");   
                return null;
            }
            steps++;
        }

        if(currentPath.Count > 0) {
            return currentPath;
        } else {
            if(OptionsData.debugMode) Debug.Log("Dikjstra didnt find path to the target, returning null");
            return null;
        }
    }

    Pathway GetNextCheapestUnvisited(List<Pathway> openSet, HashSet<Node> closetSet) {
        // Sort all paths by the cheapest into first
        for (int i = 0; i < openSet.Count(); i++) {
            if(!closetSet.Contains(openSet[i].Current)) { // openset should be sorted already.
                return openSet[i];
            }
        } 
        return null;
    }


    Queue<Pathway> BreadthFirst(Node start, Node end) {
        // Breadth First Search.. cut pasted here.
        closetSet = new HashSet<Node>();
        Queue<Node> nextNodes = new Queue<Node>();
        int steps = 0;
        List<Pathway> paths = new List<Pathway>();

        // # Go to the first on the QUEUE
        nextNodes.Enqueue(startNode);
        while(nextNodes.Count > 0) {
            if(closetSet.Contains(nextNodes.Peek() ) ) { // check the first at the queue, if that is visited, skip to next one.
                nextNodes.Dequeue();
                continue;
            }

            Node node = nextNodes.Dequeue(); // dequeue the current node,
            closetSet.Add(node); // add it to visited list
            // # Find all connected nodes and add them to QUEUE.
            foreach(Edge e in node.edges) { // check the all edges from connections nodes.
                if(closetSet.Contains(e.to)) // if those nodes are already in the visited => skip
                    continue;
                nextNodes.Enqueue(e.to); // add those to.nodes to queue.
                Pathway path = new Pathway(node, e.to, e ,e.cost);
                paths.Add(path);
            }

            // # Repeat until we found a path to the target position.
            if(node == targetNode) {
                if(OptionsData.debugMode) print("BreathFirst: found the target in: " + steps + " steps");
                MakePath(targetNode, startNode, paths);
                break;
            }

            if(nextNodes.Count == 1) {
                String viesti = "We have arrived to the last node";
                if(node != targetNode) {
                    viesti += " and it is not our target node";
                }
                if(OptionsData.debugMode) Debug.LogWarning(viesti);
            }
            steps++;
        }

        return currentPath;
    }

    public void MakePath(Node endNode,Node startNode, List<Pathway> _paths) {

        Pathway _path = null;
        List<Pathway> listToStart = new List<Pathway>();
        currentPath.Clear();
        // find the first path with parent from the list of paths.
        foreach (Pathway p in _paths) {
            if(p.Current == endNode) {
                _path = p;
                listToStart.Add(p);
            }
        }

        if(_path != null) {
            Node current = _path.Current;
            for (int i = _paths.Count -1; i > -1 ; i--) { // go through all of the paths and if path we find is current one, set it to the parent one and add it to the list.
                if(_paths[i].Current == current) {
                    current = _paths[i].Parent;
                    listToStart.Add(_paths[i]); // this iterates all of the
                }
            }

            listToStart.Reverse();
            int cost = 0;
            foreach(Pathway p in listToStart) {
                //print(p.Current.worldPosition + " and action here is " + p.CurrentAction() );
                currentPath.Enqueue(new Pathway(p.Parent, p.Current, p.connectionEdge, cost += p.Cost));
            }
        }
    }

    void GetCurrentNodes() {
        nodes = World.Instance.GetNodes();
        if(nodes == null) {
            Debug.LogError("Tried to GetCurrentNodes from World singleton but nodes are still null");
        }
    }
    #if UNITY_EDITOR
    private void OnDrawGizmos() {

        if(startNode != null) {
            Gizmos.color = Color.blue;
            float x = startNode.worldPosition.x;
            float y = startNode.worldPosition.y;
            Gizmos.DrawCube(new Vector3(x + .5f, y +.5f, 0), Vector3.one * 0.15f);
        }

        if(targetNode != null) {
            Gizmos.color = Color.red;
            float x = targetNode.worldPosition.x;
            float y = targetNode.worldPosition.y;
            Gizmos.DrawCube(new Vector3(x + 0.5f, y +  0.5f, 0), Vector3.one * 0.15f);
        }

        if(closetSet != null) {
            Gizmos.color = Color.green;
            int number = 0;
            foreach(Node n in closetSet) {
                Gizmos.DrawCube(new Vector3(n.worldPosition.x + 0.5f, n.worldPosition.y + 0.5f, 0f), Vector3.one * 0.10f);
                // null error kun pelaaja / editor ulkopuolella????
                //extGizmo.Draw(new Vector3(n.worldPosition.x + 0.5f,  n.worldPosition.y + 0.8f, 0f), number.ToString() );
                ScatteriaUtility.DrawString(number.ToString(), new Vector3(n.worldPosition.x + 0.5f, n.worldPosition.y + 1f, 0f), Color.red);
                number++;
            }
        }

        if(currentPath != null) {
            Gizmos.color = Color.white;
            foreach(Pathway p in currentPath) {
                ScatteriaUtility.DrawString(p.Cost.ToString() + " " + p.Action(), new Vector3( p.Current.worldPosition.x + 0.5f, p.Current.worldPosition.y + 0.5f), Color.red);
            }
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, targetThresholdDistance);


    }
    #endif
}
