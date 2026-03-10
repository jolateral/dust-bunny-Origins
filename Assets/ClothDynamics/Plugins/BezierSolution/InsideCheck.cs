using UnityEngine;

namespace ClothDynamics
{
    // A C# program to check if a given point 
    // lies inside a given polygon 
    // Refer https://www.geeksforgeeks.org/check-if-two-given-line-segments-intersect/ 
    // for explanation of functions onSegment(), 
    // orientation() and doIntersect() 

    public static class InsideCheck
    {

        // Define Infinite (Using INT_MAX 
        // caused overflow problems) 
        static int INF = 10000;

        struct Point
        {
            public float x;
            public float y;

            public Point(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        };

        // Given three colinear points p, q, r, 
        // the function checks if point q lies 
        // on line segment 'pr' 
        static bool onSegment(Point p, Point q, Point r)
        {
            if (q.x <= Mathf.Max(p.x, r.x) &&
                q.x >= Mathf.Min(p.x, r.x) &&
                q.y <= Mathf.Max(p.y, r.y) &&
                q.y >= Mathf.Min(p.y, r.y))
            {
                return true;
            }
            return false;
        }

        // To find orientation of ordered triplet (p, q, r). 
        // The function returns following values 
        // 0 --> p, q and r are colinear 
        // 1 --> Clockwise 
        // 2 --> Counterclockwise 
        static float orientation(Point p, Point q, Point r)
        {
            float val = (q.y - p.y) * (r.x - q.x) -
                    (q.x - p.x) * (r.y - q.y);

            if (val == 0)
            {
                return 0; // colinear 
            }
            return (val > 0) ? 1 : 2; // clock or counterclock wise 
        }

        // The function that returns true if 
        // line segment 'p1q1' and 'p2q2' intersect. 
        static bool doIntersect(Point p1, Point q1,
                                Point p2, Point q2)
        {
            // Find the four orientations needed for 
            // general and special cases 
            float o1 = orientation(p1, q1, p2);
            float o2 = orientation(p1, q1, q2);
            float o3 = orientation(p2, q2, p1);
            float o4 = orientation(p2, q2, q1);

            // General case 
            if (o1 != o2 && o3 != o4)
            {
                return true;
            }

            // Special Cases 
            // p1, q1 and p2 are colinear and 
            // p2 lies on segment p1q1 
            if (o1 == 0 && onSegment(p1, p2, q1))
            {
                return true;
            }

            // p1, q1 and p2 are colinear and 
            // q2 lies on segment p1q1 
            if (o2 == 0 && onSegment(p1, q2, q1))
            {
                return true;
            }

            // p2, q2 and p1 are colinear and 
            // p1 lies on segment p2q2 
            if (o3 == 0 && onSegment(p2, p1, q2))
            {
                return true;
            }

            // p2, q2 and q1 are colinear and 
            // q1 lies on segment p2q2 
            if (o4 == 0 && onSegment(p2, q1, q2))
            {
                return true;
            }

            // Doesn't fall in any of the above cases 
            return false;
        }

        // Returns true if the point p lies 
        // inside the polygon[] with n vertices 
        static bool isInside(Point[] polygon, int n, Point p)
        {
            // There must be at least 3 vertices in polygon[] 
            if (n < 3)
            {
                return false;
            }

            // Create a point for line segment from p to infinite 
            Point extreme = new Point(INF, p.y);

            // Count intersections of the above line 
            // with sides of polygon 
            int count = 0, i = 0;
            do
            {
                int next = (i + 1) % n;

                // Check if the line segment from 'p' to 
                // 'extreme' intersects with the line 
                // segment from 'polygon[i]' to 'polygon[next]' 
                if (doIntersect(polygon[i],
                                polygon[next], p, extreme))
                {
                    // If the point 'p' is colinear with line 
                    // segment 'i-next', then check if it lies 
                    // on segment. If it lies, return true, otherwise false 
                    if (orientation(polygon[i], p, polygon[next]) == 0)
                    {
                        return onSegment(polygon[i], p,
                                        polygon[next]);
                    }
                    count++;
                }
                i = next;
            } while (i != 0);

            // Return true if count is odd, false otherwise 
            return (count % 2 == 1); // Same as (count%2 == 1) 
        }

        // Driver Code 
        public static bool Run(Vector3 point, BezierPoint[] bezierPolygon, int axisX = 0, int axisY = 1)
        {
            int n = bezierPolygon.Length;
            Point p = new Point(point[axisX], point[axisY]);
            Point[] polygon = new Point[n];
            for (int i = 0; i < n; i++)
            {
                polygon[i] = new Point(bezierPolygon[i].position[axisX], bezierPolygon[i].position[axisY]);
            }
            if (isInside(polygon, n, p))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool Run(Vector3 point, Vector2[] bezierPolygon, int axisX = 0, int axisY = 1)
        {
            int n = bezierPolygon.Length;
            Point p = new Point(point[axisX], point[axisY]);
            Point[] polygon = new Point[n];
            for (int i = 0; i < n; i++)
            {
                polygon[i] = new Point(bezierPolygon[i].x, bezierPolygon[i].y);
            }
            if (isInside(polygon, n, p))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    // This code is contributed by 29AjayKumar 
}