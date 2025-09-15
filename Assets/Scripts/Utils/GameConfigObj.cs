using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class GameConfigObj 
{
    public GameConfiguration GameConfiguration;
    public List<Player> Players;
}

[Serializable]
public class GameConfiguration
{
    public int lapsNumber;
    public float playersInstantiationDelay;
}

[Serializable]
public class Player
{
    public string Name;
    public int Velocity;
    public string Color;

}

