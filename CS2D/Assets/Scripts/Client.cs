using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class Client : MonoBehaviour {
    private const string Localhost = "127.0.0.1";

    // Connection stuff
    public string Ip = Localhost;

    public int ServerPort;
    public int ClientPort;
    private Channel _channel;
    private readonly CommunicationManager _communicationManager;
    private bool _connected;

    // Player stuff
    public int PlayerId;

    private readonly Dictionary<int, PlayerNetworkView> _players;
    public GameObject PlayerPrefab;
    private PlayerController _ownPlayerController;

    // Simulation stuff
    private readonly List<GameData> _snapshots;

    public int BufferDesiredLength;
    public double SimulationSpeed;
    public double MaxDiffBetweenSnapshotsTime;
    private double _simulationStartingTime;
    private double _simulationTime;

    public PlayerNetworkView PlayerPublic;

    public Client() {
        _communicationManager = new CommunicationManager();
        _connected = false;
        _players = new Dictionary<int, PlayerNetworkView>();
        _snapshots = new List<GameData>();
        _simulationStartingTime = 0;
    }

    public void Start() {
        _channel = new Channel(Ip, ClientPort, ServerPort);
    }

    public void OnDestroy() {
        _channel.Disconnect();
    }

    public void Update() {
        if (!_connected && Input.GetKeyDown(KeyCode.Space)) {
            _communicationManager.SendMessage(ConnectPlayerMessage.CreateConnectPlayerMessageToSend(PlayerId));
            CleanClient();
        }
        else if (_connected && Input.GetKeyDown(KeyCode.Escape)) {
            var id = Random.Range(0, int.MaxValue);
            _communicationManager.SendMessage(new DisconnectPlayerMessage(id, PlayerId));
        }

        ReadMessages(); // Read messages from server
        Interpolate(); // Perform interpolation of snapshots
        SendInput(); // Send own data
    }

    // ===========================================================================================================
    // ===========================================================================================================

    private void CleanClient() {
        PlayerNetworkView player;
        if (_players.TryGetValue(PlayerId, out player)) {
            DisconnectPlayer(player);
        }
        _connected = false;
        _simulationStartingTime = 0.0;
        _ownPlayerController = null;
    }

    private void ReadMessages() {
        ReceiveMessages();
        ProcessMessages();
    }

    private void Interpolate() {
        DoInterpolate();
    }

    private void SendInput() {
        if (_ownPlayerController != null) {
            _communicationManager.SendMessage(new PlayerInputMessage(PlayerId, _ownPlayerController.PlayerInput));
        }
        var data = _communicationManager.BuildPacket();
        if (data != null) {
            _channel.Send(data);
        }
    }

    // ============================================================
    // Message reception
    // ============================================================

    private void ReceiveMessages() {
        Packet packet;
        while ((packet = _channel.GetPacket()) != null) {
            var bitBuffer = packet.buffer;
            var messageCount = bitBuffer.GetInt();
            for (var i = 0; i < messageCount; i++) {
                var serverMessage = ReceiveServerMessage(bitBuffer);
                if (serverMessage != null) {
                    _communicationManager.ReceiveMessage(serverMessage);
                }
            }
        }
    }

    // ======================================
    // Server message reception
    // ======================================


    private static Message ReceiveServerMessage(BitBuffer bitBuffer) {
        var messageType = bitBuffer.GetEnum<MessageType>((int) MessageType.TOTAL);

        Message message;
        switch (messageType) {
            case MessageType.PLAYER_CONNECTED:
                message = PlayerConnectedMessage.CreatePlayerConnectedMessageToReceive();
                break;
            case MessageType.PLAYER_DISCONNECTED:
                message = PlayerDisconnectedMessage.CreatePlayerDisconnectedMessageToReceive();
                break;
            case MessageType.SNAPSHOT:
                message = new SnapshotMessage();
                break;
            case MessageType.ACK_RELIABLE_MAX_WAIT_TIME:
                message = AckReliableMessage.CreateAckReliableMessageMessageToReceive();
                break;
            case MessageType.ACK_RELIABLE_SEND_EVERY_PACKET:
                message = AckReliableSendEveryFrameMessage.CreateAckReliableSendEveryFrameMessageMessageToReceive();
                break;
            default:
                return null; // Return null if the message is not a "from server" type
        }
        message.Load(bitBuffer);
        return message;
    }


    // ===========================================================================================================
    // ===========================================================================================================


    // ============================================================
    // Message processing
    // ============================================================

    private void ProcessMessages() {
        Message message;
        while ((message = _communicationManager.GetMessage()) != null) {
            ProcessServerMessage(message);
        }
    }

    // ======================================
    // Server message processing
    // ======================================

    private void ProcessServerMessage(Message msg) {
        switch (msg.Type) {
            case MessageType.PLAYER_CONNECTED:
                ProcessPlayerConnectedMessage(msg as PlayerConnectedMessage);
                return;
            case MessageType.PLAYER_DISCONNECTED:
                ProcessPlayerDisconnectedMessage(msg as PlayerDisconnectedMessage);
                return;
            case MessageType.SNAPSHOT:
                ProcessSnapshotMessage(msg as SnapshotMessage);
                return;
            default:
                return; // Do nothing if the message is not "from server" type
        }
    }

    private void ProcessPlayerConnectedMessage(PlayerConnectedMessage message) {
        var playerId = message.PlayerId;
        PlayerNetworkView player;
        if (_players.TryGetValue(playerId, out player)) {
            DisconnectPlayer(player); // Disconnect player if already connected, and reconnect it.
        }
        ConnectPlayer(playerId);
    }

    private void ProcessPlayerDisconnectedMessage(PlayerDisconnectedMessage message) {
        PlayerNetworkView player;
        if (_players.TryGetValue(message.PlayerId, out player)) {
            DisconnectPlayer(player);
        }
    }

    private void ProcessSnapshotMessage(SnapshotMessage message) {
        Debug.Log(message.GameSnapshot.Players[0].Position);
        var snapshot = message.GameSnapshot;
        _simulationStartingTime = 0.0.Equals(_simulationStartingTime) ? snapshot.Time : _simulationStartingTime;
        if (!_snapshots.Any() || _snapshots.Last().Time < snapshot.Time) {
            _snapshots.Add(snapshot);
        }
    }


    // ===========================================================================================================
    // ===========================================================================================================

    // ============================================================
    // Connection
    // ============================================================

    private void ConnectPlayer(int playerId) {
        PlayerNetworkView player;
        if (_players.TryGetValue(playerId, out player)) {
            DisconnectPlayer(player); // Disconnect player if already connected, and reconnect it.
        }

        var playerGameObject = Instantiate(PlayerPrefab);
        playerGameObject.name = string.Format("Player {0}", playerId);

        player = playerGameObject.GetComponent<PlayerNetworkView>();
        player.PlayerId = playerId;
        PlayerPublic = player;

        // Check if the connected player is the own player
        if (playerId.Equals(PlayerId)) {
            _connected = true;
            _ownPlayerController = playerGameObject.AddComponent<PlayerController>();
            _ownPlayerController.PlayerInput = new PlayerInput();
        }
        _players.Add(playerId, player);
    }

    private void DisconnectPlayer(PlayerNetworkView player) {
        Destroy(player.gameObject);
        _players.Remove(player.PlayerId);
    }

    // ===========================================================================================================
    // ===========================================================================================================

    // ============================================================
    // Interpolation
    // ============================================================

    private void DoInterpolate() {
        UpdateSpeed();
        _simulationTime += Time.deltaTime * SimulationSpeed;
        RemoveOldSnapshots();
        UpdateSpeed();

        if (_snapshots.Count <= 1) {
            return; // Can't interpolate if there is only one snapshot in the queue
        }

        GameData initial;
        GameData final;
        var diff = _snapshots.Last().Time - _simulationStartingTime - _simulationTime;
        if (MaxDiffBetweenSnapshotsTime < diff) {
            initial = _snapshots.Last();
            final = _snapshots.Last();
            _simulationTime = final.Time - _simulationStartingTime;
        }
        else {
            initial = _snapshots[0];
            final = _snapshots[1];
        }

        var interpolatedSnapshot = DoInterpolate(initial, final);
        ProcessInterpolatedSnapshot(interpolatedSnapshot);
    }

    private void UpdateSpeed() {
        var normal = 1 - 1.0 / (Math.Abs(BufferDesiredLength - _snapshots.Count) + 1);
        const double max = 1.1;
        var factor = (max - 1) * normal + 1;
        if (_snapshots.Count < BufferDesiredLength) {
            SimulationSpeed = SimulationSpeed > 1 ? 1.0 / factor : SimulationSpeed / factor;
        }
        else if (_snapshots.Count > BufferDesiredLength) {
            SimulationSpeed = SimulationSpeed < 1 ? factor : SimulationSpeed * factor;
        }
        else {
            SimulationSpeed = 1.0;
        }
    }

    private void RemoveOldSnapshots() {
        if (!_snapshots.Any()) {
            return; // Nothing to remove
        }

        GameData prev = null;
        var interpolated = _snapshots[0];

        while (_snapshots.Any() && _simulationTime > interpolated.Time - _simulationStartingTime) {
            prev = interpolated;
            interpolated = _snapshots[0];
            if (_simulationTime.CompareTo(interpolated.Time - _simulationStartingTime) > 0) {
                _snapshots.RemoveAt(0); // Remove first
            }
        }

        if (prev != null) {
            _snapshots.Insert(0, prev);
        }
    }

    private GameData DoInterpolate(GameData initial, GameData final) {
        if (final.Time.Equals(initial.Time)) {
            return final;
        }

        var t = (float) (_simulationTime - (initial.Time - _simulationStartingTime)) / (final.Time - initial.Time);
        var interpolatedPlayersData = new List<PlayerData>();

        foreach (var endPlayer in final.Players) {
            var interPosition = Vector2.zero;
            foreach (var playerData in initial.Players) {
                if (playerData.PlayerId.Equals(endPlayer.PlayerId)) {
                    interPosition = Vector2.Lerp(playerData.Position, endPlayer.Position, t);
                    break;
                }
            }
            interpolatedPlayersData.Add(new PlayerData {
                PlayerId = endPlayer.PlayerId,
                Position = interPosition
            });
        }
        return new GameData {
            PlayersData = interpolatedPlayersData,
            Time = (float) _simulationTime
        };
    }

    private void ProcessInterpolatedSnapshot(GameData snapshot) {
        var playersData = snapshot.Players;
        foreach (var data in playersData) {
            var playerId = data.PlayerId;
            PlayerNetworkView player;
            if (_players.TryGetValue(playerId, out player)) {
                player.UpdatePosition(data.Position);
            }
            else {
                ConnectPlayer(playerId);
            }
        }
    }
}