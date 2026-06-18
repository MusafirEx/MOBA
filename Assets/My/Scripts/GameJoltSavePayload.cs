using System;

[Serializable]
public class GameJoltAuthResponse
{
    public GameJoltResponse response;
}

[Serializable]
public class GameJoltDataResponse
{
    public GameJoltDataInner response;
}

[Serializable]
public class GameJoltResponse
{
    public string success;
    public string message;
}

[Serializable]
public class GameJoltDataInner
{
    public string success;
    public string message;
    public string data;
}
