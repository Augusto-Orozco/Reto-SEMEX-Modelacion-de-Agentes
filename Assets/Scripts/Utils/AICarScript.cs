using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AICarScript : MonoBehaviour
{
    private Rigidbody rigidBody;

    public GameManager gameManager;
    public PlayerInfoUI carInfoUI;

    [Header("Player Data")]
    public string playerName = " ";
    public float velocity = 0.0f;
    public Color bodyColor;
    public string iconString = " ";
    public int lapsCompleted = 0;
    public int currentLap;

    [Header("Navigation")]
    public Transform[] path;
    public GameObject pathGroup;
    public int currentPathObj;
    public int remainingNodes;
    public float distFromPath = 20.0f;

    [Header("Path Data (Single PM)")]
    public string currentPathName;
    public PathManager pathManager;
    public int assignedSubPathIndex = 0;
    public bool canSwitchPaths = true;
    public float pathSwitchProbability = 0.05f;
    [HideInInspector] public bool skipStartAutoPositioning = false;

    [Header("Specs")]
    public float maxSteer = 10f;
    public float maxTorque = 500f;
    public float maxSpeed = 150f;
    public float currentSpeed;
    public float topSpeed = 150f;
    public float decelerationSpeed = -55f;
    public Transform centerOfMass;

    [Header("Speed Control")]
    public float cornerLookAhead = 3;
    public float sharpTurnSpeedMultiplier = 0.4f;
    public float mediumTurnSpeedMultiplier = 0.7f;
    public float sharpTurnAngle = 45f;
    public float mediumTurnAngle = 20f;

    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("AI Sensors")]
    public Color sensorColor = Color.white;
    public float sensorLength = 5f;
    public float frontSensorLength = 10f;
    public float frontSensorStartPoint = 2.52f;
    public float frontSensorSideDistance = 1f;
    public float frontSensorAngle = 8f;
    public float sidewaySensorLength = 0.5f;
    public float avoidSpeed = 2f;
    private int detectionFlag = 0;
    public float respawnWait = 1.5f;
    public float respawnCounter = 0.0f;

    // velocidad objetivo suavizada
    private float targetSpeed;
    private float speedSmoothTime = 0.5f;
    private float speedVelocity;
    private bool isBraking = false;
    private float accelerationSmooth = 0f;

    [Header("Nav Mesh")]
    public NavMeshAgent navMeshAgent;
    public bool useNavMesh = true;
    public float navMeshUpdateInterval = 0.1f;
    private float nextNavMeshUpdate;
    private Vector3 navMeshTargetPos;

    public float speed = 5f;
    public bool canMove = true;

    private Coroutine ensureMoveCoroutine = null;

    // === FIN INDIVIDUAL FLAG ===
    private bool notifiedFinish = false;

    private bool stoppedByCar = false;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        targetSpeed = topSpeed;

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
        {
            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;
            navMeshAgent.speed = topSpeed;
            navMeshAgent.acceleration = maxTorque;
            navMeshAgent.angularSpeed = maxSteer * 10;
            navMeshAgent.radius = 1.5f;
            navMeshAgent.height = 2f;
        }
    }

    private void Start()
    {
        // Color de carrocería
        var rend = transform.childCount > 0 ? transform.GetChild(0).GetComponent<Renderer>() : null;
        if (rend != null) rend.material.SetColor("_Color", bodyColor);

        GetPath();

        if (path == null || path.Length < 2)
        {
            Debug.LogError($"[AICarScript] Ruta inválida para '{name}'. Necesitas al menos 2 waypoints.");
            return;
        }

        if (!skipStartAutoPositioning)
        {
            Vector3 p0 = path[0].position;
            Vector3 p1 = path[1].position;
            Vector3 dir = p1 - p0;

            transform.SetPositionAndRotation(p0, Quaternion.LookRotation(dir));

            if (rigidBody != null)
            {
                rigidBody.velocity = Vector3.zero;
                rigidBody.angularVelocity = Vector3.zero;
            }
        }
    }

    private void Update()
    {
        if (useNavMesh)
        {
            UpdateNavMeshNavigation();
        }
        else if (detectionFlag == 0)
        {
            GetSteer();
        }

        CalculateTargetSpeed();
        SideSensor();
        if (!canMove) return;
        Sensors();
        SideSensor();
        Move();
        Respawn();
    }

    private void FixedUpdate()
    {
        if (rigidBody == null) return;
        Vector3 localVelocity = transform.InverseTransformDirection(rigidBody.velocity);
        localVelocity.x *= 0.7f;
        rigidBody.velocity = transform.TransformDirection(localVelocity);
    }

    // === PATHS ===

    void GetPath()
    {
        if (pathManager == null)
            pathManager = FindObjectOfType<PathManager>();

        if (pathManager == null || pathManager.availablePaths == null || pathManager.availablePaths.Count == 0)
        {
            Debug.LogError($"[AICarScript] No hay PathManager o no tiene subpaths para '{name}'.");
            path = null;
            return;
        }

        assignedSubPathIndex = Mathf.Clamp(assignedSubPathIndex, 0, pathManager.availablePaths.Count - 1);
        var sub = pathManager.availablePaths[assignedSubPathIndex];

        currentPathName = sub.pathName;
        pathGroup = sub.pathRoot != null ? sub.pathRoot.gameObject : pathManager.gameObject;
        path = sub.waypoints;
        remainingNodes = path?.Length ?? 0;
        currentPathObj = 0;
    }

    public void SwitchToPath(PathManager pm, int subPathIndex = 0)
    {
        pathManager = pm;

        if (pathManager == null)
        {
            Debug.LogError($"[{name}] SwitchToPath: PathManager nulo.");
            return;
        }

        if (pathManager.availablePaths == null || pathManager.availablePaths.Count == 0)
        {
            Debug.LogError($"[{name}] PathManager '{pathManager.name}' no tiene subpaths.");
            return;
        }

        assignedSubPathIndex = Mathf.Clamp(subPathIndex, 0, pathManager.availablePaths.Count - 1);

        var sub = pathManager.availablePaths[assignedSubPathIndex];
        currentPathName = sub.pathName;
        pathGroup = sub.pathRoot != null ? sub.pathRoot.gameObject : pathManager.gameObject;
        path = sub.waypoints;

        currentPathObj = 0;
        remainingNodes = (path != null) ? path.Length : 0;

        if (path == null || path.Length == 0)
            Debug.LogError($"[{name}] SubPath '{currentPathName}' no tiene waypoints.");
    }

    // === MOVIMIENTO / NAV ===

    void GetSteer()
    {
        if (path == null || path.Length == 0) return;

        Vector3 steerVector = transform.InverseTransformPoint(new Vector3(
            path[currentPathObj].position.x,
            transform.position.y,
            path[currentPathObj].position.z));

        float newSteer = Mathf.Clamp((steerVector.x / steerVector.magnitude) * maxSteer, -25f, 25f);

        float speedFactor = Mathf.Clamp01(currentSpeed / topSpeed);
        newSteer *= (1f + speedFactor * 0.2f);

        frontLeft.steerAngle = newSteer;
        frontRight.steerAngle = newSteer;

        if (steerVector.magnitude <= distFromPath)
        {
            currentPathObj++;
            remainingNodes--;
            if (currentPathObj >= path.Length)
            {
                currentPathObj = 0;
                remainingNodes = path.Length;
                lapsCompleted++;

                if (!notifiedFinish && gameManager != null && lapsCompleted >= gameManager.lapsToComplete)
                {
                    notifiedFinish = true;
                    gameManager.OnCarFinished(gameObject);
                    // Remove this line: return;
                }
            }
        }
    }

    private void UpdateNavMeshNavigation()
    {
        if (navMeshAgent == null || path == null || path.Length == 0) return;

        // Check if agent is active and on NavMesh
       if (!navMeshAgent.isActiveAndEnabled || !navMeshAgent.isOnNavMesh)
        {
            Debug.LogWarning($"[{name}] NavMeshAgent not on NavMesh or not enabled.");
            return;
        }

        if (Time.time >= nextNavMeshUpdate)
        {
            nextNavMeshUpdate = Time.time + navMeshUpdateInterval;

            navMeshTargetPos = path[currentPathObj].position;
            navMeshAgent.SetDestination(navMeshTargetPos);

            float distanceToWaypoint = Vector3.Distance(transform.position, navMeshTargetPos);
            if (distanceToWaypoint <= distFromPath)
            {
                currentPathObj++;
                remainingNodes--;
                if (currentPathObj >= path.Length)
                {
                    currentPathObj = 0;
                    remainingNodes = path.Length;
                    lapsCompleted++;

                    // === FIN INDIVIDUAL ===
                    if (!notifiedFinish && gameManager != null && lapsCompleted >= gameManager.lapsToComplete)
                    {
                        notifiedFinish = true;
                        gameManager.OnCarFinished(gameObject);
                        return;
                    }
                }
            }
        }

        if (navMeshAgent.hasPath)
        {
            Vector3 nextPathPoint = navMeshAgent.path.corners[Mathf.Min(1, navMeshAgent.path.corners.Length - 1)];
            Vector3 steerVector = transform.InverseTransformPoint(new Vector3(nextPathPoint.x, transform.position.y, nextPathPoint.z));
            float newSteer = Mathf.Clamp((steerVector.x / steerVector.magnitude) * maxSteer, -25f, 25f);

            float speedFactor = Mathf.Clamp01(currentSpeed / topSpeed);
            newSteer *= (1f + speedFactor * 0.2f);

            frontLeft.steerAngle = newSteer;
            frontRight.steerAngle = newSteer;
        }

        navMeshAgent.nextPosition = transform.position;
    }

    void Move()
    {
        if (rigidBody == null) return;

        currentSpeed = rigidBody.velocity.magnitude;

        // Prevent acceleration if car is stopped or braking
        if (!canMove || isBraking)
        {
            rearLeft.motorTorque = 0f;
            rearRight.motorTorque = 0f;
            rearLeft.brakeTorque = Mathf.Abs(decelerationSpeed) * 999f;
            rearRight.brakeTorque = Mathf.Abs(decelerationSpeed) * 999f;
            return;
        }

        maxSpeed = Mathf.SmoothDamp(maxSpeed, targetSpeed, ref speedVelocity, speedSmoothTime);

        if (currentSpeed <= maxSpeed)
        {
            accelerationSmooth = Mathf.Lerp(accelerationSmooth, maxTorque, Time.deltaTime * 2f);
            rearLeft.motorTorque = accelerationSmooth;
            rearRight.motorTorque = accelerationSmooth;

            rearLeft.brakeTorque = 0f;
            rearRight.brakeTorque = 0f;
        }
        else
        {
            rearLeft.motorTorque = 0f;
            rearRight.motorTorque = 0f;
            rearLeft.brakeTorque = 200f;
            rearRight.brakeTorque = 200f;
        }
    }

    void CalculateTargetSpeed()
    {
        if (path == null || path.Length == 0)
        {
            targetSpeed = topSpeed;
            return;
        }

        float minSpeed = topSpeed;

        for (int i = 0; i < cornerLookAhead && i < path.Length; i++)
        {
            int waypointIndex = (currentPathObj + i) % path.Length;
            int nextWaypointIndex = (currentPathObj + i + 1) % path.Length;

            Vector3 currentWaypoint = path[waypointIndex].position;
            Vector3 nextWaypoint = path[nextWaypointIndex].position;

            Vector3 currentDirection = currentWaypoint - transform.position;
            Vector3 nextDirection = nextWaypoint - currentWaypoint;

            float angle = Mathf.Abs(Vector3.SignedAngle(currentDirection, nextDirection, Vector3.up));

            float speedMultiplier = 1f;

            if (angle > sharpTurnAngle)
                speedMultiplier = sharpTurnSpeedMultiplier;
            else if (angle > mediumTurnAngle)
                speedMultiplier = mediumTurnSpeedMultiplier;

            float distanceToTurn = Vector3.Distance(transform.position, currentWaypoint);
            float distanceFactor = Mathf.Clamp01(30f / (distanceToTurn + 5f));

            float adjustedSpeed = topSpeed * (speedMultiplier + (1f - speedMultiplier) * (1f - distanceFactor));
            minSpeed = Mathf.Min(minSpeed, adjustedSpeed);
        }

        targetSpeed = minSpeed;
    }

    // === SENSORES / EVASIÓN ===

    void SideSensor()
    {
        RaycastHit hit;
        detectionFlag = 0;
        float avoidSensitivity = 0f;
        Vector3 sensorOrigin = transform.position + Vector3.up * 1f;

        if (Physics.Raycast(sensorOrigin, transform.right, out hit, sidewaySensorLength))
        {
            if (hit.transform.tag != "Untagged")
            {
                detectionFlag++;
                avoidSensitivity -= avoidSpeed;
                Debug.DrawLine(sensorOrigin, hit.point, sensorColor);
            }
        }

        if (Physics.Raycast(sensorOrigin, -transform.right, out hit, sidewaySensorLength))
        {
            if (hit.transform.tag != "Untagged")
            {
                detectionFlag++;
                avoidSensitivity += avoidSpeed;
                Debug.DrawLine(sensorOrigin, hit.point, sensorColor);
            }
        }

        if (detectionFlag != 0) AvoidSteer(avoidSensitivity);
    }
    void Sensors()
    {
        detectionFlag = 0;
        float avoidSensitivity = 0f;
        Vector3 basePos = transform.position + Vector3.up * 1f;
        RaycastHit hit;

        // Calculate angled directions for side sensors
        Vector3 rightAngle = Quaternion.AngleAxis(frontSensorAngle, transform.up) * transform.forward;
        Vector3 leftAngle = Quaternion.AngleAxis(-frontSensorAngle, transform.up) * transform.forward;

        // === FRONT CENTER SENSOR - Car Detection & Stopping Logic ===
        Vector3 frontCenterPos = basePos + transform.forward * frontSensorStartPoint;
        bool carDetectedNow = false;

        if (Physics.Raycast(frontCenterPos, transform.forward, out hit, frontSensorLength))
        {
            Debug.DrawLine(frontCenterPos, hit.point, sensorColor);
            Debug.Log($"[{name}] Front sensor hit: {hit.collider.name} (Tag: {hit.collider.tag})");

            // Solo detenerse si el tag es "CarBack"
            if (hit.collider.CompareTag("CarBack"))
            {
                carDetectedNow = true;

                // Stop for the car if not already stopped
                if (!stoppedByCar)
                {
                    Debug.Log($"[{name}] Stopping for car: {hit.collider.name}");
                    isBraking = true;
                    StopAtLight();
                    stoppedByCar = true;
                }
            }
        }

        // Resume movement if we were stopped by a car but no car is detected now
        if (stoppedByCar && !carDetectedNow)
        {
            Debug.Log($"[{name}] No car detected, resuming movement");
            ResumeMovement();
            stoppedByCar = false;
        }

        // === RIGHT SIDE SENSORS - Obstacle Avoidance ===
        Vector3 rightSensorPos = basePos + transform.forward * frontSensorStartPoint + transform.right * frontSensorSideDistance;

        // Right forward sensor
        if (Physics.Raycast(rightSensorPos, transform.forward, out hit, sensorLength))
        {
            if (!hit.transform.CompareTag("Untagged") && !hit.collider.CompareTag("Car"))
            {
                detectionFlag++;
                avoidSensitivity -= avoidSpeed;
                Debug.DrawLine(rightSensorPos, hit.point, sensorColor);
            }
        }
        // Right angled sensor (if forward didn't hit)
        else if (Physics.Raycast(rightSensorPos, rightAngle, out hit, sensorLength))
        {
            if (!hit.transform.CompareTag("Untagged") && !hit.collider.CompareTag("Car"))
            {
                detectionFlag++;
                avoidSensitivity -= 0.5f;
                Debug.DrawLine(rightSensorPos, hit.point, sensorColor);
            }
        }

        // === LEFT SIDE SENSORS - Obstacle Avoidance ===
        Vector3 leftSensorPos = basePos + transform.forward * frontSensorStartPoint - transform.right * frontSensorSideDistance;

        // Left forward sensor
        if (Physics.Raycast(leftSensorPos, transform.forward, out hit, sensorLength))
        {
            if (!hit.transform.CompareTag("Untagged") && !hit.collider.CompareTag("Car"))
            {
                detectionFlag++;
                avoidSensitivity += avoidSpeed;
                Debug.DrawLine(leftSensorPos, hit.point, sensorColor);
            }
        }
        // Left angled sensor (if forward didn't hit)
        else if (Physics.Raycast(leftSensorPos, leftAngle, out hit, sensorLength))
        {
            if (!hit.transform.CompareTag("Untagged") && !hit.collider.CompareTag("Car"))
            {
                detectionFlag++;
                avoidSensitivity += 0.5f;
                Debug.DrawLine(leftSensorPos, hit.point, sensorColor);
            }
        }

        // === FALLBACK CENTER SENSOR - Final Avoidance Check ===
        // Only check if no avoidance sensitivity has been set yet
        if (avoidSensitivity == 0)
        {
            if (Physics.Raycast(frontCenterPos, transform.forward, out hit, sensorLength))
            {
                if (!hit.transform.CompareTag("Untagged") && !hit.collider.CompareTag("Car"))
                {
                    // Determine avoidance direction based on hit normal
                    avoidSensitivity += (hit.normal.x < 0) ? 1f : -1f;
                    Debug.DrawLine(frontCenterPos, hit.point, sensorColor);
                }
            }
        }

        // === APPLY AVOIDANCE STEERING ===
        if (detectionFlag > 0)
        {
            AvoidSteer(avoidSensitivity);
        }
    }
    void AvoidSteer(float sensitivity)
    {
        frontLeft.steerAngle = sensitivity;
        frontRight.steerAngle = sensitivity;
    }

    // === CONTROL PARADA / RESPAWN ===

    public void StopAtLight()
    {
        canMove = false;
        isBraking = true;

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
            navMeshAgent.isStopped = true;

        rearLeft.motorTorque = decelerationSpeed;
        rearRight.motorTorque = decelerationSpeed;
        float hardBrake = Mathf.Abs(decelerationSpeed) * 999f;
        rearLeft.brakeTorque = hardBrake;
        rearRight.brakeTorque = hardBrake;

        respawnCounter = 0f;
        Debug.Log($"[AICar] {name} StopAtLight() aplicado.");
    }

    void Respawn()
    {
        if (path == null || path.Length == 0) { respawnCounter = 0; return; }
        int safePathObj = Mathf.Clamp(currentPathObj, 0, path.Length - 1);

        if (rigidBody != null && rigidBody.velocity.magnitude < 2)
        {
            respawnCounter += Time.deltaTime;
            if (respawnCounter >= respawnWait)
            {
                Vector3 respawnPosition;
                Quaternion respawnRotation;

                if (safePathObj == 0)
                {
                    respawnPosition = path[path.Length - 1].position;
                    Vector3 direction = path[safePathObj].position - respawnPosition;
                    respawnRotation = Quaternion.LookRotation(direction);
                }
                else
                {
                    respawnPosition = path[safePathObj - 1].position;
                    Vector3 direction = path[safePathObj].position - respawnPosition;
                    respawnRotation = Quaternion.LookRotation(direction);
                }

                transform.SetPositionAndRotation(respawnPosition, respawnRotation);

                if (rigidBody != null)
                {
                    rigidBody.velocity = Vector3.zero;
                    rigidBody.angularVelocity = Vector3.zero;
                }

                respawnCounter = 0;
            }
        }
        else
        {
            respawnCounter = 0;
        }
    }

    public void ResumeMovement()
    {
        canMove = true;
        isBraking = false;

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
            navMeshAgent.isStopped = false;

        rearLeft.brakeTorque = 0f;
        rearRight.brakeTorque = 0f;

        rearLeft.motorTorque = maxTorque * 0.6f;
        rearRight.motorTorque = maxTorque * 0.6f;

        if (rigidBody != null)
            rigidBody.AddForce(transform.forward * 50f, ForceMode.Acceleration);

        if (ensureMoveCoroutine != null) StopCoroutine(ensureMoveCoroutine);
        ensureMoveCoroutine = StartCoroutine(EnsureMoving());
    }

    private IEnumerator EnsureMoving()
    {
        yield return new WaitForSeconds(0.05f);
        if (rigidBody != null && rigidBody.velocity.magnitude < 0.5f)
        {
            rigidBody.AddForce(transform.forward * 250f, ForceMode.Acceleration);
            rearLeft.motorTorque = maxTorque * 0.9f;
            rearRight.motorTorque = maxTorque * 0.9f;
        }
        yield return new WaitForSeconds(0.2f);
        ensureMoveCoroutine = null;
    }

    // === DEBUG ===

    void OnDrawGizmos()
    {
        if (path != null && path.Length > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < cornerLookAhead && i < path.Length; i++)
            {
                int waypointIndex = (currentPathObj + i) % path.Length;
                Gizmos.DrawWireSphere(path[waypointIndex].position, 2f);
            }
        }

        Gizmos.color = sensorColor;

        Vector3 pos = transform.position + transform.forward * frontSensorStartPoint;

        Gizmos.DrawLine(pos, pos + transform.forward * frontSensorLength);

        Vector3 rightPos = pos + transform.right * frontSensorSideDistance;
        Gizmos.DrawLine(rightPos, rightPos + transform.forward * sensorLength);

        Vector3 rightAngleDir = Quaternion.AngleAxis(frontSensorAngle, transform.up) * transform.forward;
        Gizmos.DrawLine(rightPos, rightPos + rightAngleDir * sensorLength);

        Vector3 leftPos = pos - transform.right * frontSensorSideDistance;
        Gizmos.DrawLine(leftPos, leftPos + transform.forward * sensorLength);

        Vector3 leftAngleDir = Quaternion.AngleAxis(-frontSensorAngle, transform.up) * transform.forward;
        Gizmos.DrawLine(leftPos, leftPos + leftAngleDir * sensorLength);

        Gizmos.DrawLine(transform.position, transform.position + transform.right * sidewaySensorLength);
        Gizmos.DrawLine(transform.position, transform.position - transform.right * sidewaySensorLength);

        Gizmos.DrawLine(pos, pos + transform.forward * sensorLength);
    }
}