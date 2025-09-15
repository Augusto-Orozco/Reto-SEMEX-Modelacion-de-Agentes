using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerInfoUI : MonoBehaviour
{
    public TextMeshProUGUI playerName;
    public TextMeshProUGUI lapsCompleted;

    public void updateLaps(int lap)
    {
        lapsCompleted.text = "Laps: " + lap + "/4";
    }
}
