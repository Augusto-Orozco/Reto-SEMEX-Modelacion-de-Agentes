using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public class LoadPlayer
{
    public int numberOfLaps = 4;
    public float miliSecondsDelay = 50f;
    public List<PlayerData> playersArray = new List<PlayerData>();

private string ReadFile(string file)
{
    string fileContent = "";
    string filePath = Application.dataPath + file;
    
    try
    {
        using (StreamReader fileReader = new StreamReader(filePath, Encoding.Default))
        {
            fileContent = fileReader.ReadToEnd(); // Actually read the file content
        } // StreamReader automatically disposed here
    }
    catch (System.Exception e)
    {
        Debug.LogError("Error reading file: " + filePath + "\n" + e.Message);
    }
    
    return fileContent;
}
    public void ConfigGame(string file, int numberOfCars)
    {
        GameConfigObj GameConfigObj = JsonUtility.FromJson<GameConfigObj>(ReadFile(file));

        numberOfLaps = GameConfigObj.GameConfiguration.lapsNumber;
        miliSecondsDelay = GameConfigObj.GameConfiguration.playersInstantiationDelay;

        for (int i = 0; i < numberOfCars; i++)
        {
            PlayerData playerData = new PlayerData();
            Color color = new Color();
            int index = Random.Range(0, GameConfigObj.Players.Count);

            playerData.name = GameConfigObj.Players[index].Name;
            playerData.velocity = GameConfigObj.Players[index].Velocity;
            ColorUtility.TryParseHtmlString(GameConfigObj.Players[index].Color, out color);
            playerData.bodyColor = color;
            playersArray.Add(playerData);
        }
    }

}