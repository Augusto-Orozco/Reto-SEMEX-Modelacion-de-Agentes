using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TraficController : MonoBehaviour
{
    [Header("Luces del semáforo")]
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;

    [Header("Zona trigger (el cubo)")]
    public GameObject triggerZone; // 👉 arrastra aquí el cubo hijo

    [Header("Duraciones en segundos")]
    public float greenDuration = 6f;
    public float yellowDuration = 2f;
    public float redDuration = 5f;

    public enum LightState { Green, Yellow, Red }
    public LightState CurrentState { get; private set; } = LightState.Green;

    private StopCars stopScript;
    private int carsWaiting = 0;
        public int carsThreshold = 5; // Cambia según lo que consideres "muchos autos"
    public float minRedDuration = 2f; // Duración mínima de rojo si hay tráfico4

    public void CarArrived()
    {
        carsWaiting++;
    }

    public void CarLeft()
    {
        carsWaiting = Mathf.Max(0, carsWaiting - 1);
    }


    //private int state = 0; // 0 = verde, 1 = amarillo, 2 = rojo

    //public bool IsRed { get; private set; } // estado propio del semáforo

    void Start()
    {
        if (triggerZone != null)
            stopScript = triggerZone.GetComponentInChildren<StopCars>();
        StartCoroutine(TrafficLightCycle());
    }

    private IEnumerator TrafficLightCycle()
    {
        while (true)
        {
            SetGreen();
            yield return new WaitForSeconds(greenDuration);

            SetYellow();
            yield return new WaitForSeconds(yellowDuration);

            // Heurística: si hay muchos autos esperando, reduce el tiempo de rojo
            float dynamicRed = carsWaiting >= carsThreshold ? minRedDuration : redDuration;
            SetRed();
            yield return new WaitForSeconds(dynamicRed);
        }
    }

    //void Update()
    //{
    //    // Para pruebas: alternar con espacio
    //    if (Input.GetKeyDown(KeyCode.Space))
    //    {
    //        if (state == 0) SetYellow();
    //        else if (state == 1) SetRed();
    //        else SetGreen();
    //    }
    //}
    void SetGreen()
    {
        redLight.SetActive(false);
        yellowLight.SetActive(false);
        greenLight.SetActive(true);

        Debug.Log("[Traffic] → GREEN (liberando coches si los hay)");

        // Llamamos a Release antes de desactivar el trigger para forzar OnTriggerExit lógico
        if (stopScript == null && triggerZone != null)
            stopScript = triggerZone.GetComponentInChildren<StopCars>();

        if (stopScript != null) stopScript.ReleaseCars();

        if (triggerZone != null) triggerZone.SetActive(false);
    } 

    void SetYellow()
    {
        CurrentState = LightState.Yellow;
        redLight.SetActive(false);
        yellowLight.SetActive(true);
        greenLight.SetActive(false);
    
        if (triggerZone != null) triggerZone.SetActive(true); // 🚧 ahora sí frena
        Debug.Log("[Traffic] → YELLOW (frena)");
    }
    
    void SetRed()
    {
        CurrentState = LightState.Red;
        redLight.SetActive(true);
        yellowLight.SetActive(false);
        greenLight.SetActive(false);
    
        if (triggerZone != null) triggerZone.SetActive(true);
        Debug.Log("[Traffic] → RED (frena)");
    }

}
