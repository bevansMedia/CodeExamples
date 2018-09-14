using System.Collections.Generic;
using UnityEngine;
using Photon;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Text;
using System;
using System.Net;
using JetBrains.Annotations;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class PlayerInfo : PunBehaviour
{
    public static PlayerInfo LocalPlayerInfo { get; private set; }

    public PhotonPlayer PhotonPlayer;
    public int PlayerID = -1;
    public string PlayerName = "";
    public bool IsAI;
    public bool IsSpectator;
    public int ColourIndex;
    public int FactionIndex;
    public int TeamIndex;
    public int PreferredSpawnLocation = -1;

    public bool IsReady;

    public Action<int> OnColourSelectionChanged;
    public Action OnPlayerDataChanged;
    public Action OnPlayerRemoved;
    
    [Header("Lobby")]
    [FormerlySerializedAs("playerEntryPrefab")] 
    public GameObject PlayerEntryPrefab;
    
    private LobbyPlayerEntry _lobbyPlayerEntry;

    [Header("Game")]
    public GameObject PlayerManager;
    
    public static readonly Dictionary<int, PlayerInfo> AssignedIDs = new Dictionary<int, PlayerInfo>();
    private static readonly List<int> SelectedColors = new List<int>();

    #region Unity Events
    
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (SelectedColors.Contains(ColourIndex))
            SelectedColors.Remove(ColourIndex);
    }
    
    private void OnDestroy()
    {
        AssignedIDs.Remove(PlayerID);

        if (LobbyPanel.Instance && LobbyPanel.Instance.gameObject.activeInHierarchy)
            LobbyPanel.Instance.MapSelection.UpdateUi();
        
        OnPlayerRemoved?.Invoke();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "Main_Menu":
                SetPhotonPlayerReady(false); //Reset readiness
                Destroy(gameObject);
                return;
            case "Game_UI":
                return;
        }

        //Pick Random Faction if Player hasn't chosen one
        SetFaction(FactionIndex == 0 ? Random.Range(1, 4) : FactionIndex);

        //Create a Manager for the new scene load
        CreatePlayerManager();   
    }
    
    #endregion
    
    #region Photon Functions

    public override void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        base.OnPhotonInstantiate(info);

        PhotonPlayer = photonView.owner;

        //Is AI?
        if (photonView.instantiationData != null &&
            (bool)photonView.instantiationData[0])
                SetAsAI();

        //Name GameObject
        gameObject.name += IsAI ? " AI" : photonView.owner.NickName;

        //Set Player Name
        PlayerName = IsAI ? "AI" : photonView.owner.NickName;

        if (PhotonNetwork.isMasterClient)
            SetPlayerId();

        if (photonView.isMine && IsAI == false)
            LocalPlayerInfo = this;

        //Create the lobby entry
        if (photonView.isMine && 
            !PhotonNetwork.offlineMode && 
            SceneManager.GetActiveScene().name == "Main_Menu")
        {
            CreatePlayerLobbyEntry();
        }
    }

    private void CreatePlayerLobbyEntry()
    {
        var data = new object[1];
        data[0] = photonView.viewID;

        var playerLobbyEntry = PhotonNetwork.Instantiate(
            PlayerEntryPrefab.name, 
            Vector3.zero, 
            Quaternion.identity, 
            0,
            data);

        _lobbyPlayerEntry = playerLobbyEntry.GetComponent<LobbyPlayerEntry>();
    }
    
    private void SetPhotonPlayerReady(bool ready)
    {
        var playerProperties = PhotonPlayer.CustomProperties;
        playerProperties["Ready"] = ready;
        PhotonPlayer.SetCustomProperties(playerProperties);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        AssignedIDs.Clear();

        if(!IsAI)
            Destroy(gameObject); //Was PhotonDestroy, but was only deactivating GO, so it remained
    }

    public override void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
    {
        base.OnPhotonPlayerDisconnected(otherPlayer);

        if (!Equals(otherPlayer, PhotonPlayer)) return;
        
        AssignedIDs.Remove(PlayerID);
        
        if(!IsAI)
            Destroy(gameObject);
    }
    
    #endregion

    #region Set PlayerID

    private void SetPlayerId()
    {
        var i = 0;

        while (AssignedIDs.Keys.Contains(i))
            i++;

        photonView.RPC(nameof(RpcSetPlayerId), PhotonTargets.AllBuffered, i);
    }

    [PunRPC]
    private void RpcSetPlayerId(int id)
    {
        PlayerID = id;
        AssignedIDs.Add(id, this);

        SetColorIndex(id);
    }

    #endregion

    #region Set Player Name

    public void SetPlayerName(string playerName)
    {
        photonView.RPC(nameof(RpcSetPlayerName), PhotonTargets.AllBuffered, playerName);
    }

    [PunRPC]
    private void RpcSetPlayerName(string playerName)
    {
        PlayerName = playerName;
    }

    #endregion

    #region Set Color

    public void SetColorIndex(int index)
    {
        photonView.RPC(nameof(RpcSetColorIndex), PhotonTargets.OthersBuffered, index);
        ForceColourSelection(index);
    }

    [PunRPC]
    private void RpcSetColorIndex(int index)
    {
        ForceColourSelection(index);
        OnColourSelectionChanged?.Invoke(ColourIndex);
    }

    public void SelectNextColour()
    {
        //Remove current selection
        SelectedColors.Remove(ColourIndex);

        //Increment select, loop to 0 if over
        ColourIndex = (int)Mathf.Repeat(ColourIndex + 1f, SharedComponents.PlayerColorLibrary.colors.Count);

        while (SelectedColors.Contains(ColourIndex))
        {
            ColourIndex = (int)Mathf.Repeat(ColourIndex + 1f, SharedComponents.PlayerColorLibrary.colors.Count);
        }

        AddColourSelectionToList();
    }

    private void ForceColourSelection(int selection)
    {
        //Debug.Log("Forcing Color: " + selection);
        SelectedColors.Remove(selection);
        ColourIndex = selection;

        AddColourSelectionToList();
    }

    private void AddColourSelectionToList()
    {
        SelectedColors.Add(ColourIndex);

        PlayerInfoChanged();
    }

    #endregion

    #region Set As AI

    private void SetAsAI()
    {
        photonView.RPC(nameof(RpcSetAsAI), PhotonTargets.AllBuffered);
    }

    [PunRPC]
    private void RpcSetAsAI()
    {
        IsAI = true;
    }

    #endregion

    #region Set As Spectator

    public void SetAsSpectator()
    {
        photonView.RPC(nameof(RpcSetAsSpectator), PhotonTargets.AllBuffered);
    }

    [PunRPC]
    private void RpcSetAsSpectator()
    {
        IsSpectator = true;
    }

    #endregion

    #region Set Team

    public void SetTeam(int teamIndex)
    {
        TeamIndex = teamIndex;
        photonView.RPC(nameof(RpcSetTeam), PhotonTargets.OthersBuffered, teamIndex);
    }

    [PunRPC]
    private void RpcSetTeam(int teamIndex)
    {
        TeamIndex = teamIndex;
        PlayerInfoChanged();
    }

    #endregion

    #region Set Faction

    public void SetFaction(int factionIndex)
    {
        FactionIndex = factionIndex;
        photonView.RPC(nameof(RpcSetFaction), PhotonTargets.OthersBuffered, factionIndex);
    }

    [PunRPC]
    private void RpcSetFaction(int factionIndex)
    {
        FactionIndex = factionIndex;
        PlayerInfoChanged();
    }

    #endregion

    #region Set Preferred Player Spawn

    public void SetPreferredPlayerSpawn(int spawnId)
    {
        photonView.RPC(nameof(RpcSetPreferredPlayerSpawn), PhotonTargets.AllBuffered, spawnId);
    }

    [PunRPC]
    private void RpcSetPreferredPlayerSpawn(int spawnId)
    {
        PreferredSpawnLocation = spawnId;
    }

    #endregion
    
    public void CreatePlayerManager()
    {
        if (!photonView.isMine) return;
        
        var data = new object[1];
        data[0] = PlayerID;

        //Create PlayerManager GameObject
        PlayerManager =
            PhotonNetwork.Instantiate(
                "[Player Manager Logic]",
                Vector3.zero,
                Quaternion.identity,
                0,
                data);

        IsReady = true;

        if (!IsAI)
            SetPhotonPlayerReady(true);
    }
    
    private void PlayerInfoChanged()
    {
        OnPlayerDataChanged?.Invoke();
    }

    public void RemovePlayer()
    {
        if(IsAI)
        {
            PhotonNetwork.Destroy(_lobbyPlayerEntry.gameObject);
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            PhotonNetwork.CloseConnection(photonView.owner);
        }
    }
    
    public bool IsLocalActivePlayer()
    {
        //Remote client
        if (!photonView.isMine)
            return false;

        return !IsAI;
    }

    #region Debug
    
    [ContextMenu("Print Player List")]
    public void DebugPrintPlayerList()
    {
        var sb = new StringBuilder();

        foreach (var pair in AssignedIDs)
            sb.AppendLine($"{pair.Key}: {pair.Value.PlayerName}");

        Debug.Log(sb.ToString());
    }
    
    #endregion
}

