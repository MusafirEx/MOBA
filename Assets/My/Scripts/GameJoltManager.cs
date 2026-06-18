using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GameJoltManager : MonoBehaviour
{
    public static GameJoltManager Instance { get; private set; }

    public GameJoltConfig config;
    public string username;
    public string userToken;
    public bool IsAuthenticated { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetCredentials(string user, string token)
    {
        username = user;
        userToken = token;
    }

    public void Auth(Action<bool, string> onComplete)
    {
        StartCoroutine(AuthRoutine(onComplete));
    }

    public void SetData(string key, string data, Action<bool, string> onComplete)
    {
        StartCoroutine(SetDataRoutine(key, data, onComplete));
    }

    public void GetData(string key, Action<bool, string> onComplete)
    {
        StartCoroutine(GetDataRoutine(key, onComplete));
    }

    private IEnumerator AuthRoutine(Action<bool, string> onComplete)
    {
        string url = BuildSignedUrl("/users/auth/", $"game_id={Escape(config.gameId)}&username={Escape(username)}&user_token={Escape(userToken)}");
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        bool ok = req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Contains("\"success\":\"true\"");
        IsAuthenticated = ok;
        onComplete?.Invoke(ok, req.downloadHandler.text);
    }

    private IEnumerator SetDataRoutine(string key, string data, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Not authenticated");
            yield break;
        }

        string query = $"game_id={Escape(config.gameId)}&username={Escape(username)}&user_token={Escape(userToken)}&key={Escape(key)}&data={Escape(data)}";
        string url = BuildSignedUrl("/data-store/set/", query);
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        bool ok = req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Contains("\"success\":\"true\"");
        onComplete?.Invoke(ok, req.downloadHandler.text);
    }

    private IEnumerator GetDataRoutine(string key, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Not authenticated");
            yield break;
        }

        string query = $"game_id={Escape(config.gameId)}&username={Escape(username)}&user_token={Escape(userToken)}&key={Escape(key)}";
        string url = BuildSignedUrl("/data-store/", query);
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        bool ok = req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Contains("\"success\":\"true\"");
        onComplete?.Invoke(ok, req.downloadHandler.text);
    }

    private string BuildSignedUrl(string endpoint, string query)
    {
        string unsigned = $"{config.apiBaseUrl}{endpoint}?{query}";
        string signature = Md5(unsigned + config.privateKey);
        return unsigned + "&signature=" + signature;
    }

    private static string Escape(string value)
    {
        return UnityWebRequest.EscapeURL(value ?? string.Empty);
    }

    private static string Md5(string input)
    {
        using MD5 md5 = MD5.Create();
        byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
