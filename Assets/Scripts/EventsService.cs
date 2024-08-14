using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class EventsService : MonoBehaviour
{
    [Serializable]
    public class EventData
    {
        public string type;
        public string data;
    }

    [Serializable]
    public class EventWrapper
    {
        public List<EventData> events = new();
    }

    [SerializeField]
    private string _serverUrl = "https://eidolon.com/events";

    [SerializeField]
    private float _cooldownBeforeSend = 2f;

    private readonly List<EventData> _eventsQueue = new();
    private bool _isCooldownActive;
    private Coroutine _sendEventsWithCooldownCoroutine;
    private const string _saveAnalyticsPrefsKey = "EVENTS_CACHE";


    private void Start()
    {
        if (!PlayerPrefs.HasKey(_saveAnalyticsPrefsKey))
        {
            Log("[Init] - 0 cached events");
            return;
        }

        var jsonData = PlayerPrefs.GetString(_saveAnalyticsPrefsKey, JsonUtility.ToJson(new EventWrapper()));
        var wrapper = JsonUtility.FromJson<EventWrapper>(jsonData);
        _eventsQueue.AddRange(wrapper.events);
        Log("[Init] - Found unsent events. Adding to queue.");
    }
    
    public void TrackEvent(string type, string data)
    {
        var newEvent = new EventData { type = type, data = data };
        _eventsQueue.Add(newEvent);

        if (!_isCooldownActive)
        {
            _sendEventsWithCooldownCoroutine = StartCoroutine(SendEventsWithCooldown());
        }

        Log("Track Event " + $"Type: {type}, Data: {data}");
    }

    private IEnumerator SendEventsWithCooldown()
    {
        _isCooldownActive = true;

        yield return new WaitForSeconds(_cooldownBeforeSend);

        yield return StartCoroutine(FlushEvents());

        _isCooldownActive = false;

        if (_eventsQueue.Count > 0)
        {
            _sendEventsWithCooldownCoroutine = StartCoroutine(SendEventsWithCooldown());
        }
    }

    private IEnumerator FlushEvents()
    {
        if (_eventsQueue.Count == 0) yield break;
        Log("Start Flush");

        var wrapper = new EventWrapper { events = _eventsQueue.ToList() };
        var jsonData = JsonUtility.ToJson(wrapper);

        using var request = new UnityWebRequest(_serverUrl, "POST");
        var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Log("Flush Success");
            _eventsQueue.Clear();
            SaveUnsentEvents();
        }
        else
        {
            Log("Flush Failed");
            SaveUnsentEvents();
        }
    }

    private void SaveUnsentEvents()
    {
        if (_eventsQueue.Count == 0 && PlayerPrefs.HasKey(_saveAnalyticsPrefsKey))
        {
            PlayerPrefs.DeleteKey(_saveAnalyticsPrefsKey);
        }
        else
        {
            var jsonData = JsonUtility.ToJson(new EventWrapper { events = _eventsQueue });
            PlayerPrefs.SetString(_saveAnalyticsPrefsKey, jsonData);
        }
    }

    private void Log(string info)
    {
        if (!Debug.isDebugBuild) return;
        Debug.LogFormat(this, "[Events] - {0}", info);
    }

    private void OnApplicationQuit()
    {
        if (_sendEventsWithCooldownCoroutine != null)
        {
            StopCoroutine(_sendEventsWithCooldownCoroutine);
        }

        SaveUnsentEvents();
    }
}