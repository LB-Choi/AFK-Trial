using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        private const float BodySpacing = .48f;
        private const int DetourSides = 24;
        private static readonly Vector2[] detourDirections = MakeDetourDirections();
        private Vector2[] pathPoints;
        private float[] pathCosts;
        private int[] firstHops;
        private bool[] visited;

        private static Vector2[] MakeDetourDirections()
        {
            var directions = new Vector2[DetourSides];
            for (int i = 0; i < directions.Length; i++)
            {
                float angle = 2f * Mathf.PI * i / directions.Length;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return directions;
        }

        private void SelectTarget(Unit unit)
        {
            var taunter = unit.Skills.Taunter;
            if (taunter != null && (!taunter.Alive || unit.Skills.TauntUntil > 0f && Elapsed >= unit.Skills.TauntUntil))
            {
                if (unit.Target == taunter) unit.Target = null;
                taunter = unit.Skills.Taunter = null;
                unit.Skills.TauntUntil = 0f;
            }
            if (taunter != null && taunter.Alive && unit.Target != taunter)
            {
                unit.Waypoint = null;
                unit.Target = taunter;
            }
            if (unit.Target != null && unit.Target.Alive)
            {
                // Engage the defender blocking the approach instead of trying to pass through it.
                if (taunter != null || unit.WindingUp || unit.Stats.ContactDistance > 0 ||
                    (unit.Target.Position - unit.Position).sqrMagnitude <= unit.Stats.Range * unit.Stats.Range) return;
                float reachSquared = unit.Stats.Range * unit.Stats.Range;
                foreach (var foe in Units)
                {
                    if (!foe.Alive || foe.Ally == unit.Ally) continue;
                    float gap = (foe.Position - unit.Position).sqrMagnitude;
                    if (gap > reachSquared) continue;
                    reachSquared = gap;
                    unit.Target = foe;
                    unit.Waypoint = null;
                }
                return;
            }

            if (unit.WindingUp) unit.SwingStart = float.NegativeInfinity;
            unit.WindingUp = false;
            unit.Waypoint = null;
            unit.Target = null;
            float closest = float.PositiveInfinity;
            foreach (var other in Units)
            {
                if (!other.Alive || other.Ally == unit.Ally) continue;
                if (unit.Attacks == 0 && other.Lane == unit.Lane)
                {
                    unit.Target = other;
                    break;
                }
                float distance = (other.Position - unit.Position).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                unit.Target = other;
            }
        }

        private Vector2 AttackPosition(Unit unit, out bool flanking)
        {
            var target = unit.Target;
            float facing = unit.Ally ? -1f : 1f;
            flanking = false;
            if (unit.Stats.ContactDistance > 0)
                return target.Position + Vector2.right * (facing * unit.Stats.ContactDistance);

            // The fighter facing the target holds the front. Formation order gives the
            // other two distinct flanks, even when they both approach from the same lane.
            Unit front = unit;
            foreach (var other in Units)
            {
                if (!other.Alive || other.Target != target) continue;
                if (other.Stats.ContactDistance > 0) { front = other; break; }
                if (other == target.Target) front = other;
                if (front == target.Target) continue;
                int distance = Mathf.Abs(other.Lane - target.Lane);
                int best = Mathf.Abs(front.Lane - target.Lane);
                if (distance < best || (distance == best && other.Lane < front.Lane)) front = other;
            }
            float reach = unit.Stats.Range * .98f;
            if (front == unit) return target.Position + Vector2.right * (facing * reach);

            flanking = true;
            int side = unit.Lane > front.Lane ? 1 : -1;
            foreach (var other in Units)
                if (other.Alive && other != unit && other != front && other.Target == target)
                    side = unit.Lane > other.Lane ? 1 : -1;
            // Sixty degrees from the front keeps all three within the circular attack range.
            return target.Position + new Vector2(facing * .5f, side * .8660254f) * reach;
        }

        private Vector2 StepAroundUnits(Unit unit, Vector2 destination, float maxStep)
        {
            if (maxStep <= 0f) return unit.Position;
            // The opposing front is a movement boundary even when a different lane is clear.
            float direction = unit.Ally ? 1f : -1f;
            float limit = float.PositiveInfinity;
            foreach (var foe in Units)
                if (foe.Alive && foe.Ally != unit.Ally)
                    limit = Mathf.Min(limit, direction * foe.Position.x - .01f);
            destination.x = direction * Mathf.Min(direction * destination.x, limit);
            bool reachableStrike = (destination - unit.Target.Position).sqrMagnitude <= unit.Stats.Range * unit.Stats.Range;
            if (reachableStrike && ClearPath(unit, unit.Position, destination)) unit.Waypoint = null;
            else
            {
                if (!unit.Waypoint.HasValue || (unit.Waypoint.Value - unit.Position).sqrMagnitude < .001f ||
                    direction * unit.Waypoint.Value.x > limit || !ClearPath(unit, unit.Position, unit.Waypoint.Value))
                    unit.Waypoint = FindDetour(unit, destination, direction, limit);
                if (unit.Waypoint.HasValue) destination = unit.Waypoint.Value;
                else return unit.Position;
            }
            return Vector2.MoveTowards(unit.Position, destination, maxStep);
        }

        // Find a route around the occupied ground discs. A straight chase needs no search.
        // The small clearance around each ring keeps chords between adjacent points walkable.
        private Vector2? FindDetour(Unit unit, Vector2 destination, float facing, float limit)
        {
            int capacity = 2 + (Units.Count + 1) * DetourSides;
            if (pathPoints == null || pathPoints.Length < capacity)
            {
                pathPoints = new Vector2[capacity];
                pathCosts = new float[capacity];
                firstHops = new int[capacity];
                visited = new bool[capacity];
            }
            pathPoints[0] = unit.Position;
            pathPoints[1] = destination;
            int count = 2;
            foreach (var other in Units)
            {
                if (other == unit || !other.Alive) continue;
                float radius = Clearance(unit, other) * 1.06f + .015f;
                foreach (Vector2 direction in detourDirections)
                {
                    Vector2 point = other.Position + direction * radius;
                    if (facing * point.x <= limit) pathPoints[count++] = point;
                }
            }
            if (unit.Stats.ContactDistance <= 0)
            {
                foreach (var offset in detourDirections)
                {
                    Vector2 point = unit.Target.Position + offset * (unit.Stats.Range * .98f);
                    if (facing * point.x <= limit) pathPoints[count++] = point;
                }
            }
            for (int i = 0; i < count; i++)
            {
                pathCosts[i] = float.PositiveInfinity;
                visited[i] = false;
            }
            pathCosts[0] = 0;
            int closest = 0;
            while (true)
            {
                int current = -1;
                float lowest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (visited[i] || float.IsInfinity(pathCosts[i])) continue;
                    float estimate = pathCosts[i] + Vector2.Distance(pathPoints[i], destination);
                    if (estimate < lowest) { lowest = estimate; current = i; }
                }
                if (current == 1 && (destination - unit.Target.Position).sqrMagnitude <= unit.Stats.Range * unit.Stats.Range) return pathPoints[firstHops[1]];
                if (current < 0) break;
                visited[current] = true;
                Vector2 point = pathPoints[current];
                // Another open side of the target is also a valid place to strike from.
                if (current > 1 && unit.Stats.ContactDistance <= 0 &&
                    (point - unit.Target.Position).sqrMagnitude <= unit.Stats.Range * unit.Stats.Range)
                    return pathPoints[firstHops[current]];
                if ((point - destination).sqrMagnitude < (pathPoints[closest] - destination).sqrMagnitude)
                    closest = current;
                for (int next = 1; next < count; next++)
                {
                    if (visited[next]) continue;
                    float cost = pathCosts[current] + Vector2.Distance(point, pathPoints[next]);
                    if (cost >= pathCosts[next] || !ClearPath(unit, point, pathPoints[next])) continue;
                    pathCosts[next] = cost;
                    firstHops[next] = current == 0 ? next : firstHops[current];
                }
            }
            // The nominal flank may be occupied by a fighter engaging a different target.
            // Stop at the nearest reachable position and use the regular range check there.
            if (closest == 0) return null;
            return pathPoints[firstHops[closest]];
        }

        private static float Clearance(Unit unit, Unit other)
        {
            if (unit.Ally == other.Ally) return BodySpacing;
            float reach = unit.Stats.ContactDistance > 0 ? unit.Stats.ContactDistance : unit.Stats.Range;
            float otherReach = other.Stats.ContactDistance > 0 ? other.Stats.ContactDistance : other.Stats.Range;
            return Mathf.Min(BodySpacing, Mathf.Min(reach, otherReach) * .95f);
        }

        private bool ClearPath(Unit unit, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared < .0000001f) return true;
            foreach (var other in Units)
            {
                if (other == unit || !other.Alive) continue;
                Vector2 offset = other.Position - start;
                float along = Vector2.Dot(offset, delta) / lengthSquared;
                if (along <= 0) continue;
                float gap = Clearance(unit, other);
                if ((offset - delta * Mathf.Min(1f, along)).sqrMagnitude < gap * gap) return false;
            }
            return true;
        }
    }
}
