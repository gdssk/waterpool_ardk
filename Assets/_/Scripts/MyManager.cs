using Niantic.ARDK.Extensions;
using UnityEngine;

public class MyManager : MonoBehaviour
{
    [SerializeField]
    private ARSessionManager _sessionManager;

    /// <summary>
    /// Initialize
    /// </summary>
    public void Initialize()
    {
        Debug.Log("Initialize");
        _sessionManager.Initialize();
        _sessionManager.CreateSession();
        _sessionManager.Run();
        Debug.Log("Initialized");
    }
}
