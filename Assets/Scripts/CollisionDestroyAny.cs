using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionDestroyAny : MonoBehaviour
{
    [Header("Que hacer al tocar")]
    [Tooltip("Si esta activo, solo destruye el coche. Si esta apagado, intenta notificar a GameManager.OnCarFinished.")]
    public bool destroyOnly = false;

    [Header("Filtro opcional por Tag")]
    public bool requireTag = true;
    public string carTag = "Car";   // Aseg�rate de poner este Tag al root del coche

    [Header("Referencias (opcional)")]
    public GameManager gameManager; // Puedes arrastrar uno desde la escena. Si es null, se buscar�.

    private void OnTriggerEnter(Collider other)
    {
        // Localiza el AICarScript aunque el collider sea de un hijo del coche
        var carAI = other.GetComponentInParent<AICarScript>();
        if (carAI == null) return; // No es un coche

        // Si quieres filtrar por Tag:
        if (requireTag)
        {
            // Comprueba tag en el collider, en su rigidbody o en el root del coche
            bool hasTag =
                other.CompareTag(carTag) ||
                (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(carTag)) ||
                carAI.gameObject.CompareTag(carTag);

            if (!hasTag) return; // No tiene el tag esperado
        }

        // Si no nos dieron GameManager, intenta encontrar uno
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

        if (destroyOnly || gameManager == null)
        {
            // Elimina SOLO el coche (sin tocar UI/listas)
            Destroy(carAI.gameObject);
        }
        else
        {
            // Usa tu flujo "fin individual" para mantener leaderboard y UI
            gameManager.OnCarFinished(carAI.gameObject);
        }
    }

    // (Opcional) dibuja el �rea en escena
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.25f);
        var c = GetComponent<Collider>() as BoxCollider;
        if (c != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(c.center, c.size);
        }
    }

}
