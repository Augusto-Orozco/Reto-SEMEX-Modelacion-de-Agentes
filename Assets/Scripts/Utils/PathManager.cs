using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages multiple sub-paths for AI navigation. Each sub-path contains waypoints that define
/// a route for AI cars to follow. This system allows for complex traffic patterns with
/// multiple lanes, intersections, and route variations.
/// </summary>
public class PathManager : MonoBehaviour
{
    [Header("Available Paths Configuration")]
    [Tooltip("List of all available sub-paths. Each sub-path represents a different route with its own waypoints.")]
    public List<SubPath> availablePaths = new List<SubPath>();

    [Header("Gizmo Visualization Settings")]
    [Tooltip("Show waypoint connections in the Scene view")]
    public bool showPathLines = true;

    [Tooltip("Show waypoint spheres in the Scene view")]
    public bool showWaypoints = true;

    [Tooltip("Size of the waypoint spheres in the Scene view")]
    [Range(0.1f, 2f)]
    public float waypointSize = 0.5f;

    [Tooltip("Base color for path visualization. Each path gets a variation of this color.")]
    public Color basePathColor = Color.cyan;

    #region Unity Lifecycle

    /// <summary>
    /// Initialize the path system when the component awakens
    /// </summary>
    void Awake()
    {
        ValidatePaths();
    }

    /// <summary>
    /// Validate all paths when the component starts
    /// </summary>
    void Start()
    {
        if (availablePaths.Count == 0)
        {
            Debug.LogWarning($"[PathManager] '{name}' has no sub-paths configured. Add at least one sub-path with waypoints.");
        }
    }

    #endregion

    #region Path Management

    /// <summary>
    /// Validates all configured paths and removes invalid ones
    /// </summary>
    void ValidatePaths()
    {
        for (int i = availablePaths.Count - 1; i >= 0; i--)
        {
            var subPath = availablePaths[i];

            // Remove paths with no waypoints
            if (subPath.waypoints == null || subPath.waypoints.Length == 0)
            {
                Debug.LogWarning($"[PathManager] Removing empty sub-path '{subPath.pathName}' (no waypoints).");
                availablePaths.RemoveAt(i);
                continue;
            }

            // Check for null waypoints
            bool hasNullWaypoints = false;
            for (int j = 0; j < subPath.waypoints.Length; j++)
            {
                if (subPath.waypoints[j] == null)
                {
                    Debug.LogWarning($"[PathManager] Sub-path '{subPath.pathName}' has null waypoint at index {j}.");
                    hasNullWaypoints = true;
                }
            }

            if (hasNullWaypoints)
            {
                Debug.LogWarning($"[PathManager] Sub-path '{subPath.pathName}' contains null waypoints. Please fix in inspector.");
            }
        }

        Debug.Log($"[PathManager] '{name}' initialized with {availablePaths.Count} valid sub-paths.");
    }

    /// <summary>
    /// Gets a sub-path by its index
    /// </summary>
    /// <param name="index">Index of the sub-path to retrieve</param>
    /// <returns>SubPath object or null if index is invalid</returns>
    public SubPath GetSubPath(int index)
    {
        if (index < 0 || index >= availablePaths.Count)
        {
            Debug.LogError($"[PathManager] Sub-path index {index} is out of range (0-{availablePaths.Count - 1}).");
            return null;
        }

        return availablePaths[index];
    }

    /// <summary>
    /// Gets a sub-path by its name
    /// </summary>
    /// <param name="pathName">Name of the sub-path to find</param>
    /// <returns>SubPath object or null if not found</returns>
    public SubPath GetSubPathByName(string pathName)
    {
        foreach (var subPath in availablePaths)
        {
            if (subPath.pathName == pathName)
                return subPath;
        }

        Debug.LogWarning($"[PathManager] Sub-path '{pathName}' not found.");
        return null;
    }

    /// <summary>
    /// Gets the total number of available sub-paths
    /// </summary>
    /// <returns>Number of available sub-paths</returns>
    public int GetPathCount()
    {
        return availablePaths.Count;
    }

    #endregion

    #region Gizmo Visualization

    /// <summary>
    /// Draws path visualization in the Scene view
    /// </summary>
    void OnDrawGizmos()
    {
        if (availablePaths == null || availablePaths.Count == 0)
            return;

        // Draw each sub-path with a consistent color
        for (int pathIndex = 0; pathIndex < availablePaths.Count; pathIndex++)
        {
            var subPath = availablePaths[pathIndex];

            if (subPath.waypoints == null || subPath.waypoints.Length == 0)
                continue;

            // Generate a consistent color for each path based on its index
            Color pathColor = GetPathColor(pathIndex);
            Gizmos.color = pathColor;

            // Draw waypoints as spheres
            if (showWaypoints)
            {
                foreach (var waypoint in subPath.waypoints)
                {
                    if (waypoint != null)
                    {
                        Gizmos.DrawWireSphere(waypoint.position, waypointSize);
                    }
                }
            }

            // Draw connections between consecutive waypoints (no loop back to start)
            if (showPathLines && subPath.waypoints.Length > 1)
            {
                for (int i = 0; i < subPath.waypoints.Length - 1; i++)
                {
                    var current = subPath.waypoints[i];
                    var next = subPath.waypoints[i + 1];

                    if (current != null && next != null)
                    {
                        Gizmos.DrawLine(current.position, next.position);

                        // Draw arrow to show direction
                        DrawArrow(current.position, next.position, pathColor);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates a consistent color for a path based on its index
    /// </summary>
    /// <param name="pathIndex">Index of the path</param>
    /// <returns>Color for the path</returns>
    Color GetPathColor(int pathIndex)
    {
        // Use HSV color space to generate distinct colors
        float hue = (basePathColor.r + (pathIndex * 0.3f)) % 1f;
        float saturation = Mathf.Clamp(basePathColor.g, 0.5f, 1f);
        float value = Mathf.Clamp(basePathColor.b, 0.7f, 1f);

        return Color.HSVToRGB(hue, saturation, value);
    }

    /// <summary>
    /// Draws a directional arrow between two points
    /// </summary>
    /// <param name="from">Starting position</param>
    /// <param name="to">Ending position</param>
    /// <param name="color">Arrow color</param>
    void DrawArrow(Vector3 from, Vector3 to, Color color)
    {
        Vector3 direction = (to - from).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 up = Vector3.Cross(direction, right);

        float arrowSize = waypointSize * 0.5f;
        Vector3 arrowPos = Vector3.Lerp(from, to, 0.7f); // Position arrow 70% along the line

        // Draw arrow head
        Gizmos.color = color;
        Gizmos.DrawRay(arrowPos, -direction * arrowSize + right * arrowSize * 0.5f);
        Gizmos.DrawRay(arrowPos, -direction * arrowSize - right * arrowSize * 0.5f);
    }

    #endregion

    #region Editor Utilities

    /// <summary>
    /// Adds a new empty sub-path to the available paths list
    /// Called from custom editor or inspector
    /// </summary>
    [ContextMenu("Add New Sub-Path")]
    public void AddNewSubPath()
    {
        var newSubPath = new SubPath
        {
            pathName = $"Path_{availablePaths.Count + 1}",
            waypoints = new Transform[0]
        };

        availablePaths.Add(newSubPath);
        Debug.Log($"[PathManager] Added new sub-path: {newSubPath.pathName}");
    }

    /// <summary>
    /// Removes empty or invalid sub-paths
    /// </summary>
    [ContextMenu("Clean Invalid Paths")]
    public void CleanInvalidPaths()
    {
        int originalCount = availablePaths.Count;
        ValidatePaths();
        int removedCount = originalCount - availablePaths.Count;

        if (removedCount > 0)
            Debug.Log($"[PathManager] Cleaned {removedCount} invalid sub-paths.");
        else
            Debug.Log("[PathManager] No invalid paths found.");
    }

    #endregion
}

/// <summary>
/// Represents a single sub-path containing waypoints and metadata
/// </summary>
[System.Serializable]
public class SubPath
{
    [Header("Path Identification")]
    [Tooltip("Unique name for this sub-path. Used for debugging and path switching.")]
    public string pathName = "Path_1";

    [Header("Path Structure")]
    [Tooltip("Root GameObject that contains all waypoints for this path. Optional - used for organization.")]
    public Transform pathRoot;

    [Tooltip("Array of Transform waypoints that define the path route. Cars will follow these points in order.")]
    public Transform[] waypoints;

    [Header("Path Properties")]
    [Tooltip("Maximum recommended speed for this path. AI cars may use this for speed adjustments.")]
    [Range(10f, 200f)]
    public float recommendedSpeed = 100f;

    [Tooltip("Priority level of this path. Higher values = higher priority for path selection.")]
    [Range(1, 10)]
    public int pathPriority = 5;

    [Tooltip("Tags for categorizing paths (e.g., 'highway', 'city', 'intersection')")]
    public string[] pathTags = new string[0];

    /// <summary>
    /// Gets the number of waypoints in this sub-path
    /// </summary>
    public int WaypointCount => waypoints?.Length ?? 0;

    /// <summary>
    /// Checks if this sub-path is valid (has waypoints)
    /// </summary>
    public bool IsValid => waypoints != null && waypoints.Length > 0;

    /// <summary>
    /// Gets a waypoint by index with bounds checking
    /// </summary>
    /// <param name="index">Index of the waypoint</param>
    /// <returns>Transform of the waypoint or null if invalid</returns>
    public Transform GetWaypoint(int index)
    {
        if (waypoints == null || index < 0 || index >= waypoints.Length)
            return null;

        return waypoints[index];
    }

    /// <summary>
    /// Calculates the approximate total length of this path
    /// </summary>
    /// <returns>Total distance in world units</returns>
    public float GetTotalPathLength()
    {
        if (waypoints == null || waypoints.Length < 2)
            return 0f;

        float totalLength = 0f;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
            {
                totalLength += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
            }
        }

        return totalLength;
    }
}