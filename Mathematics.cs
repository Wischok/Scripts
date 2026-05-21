using UnityEngine;

public static class Mathematics
{
    public static float Determinant(Vector2 v1, Vector2 v2)
    {
        return (v1.x * v2.y) - (v2.x * v1.y);
    }

    public static float DotToAngle(Vector2 v1, Vector2 v2)
    {
        //dot product to get angle
        //cos^-1(v1 * v2 / ||v1|| ||v2||)
        return Mathf.Acos(
            Mathf.Clamp(
                (v1.x * v2.x + v1.y * v2.y) / (v1.magnitude * v2.magnitude),
                -1f,
                1f
            )
        );
    }

    public static float Dot(Vector2 v1, Vector2 v2)
    {
        //traditional dot product of two 2d vectors
        return v1.x * v2.x + v1.y * v2.y;
    }

    public static bool IsAboveEdge(Vector2 point, ref Vector2[] edgePoints)
    {
        //check if the point is above the edge defined by the edge points
        for (int i = 0; i < edgePoints.Length - 1; i++)
        {
            Vector2 edgeStart = edgePoints[i];
            Vector2 edgeEnd = edgePoints[i + 1];

            // normalize to always go left → right so det sign is consistent
            if (edgeStart.x > edgeEnd.x)
            {
                (edgeEnd, edgeStart) = (edgeStart, edgeEnd);
            }

            //check if entity is within the x bounds of the edge
            if (point.x < Mathf.Min(edgeStart.x, edgeEnd.x) 
            || point.x > Mathf.Max(edgeStart.x, edgeEnd.x))
                continue;

            bool result = Determinant(edgeEnd - edgeStart, point - edgeStart) > 0;
            return result;
        }
        
        // fallback: find the endpoint with the closest x and use its y
        float closestDist = float.MaxValue;
        float closestY = edgePoints[0].y;
        foreach (var ep in edgePoints)
        {
            float dist = Mathf.Abs(point.x - ep.x);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestY = ep.y;
            }
        }
        return point.y > closestY;
    }
}
