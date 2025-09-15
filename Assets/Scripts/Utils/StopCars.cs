using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StopCars : MonoBehaviour
{
    public TraficController trafficLight;
    private HashSet<AICarScript> stopped = new HashSet<AICarScript>();

    void Awake()
    {
        if (trafficLight == null)
            trafficLight = GetComponentInParent<TraficController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var car = other.GetComponentInParent<AICarScript>();
        if (car == null) return;
        if (!car.gameObject.CompareTag("Car")) return;

        // MODIFICACIÓN: Notificar al semáforo que llegó un auto
        if (trafficLight != null)
            trafficLight.CarArrived();

        if (trafficLight != null &&
           (trafficLight.CurrentState == TraficController.LightState.Red
         || trafficLight.CurrentState == TraficController.LightState.Yellow))
        {
            Debug.Log("[StopCars] 🚦 Deteniendo " + car.name + " (amarillo/rojo)");
            car.StopAtLight();
            stopped.Add(car);
        }
        else
        {
            Debug.Log("[StopCars] ✅ Entró " + car.name + " pero semáforo no es amarillo/rojo → sigue");
            car.ResumeMovement();
            stopped.Remove(car);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var car = other.GetComponentInParent<AICarScript>();
        if (car == null) return;
        if (!car.gameObject.CompareTag("Car")) return;

        // MODIFICACIÓN: Notificar al semáforo que salió un auto
        if (trafficLight != null)
            trafficLight.CarLeft();

        Debug.Log("[StopCars] OnTriggerExit: " + car.name + " sale del trigger → permitir movimiento");
        car.ResumeMovement();
        stopped.Remove(car);
    }

    public void ReleaseCars()
    {
        Debug.Log("[StopCars] ReleaseCars() — coches detenidos: " + stopped.Count);
        foreach (var car in stopped)
        {
            if (car == null) continue;
            car.ResumeMovement();
            Debug.Log("[StopCars] → liberado " + car.name);
        }
        stopped.Clear();
    }

    void OnDisable()
    {
        if (stopped.Count > 0) ReleaseCars();
    }
}