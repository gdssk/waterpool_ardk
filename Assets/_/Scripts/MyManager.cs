using System;
using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR.Networking;
using Niantic.ARDK.AR.Networking.ARNetworkingEventArgs;
using Niantic.ARDK.Extensions;
using Niantic.ARDK.Networking;
using Niantic.ARDK.Networking.HLAPI;
using Niantic.ARDK.Networking.HLAPI.Authority;
using Niantic.ARDK.Networking.HLAPI.Data;
using Niantic.ARDK.Networking.HLAPI.Object;
using Niantic.ARDK.Networking.HLAPI.Routing;
using Niantic.ARDK.Networking.MultipeerNetworkingEventArgs;
using Niantic.ARDK.Utilities;
using UnityEngine;

public class MyManager : MonoBehaviour
{
    private bool _synced;
    private bool _isHost;
    private bool _gameStart;
    
    /// Cache your location every frame
    private Vector3 _location;
    
    private IPeer _self;
    
    private GameObject _player;
    
    private IARNetworking _arNetworking;
    
    /// HLAPI Networking objects
    private IHlapiSession _manager;
    
    private IAuthorityReplicator _auth;
    
    private INetworkedField<Vector3> _fieldPosition;
    
    private INetworkedField<byte> _gameStarted;
    
    private INetworkedField<string> _scoreText;
    
    private MessageStreamReplicator<Vector3> _hitStreamReplicator;

    /// <summary>
    /// ARセッション
    /// </summary>
    [SerializeField]
    private ARSessionManager _sessionManager;
    
    /// <summary>
    /// 
    /// </summary>
    [SerializeField]
    private FeaturePreloadManager preloadManager;

    /// <summary>
    /// 
    /// </summary>
    [SerializeField]
    private ARNetworkingManager _networkingManager;
    
    /// <summary>
    /// ARセッションの初期化
    /// </summary>
    public void Initialize()
    {
        Debug.Log("Initialize");
        _sessionManager.Initialize();
        _sessionManager.CreateSession();
        _sessionManager.Run();
        Debug.Log("Initialized");
    }

    /// <summary>
    /// Start
    /// </summary>
    private void Start()
    {
        ARNetworkingFactory.ARNetworkingInitialized += OnAnyARNetworkingSessionInitialized;
        preloadManager.ProgressUpdated += PreloadProgressUpdated;
    }
    
    /// <summary>
    /// OnDestroy
    /// </summary>
    private void OnDestroy()
    {
        ARNetworkingFactory.ARNetworkingInitialized -= OnAnyARNetworkingSessionInitialized;
        if (_arNetworking != null)
        {
            _arNetworking.PeerStateReceived -= OnPeerStateReceived;
            _arNetworking.ARSession.FrameUpdated -= OnFrameUpdated;
            _arNetworking.Networking.Connected -= OnDidConnect;
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    private void PreloadProgressUpdated(FeaturePreloadManager.PreloadProgressUpdatedArgs args)
    {
        Debug.Log(nameof(PreloadProgressUpdated));
        
        if (args.PreloadAttemptFinished)
        {
            if (args.FailedPreloads.Count > 0)
            {
                Debug.LogError("Failed to download resources needed to run AR Multiplayer");
                return;
            }
            _networkingManager.enabled = true;
            preloadManager.ProgressUpdated -= PreloadProgressUpdated;
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    private void OnAnyARNetworkingSessionInitialized(AnyARNetworkingInitializedArgs args)
    {
        Debug.Log(nameof(OnAnyARNetworkingSessionInitialized));
        Debug.Log(args);
        
        _arNetworking = args.ARNetworking;
        _arNetworking.PeerStateReceived += OnPeerStateReceived;
        _arNetworking.ARSession.FrameUpdated += OnFrameUpdated;
        _arNetworking.Networking.Connected += OnDidConnect;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    private void OnPeerStateReceived(PeerStateReceivedArgs args)
    {
        Debug.Log(nameof(OnPeerStateReceived));
        if (_self.Identifier != args.Peer.Identifier)
        {
            if (args.State == PeerState.Stable)
            {
                _synced = true;
                if (_isHost)
                {
                    Debug.Log("Host");
                }
                else
                {
                    Debug.Log("Slave");
                }
            }
            return;
        }
        var message = args.State.ToString();
        Debug.Log("We reached state " + message);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    private void OnFrameUpdated(FrameUpdatedArgs args)
    {
        _location = MatrixUtils.PositionFromMatrix(args.Frame.Camera.Transform);
        if (_player == null) { return; }

        var playerPos = _player.transform.position;
        playerPos.x = _location.x;
        _player.transform.position = playerPos;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="connectedArgs"></param>
    private void OnDidConnect(ConnectedArgs connectedArgs)
    {
        Debug.Log(nameof(OnDidConnect));
        
        _isHost = connectedArgs.IsHost;
        _self = connectedArgs.Self;
        _manager = new HlapiSession(19244);

        var group = _manager.CreateAndRegisterGroup(new NetworkId(4321));
        _auth = new GreedyAuthorityReplicator("pongHLAPIAuth", group);
        _auth.TryClaimRole(_isHost ? Role.Authority : Role.Observer, () => {}, () => {});

        var authToObserverDescriptor = _auth.AuthorityToObserverDescriptor(TransportType.ReliableUnordered);
        _fieldPosition = new NetworkedField<Vector3>("fieldReplicator", authToObserverDescriptor, group);
        _fieldPosition.ValueChangedIfReceiver += OnFieldPositionDidChange;
        _scoreText = new NetworkedField<string>("scoreText", authToObserverDescriptor, group);
        _scoreText.ValueChanged += OnScoreDidChange;

        _gameStarted = new NetworkedField<byte>("gameStarted", authToObserverDescriptor, group);
        _gameStarted.ValueChanged += value =>
        {
            _gameStart = Convert.ToBoolean(value.Value.Value);
            if (_gameStart)
            {
                Debug.Log("GameStart");
                // _ball = FindObjectOfType<BallBehaviour>().gameObject;
            }
        };
        _hitStreamReplicator = new MessageStreamReplicator<Vector3>
        (
            "hitMessageStream",
            _arNetworking.Networking.AnyToAnyDescriptor(TransportType.ReliableOrdered),
            group
        );
        _hitStreamReplicator.MessageReceived += (args) =>
        {
            Debug.Log("Ball was hit");
            if (_auth.LocalRole != Role.Authority) { return; }
        };
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    private void OnFieldPositionDidChange(NetworkedFieldValueChangedArgs<Vector3> args)
    {
        var value = args.Value;
        if (!value.HasValue) { return; }

        var offsetPos = value.Value + new Vector3(0, 0, 1);
        _player.transform.position = offsetPos;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="args"></param>
    private void OnScoreDidChange(NetworkedFieldValueChangedArgs<string> args)
    {
        Debug.Log(args.Value.GetOrDefault());
    }
}
