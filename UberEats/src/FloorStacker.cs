using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SMTUberEats
{
    // Stacks boxes in a neat grid around the delivery point, nearest cells first
    class FloorStacker
    {
        const int GridRadius = 3;          // 7x7 cells
        const float Gap = 0.15f;           // space between neighbouring stacks
        const float GroundTolerance = 0.25f;
        const float DropHeight = 0.02f;
        // Box size from the game's delivery code (half extents 0.3, 0.3, 0.45), used if the prefab has no BoxCollider
        static readonly Vector3 FallbackBoxSize = new Vector3(0.6f, 0.6f, 0.9f);

        readonly ManagerBlackboard board;
        readonly int stackHeight;
        readonly Vector3 center;
        readonly Vector3 boxSize;
        readonly float groundY;
        readonly List<Vector3> cells = new List<Vector3>();
        int cell;

        public FloorStacker(ManagerBlackboard board, int stackHeight)
        {
            this.board = board;
            this.stackHeight = Math.Max(1, stackHeight);
            center = board.merchandiseSpawnpoint.transform.position;
            boxSize = MeasureBox(board.boxPrefab);
            groundY = FindGround(center);

            float stepX = boxSize.x + Gap, stepZ = boxSize.z + Gap;
            for (int x = -GridRadius; x <= GridRadius; x++)
                for (int z = -GridRadius; z <= GridRadius; z++)
                    cells.Add(new Vector3(center.x + x * stepX, groundY, center.z + z * stepZ));
            cells.Sort((a, b) => Flat(a - center).CompareTo(Flat(b - center)));
        }

        public void Spawn(int productID, int count)
        {
            // Cells only fill up, so earlier ones that were full or blocked stay that way for this delivery
            for (; cell < cells.Count; cell++)
            {
                if (TryStackHeight(cells[cell], out float top))
                {
                    var position = new Vector3(cells[cell].x, top + boxSize.y / 2f + DropHeight, cells[cell].z);
                    board.SpawnBoxFromEmployee(position, productID, count);
                    // Make the new box's collider visible to the next probe straight away
                    Physics.SyncTransforms();
                    return;
                }
            }
            // Every cell is full: pile the rest up like the unmodded game
            var offset = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
            board.SpawnBoxFromEmployee(center + offset, productID, count);
        }

        // Finds the top of the stack in a cell. False when the cell is full, blocked, or not reachable from the delivery point.
        bool TryStackHeight(Vector3 cellBase, out float top)
        {
            top = groundY;
            float probeTop = groundY + (stackHeight + 1) * boxSize.y + 0.5f;
            var origin = new Vector3(cellBase.x, probeTop, cellBase.z);
            var halfExtents = new Vector3(boxSize.x, 0.01f, boxSize.z) * 0.45f;
            var hits = Physics.BoxCastAll(origin, halfExtents, Vector3.down, Quaternion.identity,
                probeTop - groundY + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int boxes = 0;
            bool first = true;
            foreach (var hit in hits)
            {
                if (hit.distance <= 0f) return false;  // something (a ceiling?) is already inside the probe
                if (hit.collider.GetComponentInParent<BoxData>() != null)
                {
                    if (first) top = hit.point.y;
                    first = false;
                    boxes++;
                    continue;
                }
                // The first thing under the boxes must be the floor, not a player, shelf or wall
                if (Mathf.Abs(hit.point.y - groundY) > GroundTolerance) return false;
                if (boxes >= stackHeight) return false;
                return IsReachable(cellBase);
            }
            return false;  // no floor found
        }

        // Stops the grid from reaching through a wall or fence next to the delivery point
        bool IsReachable(Vector3 cellBase)
        {
            float y = groundY + boxSize.y / 2f;
            var from = new Vector3(center.x, y, center.z);
            var to = new Vector3(cellBase.x, y, cellBase.z);
            var direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return true;
            foreach (var hit in Physics.RaycastAll(from, direction.normalized, direction.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsMovable(hit.collider)) return false;
            }
            return true;
        }

        static float FindGround(Vector3 from)
        {
            var hits = Physics.RaycastAll(from + Vector3.up * 0.5f, Vector3.down, 20f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
                if (!IsMovable(hit.collider)) return hit.point.y;
            return from.y;
        }

        // Boxes, players and NPCs come and go, so they never count as walls or floor
        static bool IsMovable(Collider collider) =>
            collider.GetComponentInParent<BoxData>() != null ||
            collider.GetComponentInParent<PlayerNetwork>() != null ||
            collider.GetComponentInParent<NavMeshAgent>() != null ||
            collider.GetComponentInParent<CharacterController>() != null;

        static Vector3 MeasureBox(GameObject prefab)
        {
            var collider = prefab != null ? prefab.GetComponentInChildren<BoxCollider>() : null;
            if (collider == null) return FallbackBoxSize;
            var size = Vector3.Scale(collider.size, collider.transform.lossyScale);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            return size.x > 0.05f && size.y > 0.05f && size.z > 0.05f ? size : FallbackBoxSize;
        }

        static float Flat(Vector3 v) => v.x * v.x + v.z * v.z;
    }
}
