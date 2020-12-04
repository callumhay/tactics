using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class TerrainGrid : MonoBehaviour {



  // Class containing various static helper methods for traversing TerrainGridNodes
  public static class TerrainNodeTraverser {

    public static void updateGroundedNodes(in TerrainGrid terrainGrid) {
      TerrainNodeTraverser.updateGroundedNodes(terrainGrid, new List<TerrainColumn>());
    }

    public static void updateGroundedNodes(in TerrainGrid terrainGrid, in IEnumerable<TerrainColumn> affectedTCs) {
      var queue = new Queue<TerrainGridNode>();
      if (affectedTCs.Count() > 0) {
        // When TerrainColumns are provided we only focus on traversing the nodes 
        // (and those attached to the nodes) in those columns

        // Get a list of all nodes connected to (and including) affected TerrainColumns
        // Have a seperate set of all the grounded nodes that are connected
        foreach (var terrainCol in affectedTCs) {
          enqueueNodesInTerrainColumn(terrainGrid, terrainCol, ref queue);
        }
        var allAffectedNodes = new HashSet<TerrainGridNode>(queue);
        var allAffectedGroundedNodes = new HashSet<TerrainGridNode>();
        while (queue.Count > 0) {
          var node = queue.Dequeue();
          if (node.isDefinitelyGrounded()) { allAffectedGroundedNodes.Add(node); }
          
          var neighbours = terrainGrid.getNeighboursForNode(node)
            .FindAll(n => n.isTerrain() && !allAffectedNodes.Contains(n));
          foreach (var neighbour in neighbours) {
            allAffectedNodes.Add(neighbour);
            queue.Enqueue(neighbour);
          }
        }

        // If the set of grounded nodes is empty then *every* affected node is ungrounded
        if (allAffectedGroundedNodes.Count == 0) {
          foreach (var node in allAffectedNodes) {
            node.isTraversalGrounded = false;
          }
          return;
        }
        else {
          // ... otherwise, only re-traverse the connected nodes
          queue.Clear();
          foreach (var node in allAffectedNodes) {
            // Fill the queue with all the ground nodes, set the rest to be ungrounded
            if (node.isDefinitelyGrounded()) { queue.Enqueue(node); }
            else { node.isTraversalGrounded = false; }
          }
        }
      }
      else {
        // No TerrainColumns were provided, go through ALL the nodes
        var nodes = terrainGrid.nodes;
        for (var x = 0; x < nodes.GetLength(0); x++) {
          for (var z = 0; z < nodes.GetLength(2); z++) {
            // The only nodes gaurenteed to be grounded are terrain at a y index of 0
            var groundLevelNode = nodes[x,0,z];
            // Fill the queue with all the ground nodes, set the rest to be ungrounded
            if (groundLevelNode.isTerrain()) { queue.Enqueue(groundLevelNode); }
            else { groundLevelNode.isTraversalGrounded = false; }
            // Initialize all non-ground level nodes as untraversed/ungrounded
            for (var y = 1; y < nodes.GetLength(1); y++) {
              nodes[x,y,z].isTraversalGrounded = false;
            }
          }
        }
      }

      var traversedSet = new HashSet<TerrainGridNode>();
      while (queue.Count > 0) {
        var node = queue.Dequeue();

        // If we've already traversed this node or the node is not terrain then
        // we move on - this causes us to only traverse unique nodes making up the terrain
        if (!node.isTerrain() || traversedSet.Contains(node)) { continue; }
        //Debug.Log("Grounded node found.");
        node.isTraversalGrounded = true;
        traversedSet.Add(node);
        var terrainNeighbours = terrainGrid.getNeighboursForNode(node).Where(n => n.isTerrain());
        foreach (var neighbour in terrainNeighbours) { queue.Enqueue(neighbour); }
      }
    }
  
    private static void depthFirstNodeIslandSearch(
      in TerrainGrid terrainGrid, in TerrainGridNode node, 
      ref HashSet<TerrainGridNode> islandNodes, ref HashSet<TerrainGridNode> traversedSet
    ) {
      var terrainNeighbours = terrainGrid.getNeighboursForNode(node).Where(n => n.isTerrain());
      traversedSet.Add(node);
      islandNodes.Add(node);
      foreach (var neighbour in terrainNeighbours) {
        if (!traversedSet.Contains(neighbour)) { 
          TerrainNodeTraverser.depthFirstNodeIslandSearch(terrainGrid, neighbour, ref islandNodes, ref traversedSet);
        }
      }
    }

    // This method MUST be called AFTER updateGroundedNodes in order to work correctly
    // It is assumed that we now know which nodes are grounded and which are not.
    public static List<HashSet<TerrainGridNode>> traverseNodeIslands(in TerrainGrid terrainGrid) {
      var islands = new List<HashSet<TerrainGridNode>>();
      var traversedSet = new HashSet<TerrainGridNode>();

      // Initialize the traversal info for all ungrounded nodes
      var ungroundedTerrainNodes = new HashSet<TerrainGridNode>();
      var nodes = terrainGrid.nodes;
      for (var x = 0; x < nodes.GetLength(0); x++) {
        for (var y = 0; y < nodes.GetLength(1); y++) {
          for (var z = 0; z < nodes.GetLength(2); z++) {
            var node = nodes[x,y,z];
            if (!node.isTraversalGrounded && node.isTerrain()) { ungroundedTerrainNodes.Add(node); }
            else { traversedSet.Add(node); }
          }
        }
      }

      foreach (var node in ungroundedTerrainNodes) {
        if (traversedSet.Contains(node)) { continue; }
        var islandNodes = new HashSet<TerrainGridNode>();
        TerrainNodeTraverser.depthFirstNodeIslandSearch(terrainGrid, node, ref islandNodes, ref traversedSet);
        if (islandNodes.Count > 0 && !islandNodes.First().isTraversalGrounded) { 
          islands.Add(islandNodes);
        }
      }

      return islands;
    }

    private static void enqueueNodesInTerrainColumn(in TerrainGrid terrainGrid, in TerrainColumn terrainCol, ref Queue<TerrainGridNode> tcNodes) {
      var tcIndices = terrainGrid.getIndexRangeForTerrainColumn(terrainCol);
      for (var x = tcIndices.xStartIdx; x <= tcIndices.xEndIdx; x++) {
        for (var y = tcIndices.yStartIdx; y <= tcIndices.yEndIdx; y++) {
          for (var z = tcIndices.zStartIdx; z < tcIndices.zEndIdx; z++) {
            tcNodes.Enqueue(terrainGrid.nodes[x,y,z]);
          }
        }
      }
    }

  }



}