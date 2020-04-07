﻿using System;
using App.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using App.Events;
using App.Audio;
using App.Utility;
using App.Data;

namespace App.Controllers
{
    public class DanceTogetherGameManager : NetworkBehaviour
    {
        // public delegates
        public event Action GameInitializedEvent;
        public event Action GameBeginEvent;
        public event Action GameEndEvent;
        public event Action GamePostEvent;
        public event Action GameCompleteEvent;
        public event Action<SongGenre> MusicGenreChanged;

        [Header("Sub Managers")]
        [SerializeField]
        private EndGameManager endGameManager;
        [SerializeField]
        private PostGameManager postGameManager;

        //Audio Options
        [Header("Audio Genre Lists")]
        [SerializeField]
        private List<TrackList> availableMusicList;
        public List<TrackList> AvailableMusicList
        {
            get { return availableMusicList; }
        }

        /// <summary>
        /// The Index of Available Music Genres List
        /// A Vector2Int is used. Value X is used for genre selection. Value Y is uesd for track selection.
        /// </summary>
        [SyncVar(hook = "OnGenreChange")]
        public int currentGenreIndex = 0;

        //  Init Var
        private bool isInitialized = false;
        public bool IsInitialized
        {
            get { return isInitialized; }
        }

        public DanceTogetherPlayer LocalPlayer
        {
            get { return Controller.NetworkController.LocalPlayer; }
        }

        // GameEvents ScriptableObjects
        [Header("Game Events")]
        [SerializeField]
        private GameEvent CountDownEvent;
        [SerializeField]
        private GameEvent GameTimeEvent;

        // CountDown Vars
        private float currentCountDownTime;

        private float currentTick = 0f;

        public enum GameStates
        {
            Inactive,
            CountDown,
            Active,
            Ended,
            Post
        }
        /// <summary>
        /// Track the Game State. Game Acts as a state machine.
        /// Note: Certain actions can only occur in certain states.
        /// </summary>
        public GameStates CurrentGameState
        {
            get;
            private set;
        }

        /// <summary>
        /// A list of Player Actively playing the game.
        /// Note : seperated so people may join lobby, while game is active.
        /// Acitve Players is now a method, as UNET doesnt allow for complex classes to sync.
        /// </summary>
        public List<DanceTogetherPlayer> ActivePlayers()
        {
            List<DanceTogetherPlayer> activePlayerList = new List<DanceTogetherPlayer>();

            foreach(DanceTogetherPlayer player in Controller.NetworkController.PlayerList)
            {
                if (player.IsActivePlayer)
                {
                    activePlayerList.Add(player);
                }
            }

            return activePlayerList;
        }

        /// <summary>
        /// Active player data Snap shots are taken for reference, incase a player drops.
        /// </summary>
        private List<PlayerDataSnapShot> activePlayerData = new List<PlayerDataSnapShot>();
        public List<PlayerDataSnapShot> ActivePlayerData
        {
            get { return activePlayerData; }
        }
        public float currentGameTime
        {
            get;
            private set;
        }
        public int roundCount
        {
            get;
            private set;
        }

        //public event Action something
        public MainController Controller
        {
            get;
            private set;
        }

        /// <summary>
        /// Check if all player have selected a match
        /// </summary>
        public bool CheckAllPlayersMatched
        {
            get
            {
                // if no controller set up. false
                if (Controller == null || Controller?.NetworkController == null)
                    return false;

                // if any player checked 
                foreach(DanceTogetherPlayer player in Controller.NetworkController.PlayerList)
                {
                    if (player.IsActivePlayer && player.SelectedMatchSongId != -1) // check only active players if made a partner selection
                        return false;
                }

                return true;

            }
        }

        /// <summary>
        /// As an alternative to the singleton pattern. Call When setting up MainController.
        /// </summary>
        public void Init(MainController _controller)
        {
            if (isInitialized)
            {
                Debug.LogError("Game Manager Has Already Been Initialized.");
                return;
            }

            Controller = _controller;

            if (_controller == null)
            {
                Debug.LogWarning("Game Manager Failed to Init due to null MainController." );
                return;
            }

            isInitialized = true;
            // Init children
            endGameManager?.Init(this);
            postGameManager?.Init(this);
        }
        /// <summary>
        /// Attempt to obtain the selected music track from current genre.
        /// Note: if current index cannot be retrieved, null will return.
        /// </summary>
        /// <param name="_value"></param>
        /// <returns></returns>
        public MusicTrack GetSong(int _value)
        {
            if (availableMusicList[currentGenreIndex]?.MusicTrackList[_value] != null)
            {
                return availableMusicList[currentGenreIndex].MusicTrackList[_value];
            } else
            {
                return null;
            }
        }
        public TrackList GetCurrentTrackList()
        {
            if(availableMusicList[currentGenreIndex] != null)
            {
                return availableMusicList[currentGenreIndex];
            } else
            {
                return null;
            }
        }

        /// <summary>
        /// Clear Active Player Snap Shots.
        /// </summary>
        public void ClearPlayerDataList()
        {
            activePlayerData.Clear();
        }

        /// <summary>
        /// Local Reset Method - offline
        /// </summary>
        public void Reset()
        {
            roundCount = 0;
            currentCountDownTime = 3f;
            CurrentGameState = GameStates.Inactive;
        }

        public PlayerDataSnapShot OtherPlayerWithSongID(int _songId)
        {
            foreach(PlayerDataSnapShot player in activePlayerData)
            {
                if (_songId == player.SongID) // if song id match
                {
                    if (!player.IsLocalPlayer)
                    {
                        Debug.Log("Match found to player with songID : " + player.SongID);
                        return player;
                    }
                }
            }

            Debug.LogWarning("No player found that matches the Local player's songID");
            return null;
        }

        /// <summary>
        /// Used to track and set gameStates
        /// </summary>
        private void Update()
        {
            /// !!! Tick update !!! ///
            currentTick += Time.deltaTime;
            if (currentTick < 0.2f)
            {
                return;
            }
            else
            {
                currentTick = 0f;
            }
            /// !!! End Tick Code !!! ///

            switch (CurrentGameState)
            {
                case GameStates.CountDown:
                    currentCountDownTime -= 0.2f;

                    CountDownEvent?.Raise(currentCountDownTime);

                    //if (isServer){
                    if (currentCountDownTime <= 0f)
                    {
                        CmdBeginGame();
                    }
                    //}
                    break;
                case GameStates.Active:
                    currentGameTime -= 0.2f;

                    GameTimeEvent?.Raise(currentGameTime);

                    //if (isServer){
                        if (currentGameTime <= 0f)
                        {
                            CmdEndGame();
                        }
                    //}
                    break;
                default:
                    /// DO Nothing
                    break;
            }

        }

        /// <summary>
        /// Method to clear out reference list to current Active Players.
        /// </summary>
        private void ClearActivePlayers()
        {
            if (isServer)
            {
                foreach (DanceTogetherPlayer player in Controller.NetworkController.PlayerList)
                {
                    player.CmdSetActive(false);
                    player.CmdClearReady();
                }
            }

            activePlayerData.Clear();
        }

        /// <summary>
        /// Method to go to post state without all clients going as well.
        /// </summary>
        public void LocalGotoPostGame()
        {
            if (!Controller.NetworkController.LocalPlayer.IsActivePlayer)
            {
                Debug.Log("Game Post Called, but you are not an active player");
                return;
            }
            if (CurrentGameState != GameStates.Ended)
            {
                Debug.Log("Game Cannot be sent to post, as It hasnt Ended. : " + CurrentGameState.ToString());
                return;
            }

            // audio stuff
            Controller.AudioController.BeginLobbyMusic();

            CurrentGameState = GameStates.Post;

            if (GamePostEvent != null)
            {
                GamePostEvent();
            }
        }

        #region NetworkBehaviour Commands and Responses
        /// <summary>
        /// network command ChangeMusicGenre method itterates through available genres in a forward progression.
        /// </summary>
        [Command, Server]
        public void CmdChangeMusicGenre()
        {
            if (availableMusicList.Count - 1 > currentGenreIndex)
            {
                currentGenreIndex += 1;
            }
            else if(currentGenreIndex == availableMusicList.Count - 1)
            {
                currentGenreIndex = 0;
            }

            Debug.Log("Genre has switched to : " + GetCurrentTrackList().Genre.name);
        }
        /// <summary>
        /// Begin game Command
        /// </summary>
        [Command, Server]
        public void CmdBeginGame()
        {
            Debug.Log("Command Begin Game Called");
            RpcBeginGame();
        }
        /// <summary>
        /// End Game Command
        /// </summary>
        [Command, Server]
        public void CmdEndGame()
        {
            Debug.Log("Command End Game Called");
            RpcEndGame();
        }
        /// <summary>
        /// Start CountDown to Game Start Command
        /// </summary>
        [Command, Server]
        public void CmdStartMainCountdown()
        {
            if (Controller.NetworkController.CheckAllPlayersReady)
            {
                Debug.Log("All Players Are Ready!. Begin Game!");

                RpcStartMainCountdown();
            }
        }
        /// <summary>
        /// Call Game Post Command
        /// </summary>
        [Command, Server]
        public void CmdPostGame()
        {
            Debug.Log("Command Post Game Called");
            RpcPostGame();
        }
        [Command, Server]
        public void CmdCompleteGame()
        {
            Debug.Log("Command Complete Game Called");
            RpcCompleteGame();
        }
        /// <summary>
        /// Call Reset Info Command
        /// </summary>
        [Command]
        public void CmdResetGameAll()
        {
            Debug.Log("Command Reset Game Info Called");
            RpcResetGameAll();
        }

        // Client Responces
        [ClientRpc]
        void RpcBeginGame()
        {
            if (!Controller.NetworkController.LocalPlayer.IsActivePlayer)
            {
                Debug.Log("Game Begin Called, but you are not an active player");
                return;
            }
            if(CurrentGameState != GameStates.Inactive && CurrentGameState != GameStates.CountDown)
            {
                Debug.Log("Game Cannot Begin unless in Inactive or Countdown state.");
                return;
            }

            // audio stuff
            Controller.AudioController.BeginGameMusic(GetSong(LocalPlayer.SongID), 1f);

            CurrentGameState = GameStates.Active;

            // increment round count.
            roundCount++;
            // set game time to the length noted in CurrentGameType
            currentGameTime = Controller.CurrentGameType.Data.GameTime;

            if (GameBeginEvent != null)
            {
                GameBeginEvent();
            }
        }
        [ClientRpc]
        void RpcEndGame()
        {
            if (!Controller.NetworkController.LocalPlayer.IsActivePlayer)
            {
                Debug.Log("Game End Called, but you are not an active player");
                return;
            }
            if (CurrentGameState != GameStates.Active)
            {
                Debug.Log("Game Cannot be Ended, as It hasnt Started. : " + CurrentGameState.ToString());
                return;
            }

            // Audio stuff
            Controller.AudioController.EndGameMusic();
            Controller.AudioController.PlayFindSFX();

            CurrentGameState = GameStates.Ended;

            if(GameEndEvent != null)
            {
                GameEndEvent();
            }
        }
        [ClientRpc]
        void RpcPostGame()
        {
            if (!Controller.NetworkController.LocalPlayer.IsActivePlayer)
            {
                Debug.Log("Game End Called, but you are not an active player");
                return;
            }
            LocalGotoPostGame();
        }
        [ClientRpc]
        void RpcCompleteGame()
        {
            // audio stuff
            Controller.AudioController.BeginLobbyMusic();

            CurrentGameState = GameStates.Inactive;

            ClearActivePlayers();

            if (GameCompleteEvent != null)
            {
                GameCompleteEvent();
            }
        }
        [ClientRpc]
        void RpcStartMainCountdown()
        {
            // the game can start if Inactive or In post mode.
            if (CurrentGameState != GameStates.Inactive && CurrentGameState != GameStates.Post)
            {
                Debug.Log("Game CountDown cannot be Started : game Already in session. current state : " + CurrentGameState.ToString());
                return;
            }
            ClearActivePlayers();

            // Check If enough players Exist.
            if (Controller.CurrentGameType.Data.MinPlayers > Controller.NetworkController.PlayerList.Count)
            {
                Debug.Log("game Cannot Start without minimum players : " + Controller.CurrentGameType.Data.MinPlayers + " - Has : " + Controller.NetworkController.PlayerList.Count);
                CmdCompleteGame();
                return;
            }

            if (availableMusicList.Count == 0 || availableMusicList[currentGenreIndex] == null)
            {
                Debug.Log("There are no genres listed in GameController. Please Add a genre in inspector.");
                CmdCompleteGame();
                return;
            }

            // attempt to assign song ids to pairs of players..
            if (availableMusicList[currentGenreIndex].MusicTrackList.Count >= Controller.NetworkController.PlayerList.Count / 2)
            {
                Debug.Log("assign!");
                if(isServer)
                    DanceTogetherUtility.AssignPairs(Controller.NetworkController.PlayerList, availableMusicList[currentGenreIndex].MusicTrackList.Count, Controller.CurrentGameType.Data.GroupSize); // assign this only from server command
            }
            else
            {
                Debug.Log("Songs Cannot be paired to Active players, as not enough tracks exist in genre selection. Cancelling Game.");
                CmdCompleteGame();
                return;
            }

            foreach (DanceTogetherPlayer player in Controller.NetworkController.PlayerList)
            {
                if (isServer)
                {
                    player.CmdSetActive(true); // set player to active
                }

                //PlayerDataSnapShot playerSS = new PlayerDataSnapShot(player);
                activePlayerData.Add(player.PlayerSnapShot); // save a ref to the player's snapshot
            }

            // audio stuff
            Controller.AudioController.RoundBegin();
            Controller.AudioController.PlayCountdownSFX();

            // set timer amount
            currentCountDownTime = 3f;

            // Change State
            CurrentGameState = GameStates.CountDown;

            Debug.Log("Response Start Game Countdown Called");

            if (GameInitializedEvent != null)
            {
                GameInitializedEvent();
            }

        }
        [ClientRpc]
        void RpcResetGameAll()
        {
            roundCount = 0;
            currentCountDownTime = 3f;
            CurrentGameState = GameStates.Inactive;
            LocalPlayer.CmdResetPlayer();
        }
        #endregion

        #region Local Responses
        private void OnGenreChange(int _value)
        {
            currentGenreIndex = _value;
            if(MusicGenreChanged != null)
            {
                MusicGenreChanged(GetCurrentTrackList().Genre);
            }
        }
        #endregion

    }
}