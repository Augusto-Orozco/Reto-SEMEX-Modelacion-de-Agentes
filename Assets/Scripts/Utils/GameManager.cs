using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject gameCanvas;
    private List<PlayerData> playersArray;
    public LoadPlayer loader = new LoadPlayer();
    private int playerCreated = 0;

    [Header("Cars Starting Point (opcional)")]
    public GameObject startingPoint;

    [Header("Cars Management")]
    public string filepath = "/Scripts/Utils/data.txt";
    public int numberOfCars = 4;

    [Header("Relevant Info")]
    public int lapsToComplete = 0;
    public float spawnTime = 0.0f;

    [Header("Race Results (opcional)")]
    public List<string> finishOrder = new List<string>();


    // === UN SOLO PATH MANAGER ===
    [Header("Single PathManager")]
    public PathManager pathManager;                 // arrástralo en el Inspector
    private int currentSubPathAssignmentIndex = -1; // para Alternating

    [Header("Assignment Mode (Subpaths)")]
    public PathAssignmentMode pathAssignmentMode = PathAssignmentMode.Sequential;
    public bool allowPathSwitching = true;
    [Range(0f, 1f)]
    public float globalPathSwitchProbability = 0.05f;

    [Header("Leader board (Data)")]
    public List<GameObject> carObjects = new List<GameObject>();
    public List<GameObject> carInfoObjects = new List<GameObject>();

    [Header("Leaderboard UI (Fixed Canvas)")]
    public TextMeshProUGUI[] leaderboardTexts;

    [Header("Reset UI")]
    public GameObject resetCanvas;
    public TextMeshPro winner;

    Coroutine SpawnCoroutine;

    #region Unity Lifecycle

    void Awake()
    {
        CreatePlayersArray();
        gameCanvas = GameObject.Find("GameCanvas");
        InitializePathSystem();
    }

    void Start()
    {
        SpawnCoroutine = StartCoroutine(InstantiatePlayers());
    }

    void Update()
    {
        UpdateLeaderboardUI();
        HandlePathSwitchingInput();
    }

    #endregion

    #region Path System (Single PM)

    void InitializePathSystem()
    {
        if (pathManager == null)
            pathManager = FindObjectOfType<PathManager>();

        if (pathManager == null)
        {
            Debug.LogError("[GameManager] No se encontró un PathManager en la escena.");
            return;
        }

        if (pathManager.availablePaths == null || pathManager.availablePaths.Count == 0)
        {
            Debug.LogError("[GameManager] El PathManager no tiene subpaths. Crea al menos 1 con waypoints.");
            return;
        }

        Debug.Log($"[GameManager] PathManager listo: {pathManager.name} | subpaths: {pathManager.availablePaths.Count}");
    }

    int GetSubPathIndexForCar(int carIndex)
    {
        int count = (pathManager != null && pathManager.availablePaths != null)
            ? pathManager.availablePaths.Count
            : 0;

        if (count <= 1) return 0;

        switch (pathAssignmentMode)
        {
            case PathAssignmentMode.Sequential:
                return carIndex % count;

            case PathAssignmentMode.Random:
                return Random.Range(0, count);

            case PathAssignmentMode.AllSamePath:
                return 0;

            case PathAssignmentMode.Alternating:
                currentSubPathAssignmentIndex = (currentSubPathAssignmentIndex + 1 + count) % count;
                return currentSubPathAssignmentIndex;

            default:
                return 0;
        }
    }

    void HandlePathSwitchingInput()
    {
        if (pathManager == null || pathManager.availablePaths == null) return;
        int count = pathManager.availablePaths.Count;

        // Teclas 1..9 -> subpath 0..8
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                int subIndex = i - 1;
                if (subIndex < count)
                    SwitchAllCarsToPath(subIndex);
                else
                    Debug.LogWarning($"[GameManager] SubPath {subIndex} fuera de rango (0..{count - 1}).");
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RedistributeCarsRandomly();
        }
    }

    public void SwitchAllCarsToPath(int subPathIndex)
    {
        if (pathManager == null || pathManager.availablePaths == null || pathManager.availablePaths.Count == 0)
        {
            Debug.LogWarning("[GameManager] No hay subpaths disponibles.");
            return;
        }

        int count = pathManager.availablePaths.Count;
        if (subPathIndex < 0 || subPathIndex >= count)
        {
            Debug.LogWarning($"[GameManager] SubPath {subPathIndex} fuera de rango (0..{count - 1}).");
            return;
        }

        foreach (GameObject carObject in carObjects)
        {
            var ai = carObject.GetComponent<AICarScript>();
            if (ai != null) ai.SwitchToPath(pathManager, subPathIndex);
        }

        Debug.Log($"[GameManager] Todos los carros cambiados al subpath {subPathIndex}.");
    }

    public void RedistributeCarsRandomly()
    {
        if (pathManager == null || pathManager.availablePaths == null || pathManager.availablePaths.Count == 0) return;
        int count = pathManager.availablePaths.Count;

        foreach (GameObject carObject in carObjects)
        {
            var ai = carObject.GetComponent<AICarScript>();
            if (ai == null) continue;

            int sub = Random.Range(0, count);
            ai.SwitchToPath(pathManager, sub);
        }

        Debug.Log("[GameManager] Carros redistribuidos aleatoriamente entre subpaths.");
    }

    public void SwitchCarToRandomPath(AICarScript car)
    {
        if (pathManager == null || pathManager.availablePaths == null) return;
        int count = pathManager.availablePaths.Count;
        if (count <= 1) return;

        int current = car.assignedSubPathIndex;
        int next;
        do { next = Random.Range(0, count); } while (next == current);

        car.SwitchToPath(pathManager, next);
    }

    #endregion

    #region Player Loading

    void CreatePlayersArray()
    {
        loader.ConfigGame(filepath, numberOfCars);
        lapsToComplete = loader.numberOfLaps;
        spawnTime = loader.miliSecondsDelay / 1000f;
        playersArray = loader.playersArray;
    }

    IEnumerator InstantiatePlayers()
    {
        if (playerCreated < playersArray.Count)
        {
            // Instancia prefab
            GameObject raceCar = Instantiate(Resources.Load("SkyCar")) as GameObject;
            if (startingPoint != null)
                raceCar.transform.position = startingPoint.transform.position;

            var raceCarAI = raceCar.GetComponent<AICarScript>();

            // Datos del jugador
            raceCarAI.playerName = playersArray[playerCreated].name;
            raceCarAI.velocity = playersArray[playerCreated].velocity;
            raceCarAI.bodyColor = playersArray[playerCreated].bodyColor;
            raceCarAI.gameManager = this;

            // Asignación de subpath del ÚNICO PathManager
            int subIndex = GetSubPathIndexForCar(playerCreated);
            raceCarAI.SwitchToPath(pathManager, subIndex);

            // Spawn directly at path starting point
            var sub = pathManager.availablePaths[subIndex];
            var wps = sub.waypoints;
            if (wps != null && wps.Length >= 2)
            {
                Vector3 p0 = wps[0].position;
                Vector3 p1 = wps[1].position;
                Vector3 forward = (p1 - p0).normalized;

                // Position exactly at the first waypoint, looking towards the second
                raceCar.transform.SetPositionAndRotation(p0, Quaternion.LookRotation(forward));

                raceCarAI.currentPathObj = 0;
                raceCarAI.remainingNodes = wps.Length;
                raceCarAI.skipStartAutoPositioning = true;
            }
            else
            {
                Debug.LogError("[GameManager] Cada subpath necesita al menos 2 waypoints.");
            }

            // Flags globales
            raceCarAI.canSwitchPaths = allowPathSwitching;
            raceCarAI.pathSwitchProbability = globalPathSwitchProbability;

            carObjects.Add(raceCar);

            Debug.Log($"[GameManager] Carro '{raceCarAI.playerName}' creado en subpath {subIndex}.");

            playerCreated++;
            yield return new WaitForSeconds(spawnTime);
            StartCoroutine(InstantiatePlayers());
        }
        else
        {
            playerCreated = 0;
            yield return null;
        }
    }

    #endregion

    #region Leaderboard

    public void UpdateLeaderboardUI()
    {
        var leaderboard = GetLeaderboard();

        for (int i = 0; i < leaderboardTexts.Length; i++)
        {
            if (i < leaderboard.Count)
            {
                var car = leaderboard[i].car.GetComponent<AICarScript>();
                string pathInfo = car.pathGroup != null ? $" (Path: {car.pathGroup.name})" : "";
                leaderboardTexts[i].text = $"{i + 1}. {car.playerName} - Laps: {car.lapsCompleted}{pathInfo}";
            }
            else
            {
                leaderboardTexts[i].text = $"{i + 1}. ---";
            }
        }
    }

    public List<CarRaceState> GetLeaderboard()
    {
        List<CarRaceState> leaderboard = new List<CarRaceState>();
        foreach (GameObject car in carObjects)
        {
            if (car != null) // Only add if not destroyed
            {
                var ai = car.GetComponent<AICarScript>();
                if (ai != null)
                    leaderboard.Add(new CarRaceState(car));
            }
        }

        leaderboard.Sort((a, b) =>
        {
            int lapCompare = b.laps.CompareTo(a.laps);
            if (lapCompare != 0) return lapCompare;
            return b.pathIndex.CompareTo(a.pathIndex);
        });

        return leaderboard;
    }

    #endregion

    #region End Game

    public void CheckConditions(GameObject car, int lap, int remainingNode)
    {
        if (lap >= lapsToComplete)
        {
            CleanGameElements();

            if (winner != null)
            {
                winner.text += '\n' + car.GetComponent<AICarScript>().playerName;
            }

            resetCanvas.SetActive(true);
        }


    }

    public void OnCarFinished(GameObject car)
    {
        // Guarda el nombre (opcional)
        var ai = car.GetComponent<AICarScript>();
        if (ai != null)
        {
            finishOrder.Add(ai.playerName);
            if (winner != null)
            {
                // Muestra el último que terminó (o puedes mostrar el primero, a tu gusto)
                winner.text += (winner.text.Length > 0 ? "\n" : "") + ai.playerName;
            }
        }

        // Quita del leaderboard y destruye solo ese coche
        if (carObjects.Contains(car)) carObjects.Remove(car);

        // Si manejabas UI por-coche en carInfoObjects, destrúyela también
        // (solo si tienes algo ahí)
        // var ui = carInfoObjects.FirstOrDefault(x => ... );
        // if (ui) { Destroy(ui); carInfoObjects.Remove(ui); }

        Destroy(car);

        // Si ya no queda ninguno, muestra el reset (o haz lo que gustes)
        if (carObjects.Count == 0 && resetCanvas != null)
            resetCanvas.SetActive(true);
    }


    void CleanGameElements()
    {
        foreach (GameObject carObject in carObjects) Destroy(carObject);
        carObjects.Clear();

        foreach (GameObject carInfoObject in carInfoObjects) Destroy(carInfoObject);
        carInfoObjects.Clear();
    }

    public void ResetGame()
    {
        Debug.Log("[GameManager] Reset");
        CreatePlayersArray();
        resetCanvas.SetActive(false);
        StartCoroutine(InstantiatePlayers());
    }

    #endregion
}

public enum PathAssignmentMode
{
    Sequential,
    Random,
    AllSamePath,
    Alternating
}

[System.Serializable]
public class CarRaceState
{
    public GameObject car;
    public AICarScript ai;
    public int laps;
    public int pathIndex;

    public CarRaceState(GameObject car)
    {
        this.car = car;
        this.ai = car.GetComponent<AICarScript>();
        this.laps = ai.lapsCompleted;
        this.pathIndex = ai.currentPathObj;
    }
}