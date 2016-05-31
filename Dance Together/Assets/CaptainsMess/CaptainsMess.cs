using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class CaptainsMess : MonoBehaviour
{
    public string broadcastIdentifier = "CM";
    public int minPlayers = 2;
    public int maxPlayers = 4;
    public CaptainsMessPlayer playerPrefab;
    public float countdownDuration = 3; // Wait for this many seconds after people are ready before starting the game
    public CaptainsMessListener listener;
    public bool verboseLogging = false;
    public bool useDebugGUI = true;

    private CaptainsMessNetworkManager networkManager;

    void OnLevelWasLoaded(int levelIndex)
    {
        Debug.Log("!!! LEVEL LOAD !!!");
    }

    public void Awake()
    {
        ValidateConfig();

        // Create network manager
        networkManager = (Instantiate(Resources.Load("CaptainsMessNetworkManager")) as GameObject).GetComponent<CaptainsMessNetworkManager>();
        if (networkManager != null)
        {
            //networkManager.logLevel = 0;

            networkManager.name = "CaptainsMessNetworkManager";
            networkManager.runInBackground = false; // runInBackground is not recommended on iOS
            networkManager.broadcastIdentifier = broadcastIdentifier;
            networkManager.minPlayers = minPlayers;
            networkManager.SetMaxPlayers(maxPlayers);

            networkManager.allReadyCountdownDuration = countdownDuration;

            // I'm just using a single scene for everything
            networkManager.offlineScene = "";
            networkManager.onlineScene = "";

            networkManager.playerPrefab = playerPrefab.gameObject;
            networkManager.listener = listener;
            networkManager.verboseLogging = verboseLogging;

            // Optionally create Debug GUI
            if (useDebugGUI) {
                networkManager.GetComponent<CaptainsMessDebugGUI>().enabled = true;
            }
        }
        else
        {
            Debug.LogError("#CaptainsMess# Error creating network manager");
        }
    }

    public void ValidateConfig()
    {
        if (broadcastIdentifier == "Spaceteam")
        {
            Debug.LogError("#CaptainsMess# You should pick a unique Broadcast Identifier for your game", this);
        }
        if (playerPrefab == null)
        {
            Debug.LogError("#CaptainsMess# Please pick a Player prefab", this);
        }
        if (listener == null)
        {
            Debug.LogError("#CaptainsMess# Please set a Listener object", this);
        }
    }

    public void Update()
    {
        if (networkManager == null)
        {
            networkManager = FindObjectOfType(typeof(CaptainsMessNetworkManager)) as CaptainsMessNetworkManager;
            networkManager.listener = listener;

            if (networkManager.verboseLogging) {
                Debug.Log("#CaptainsMess# !! RECONNECTING !!");
            }
        }
    }

    public List<CaptainsMessPlayer> Players()
    {
        return networkManager.LobbyPlayers();
    }

    public CaptainsMessPlayer LocalPlayer()
    {
        return networkManager.localPlayer;
    }

    public void AutoConnect()
    {
        networkManager.minPlayers = minPlayers;
        networkManager.AutoConnect();
    }

    public void StartHosting()
    {
        networkManager.minPlayers = minPlayers;
        networkManager.StartHosting();
    }

    public void StartJoining()
    {
        networkManager.minPlayers = minPlayers;
        networkManager.StartJoining();
    }

    public void Cancel()
    {
        networkManager.Cancel();
    }

    public bool AreAllPlayersReady()
    {
        return networkManager.AreAllPlayersReady();
    }

    public float CountdownTimer()
    {
        return networkManager.allReadyCountdown;
    }

    public void StartLocalGameForDebugging()
    {
        networkManager.minPlayers = 1;
        networkManager.StartLocalGameForDebugging();
    }

    public bool IsConnected()
    {
        return networkManager.IsConnected();
    }

    public bool IsHost()
    {
        return networkManager.IsHost();
    }
}