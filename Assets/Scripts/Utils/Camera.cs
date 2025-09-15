using UnityEngine;
using Cinemachine;
using System.Collections;

public class CameraAutoCycle : MonoBehaviour
{
    [Header("Tus vCams en orden")]
    public CinemachineVirtualCamera[] vcams;

    [Header("Prioridades")]
    [Range(1, 100)] public int activePriority = 20;
    [Range(-10, 19)] public int inactivePriority = 10;

    [Header("Timing")]
    public float holdSeconds;     // tiempo que dura cada cámara
    public bool loop = true;

    private int current = -1;
    private CinemachineBrain brain;

    void Awake()
    {
        brain = Camera.main.GetComponent<CinemachineBrain>();
        // Seguridad: reactiva todas para permitir blends
        foreach (var v in vcams) if (v) v.gameObject.SetActive(true);
    }

    void Start()
    {
        if (vcams == null || vcams.Length == 0) return;
        StartCoroutine(AutoRun());
    }

    IEnumerator AutoRun()
    {
        do
        {
            for (int i = 0; i < vcams.Length; i++)
            {
                SwitchTo(i);
                // espera a que termine el blend (si lo hay)
                yield return null;
                while (brain && brain.IsBlending) yield return null;

                // mantener esta cámara por holdSeconds
                yield return new WaitForSeconds(holdSeconds);
            }
        }
        while (loop);
    }

    public void SwitchTo(int index)
    {
        current = Mathf.Clamp(index, 0, vcams.Length - 1);
        for (int i = 0; i < vcams.Length; i++)
            if (vcams[i]) vcams[i].Priority = (i == current) ? activePriority : inactivePriority;
    }
}
