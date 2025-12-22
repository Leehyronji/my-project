using UnityEngine;
using System.Collections.Generic;

public class WaypointWalker : MonoBehaviour
{
    [Header("Waypoint Settings")]
    public List<Transform> waypoints;
    public float moveSpeed = 2f;
    public float arriveThreshold = 0.15f;

    [Header("Ground Stick Settings")]
    public float groundCheckHeight = 2f;
    public float groundCheckDistance = 5f;
    public LayerMask groundLayer;

    [Header("Obstacle Detection")]
    public float detectDistance = 2f;
    public Vector3 rayOffset = new Vector3(0, 4f, 0);
    public LayerMask obstacleLayer;
    public bool showRay = true;

    private Animator animator;
    private bool pauseByRay = false;
    private bool pauseExternal = false;
    private int currentIdx = 0;
    private int direction = 1;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        CheckObstacle();
        
        if (!pauseByRay && !pauseExternal)
        {
            MoveAlongWaypoints();
        }
        
        StickToGround();
    }

    private void CheckObstacle()
    {
        Vector3 origin = transform.position + rayOffset;
        Vector3 direction = transform.forward;
        
        if (showRay)
        {
            Debug.DrawRay(origin, direction * detectDistance, Color.red);
        }

        if (Physics.Raycast(origin, direction, out RaycastHit hit, detectDistance, obstacleLayer))
        {
            pauseByRay = true;
        }
        else
        {
            pauseByRay = false;
        }
    }

    private void MoveAlongWaypoints()
    {
        if (waypoints == null || waypoints.Count == 0)
            return;

        Transform target = waypoints[currentIdx];
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;

        if (dist > 0.001f)
        {
            dir.Normalize();
            transform.position += dir * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        if (dist <= arriveThreshold)
        {
            currentIdx += direction;
            
            if (currentIdx >= waypoints.Count)
            {
                direction = -1;
                currentIdx = waypoints.Count - 2;
            }
            else if (currentIdx < 0)
            {
                direction = 1;
                currentIdx = 1;
            }
        }
    }

    private void StickToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckHeight;
        Ray ray = new Ray(origin, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y;
            transform.position = pos;

            Vector3 euler = transform.eulerAngles;
            euler.x = 0f;
            euler.z = 0f;
            transform.rotation = Quaternion.Euler(euler);
        }
    }

    public void PauseWalking()
    {
        pauseExternal = true;
    }

    public void ResumeWalking()
    {
        pauseExternal = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origin = transform.position + rayOffset;
        Vector3 dir = transform.forward;
        Gizmos.DrawLine(origin, origin + dir * detectDistance);
    }
}
