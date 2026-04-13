using System;

[Serializable]
public class gameDataFileSave
{
    public string lastUpdated = "";
    public string username = "";
    public string gender = "female";

    public float volume = 70f;
    public float sensitivity = 30f;
    public float fieldOfView = 84f;

    public int level = 1;
    public float exp = 0f;

    public string specialTool = "none";
    public bool ouijaActive = false;
    public string ghostPreference = "random";

    public int totalSingleMatch = 0;
    public int totalMultiMatch = 0;
    public int totalSingleWins = 0;
    public int totalMultiWins = 0;
}