/**
 * @file LocalRollbackSimulation.cs
 * @description 
 * @author Alexander Theodore
 * @copyright 2024 All Rights Reserved.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Coherence.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace SpaghettiCode.CoherenceReplays.Runtime
{
    // class used for testing the rollback simulation on a local machine
    public class LocalRollbackSimulation : MonoBehaviour
    {
        public IReplayPlayer Player;
        public ScriptableObject ReplayAsset;
        [field:SerializeField] public bool Play {get; set;}
        [SerializeField] public GameObject LocalClientPrefab;
        public int Frame;
        
        public void Awake()
        {
            InitalizePlayer();
        }

        private void InitalizePlayer()
        {
            try
            {
                Player = ReplaySettings.instance.CreateGenericReplayPlayer(this);
                Player.SetReplay(ReplayAsset as IReplay);
                Player.Play = Play;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create harness, are your replay project settings set correcty? original error: {ex}", this);
                enabled = false;
            }
        }

        public void FixedUpdate()
        {
            if(Player.Play)
            {
                Player.AdvanceFrame();
            }
            Play = Player.Play;
            Frame = Player.Frame;
        }

        /*
        public EventHandler<Replay> OnReplayStarted;
        public long Frame;
        [SerializeField] private long _gameDuration = 1000;
        [SerializeField] private bool _autoReset;
        public bool Play;
        public LocalStateStoreTester localStateStore = new LocalStateStoreTester();
        [SerializeField] private long savedFrame = 1;
        private bool dirty;
        public List<Replay> m_replays;
        private IEnumerator<Replay> _allTests;
        private Replay m_currentReplay;
        [SerializeField] private ReplayMode m_replayMode;
        [SerializeReference, SelectImplementation(typeof(ILocalSimulationTest))] private List<ILocalSimulationTest> m_tests;

        // done to prevent underlying coherence module form attempting to connect to the internet
        new void Start()
        {
            OnStart();
        }

        public void Awake()
        {
            return;
        }

        protected void OnStart()
        {
            bool editor = false;
            #if UNITY_EDITOR
                if(ServiceLocator.MultiplayerService.GetType() != typeof(NullServiceObject))
                {
                    Debug.LogWarning($"disabling replay manager. Reason: in online mode, ");
                    m_replayMode = ReplayMode.None;
                }
                if(!GameManager.Online && m_replayMode != ReplayMode.SyncTest)
                {
                    Debug.LogWarning($"disabling replay manager. Reason: {m_replayMode} is not valid outside of testingmode = online");
                    m_replayMode = ReplayMode.None;
                }
                switch(m_replayMode)
                {
                    case ReplayMode.Play:
                        ServiceLocator.AddMultiplayerService(this);
                    break;
                    case ReplayMode.DebugExactCopy:
                        ServiceLocator.AddMultiplayerService(this);
                    break;
                    case ReplayMode.SyncTest:
                        ServiceLocator.AddMultiplayerService(this);
                        _debugger = new OfflineRuntimeDebugger(this);
                    break;
                    default:
                        gameObject.SetActive(false);
                        return;
                }
                editor = true;
            #endif
            if(!editor)
            {
                gameObject.SetActive(false);
                return;
            }
            PlayerManager.PostTickDebug += OnPostTick;
            StateTester.SampleFailed += OnSampleFailed;
            if(m_replayMode == ReplayMode.Play || m_replayMode == ReplayMode.Play)
            {
                if(m_replays.Count > 0)
                {
                    localGame.SetMultiplayerStatus(true);
                    _allTests = m_replays.GetEnumerator();
                    OnReplayFinished();
                }
                else
                {
                    Play = false;
                }
            }
            else
            {
                Play = false;
                m_replays.Clear();
            }
        }

        public void OnReplayFinished()
        {
            if(_allTests.MoveNext())
            {
                LoadReplay(_allTests.Current);
            }
            else if(_autoReset)
            {
                _allTests.Reset();
                OnReplayFinished();
            }
            else
            {
                Debug.Log("Replays Completed");
                Play = false;   
            }
        }

        public void LoadReplay(Replay replay)
        {
            _gameDuration = replay.Duration;
            m_currentReplay = replay;
            localStateStore = replay.GetStateStoreTester();
            localStateStore.Clear();
            GameManager.LoadReplay(replay);
            _clientControllers = new NetworkedController[2];
            _clientControllers[0] = (LocalRollbackTestController)Data.GetPlayer(0).Inputs;
            _clientControllers[1] = (LocalRollbackTestController)Data.GetPlayer(1).Inputs;
            ServiceLocator.GameSerivce.GameMode.Intro = false;
            ServiceLocator.GameSerivce.StartNewGame(GameStartSettings.Multiplayer);
            GameManager.StartRound();
            Play = true;
            m_currentReplay.OnReplayLoad(this);
            Frame = 0;
            //test ensure that order of hitbox behaviors is not breaking hit logic
            OnReplayStarted?.Invoke(this, replay);
            foreach(var test in m_tests)
            {
                test.OnReplayLoad(this, replay);
            }
        }

        public void FixedUpdate()
        {
            if(Play)
            {
                AdvanceFrame();
            }
            dirty = false;
        }

        public void OnPostTick()
        {
            _debugger.AssertHash(this);
        }

        public void BackFrame()
        {
            RollbackBy(1);
        }

        public void AdvanceFrame()
        {
            if(Frame < _gameDuration)
            {
                if(m_replayMode != ReplayMode.DebugExactCopy)
                {
                    Simulate(Frame);
                    localStateStore.Add(CreateState(), Frame);
                }
                Frame++;
                m_currentReplay.OnReplayUpdate(Frame - 1, this, m_replayMode); 
                foreach(var test in m_tests)
                {
                    test.OnReplayUpdate(Frame - 1, this, m_currentReplay);
                }
            }
            else
            {
                Play = false;
                OnReplayFinished();
            }
        }


        public void RollbackBy(int framesBack)
        {
            long to = Frame - framesBack;
            if(to < 1)
            {
                to = 1;
            }
            RollbackTo(to);
        }

        public void RollbackTo(long to)
        {
            GameSimulationState result;
            if(localStateStore.TryRollback(to, out result))
            {
                Rollback(to, result);
                Debug.Log($"rolled back to frame {result}");
            }
            else
            {
                Debug.LogError($"unable to rollback to {to}", this);
            }
            Frame = to;
        }

        public void SimulateGameState(long to, GameSimulationState state)
        {
            if(localStateStore.TryRollback(to, out GameSimulationState discarded))
            {
                
            }
            Rollback(to, state);
            Frame = to;
        }

        public void ForceLoadState(GameSimulationState previousState)
        {
            try
            {
                Rollback(Frame, previousState);
            }
            catch(NullReferenceException)
            {
                Debug.Log("unable to copy rollback exactly");
                // players
                ServiceLocator.DataService.GetPlayer(0).Instance.ApplyState(previousState.Players[0]);
                ServiceLocator.DataService.GetPlayer(1).Instance.ApplyState(previousState.Players[1]);

                // manager
                localGame.ApplyState(previousState.Manager);
                // data 
                Data.ApplyState(previousState.Data);

                // undoables        
                _undoQueue.RollbackEntries(Frame);

                // debug
                _debugger.AssertRollback(this, previousState);
            }
        }

        public GameSimulationState GetCurrentState()
        {
            try
            {
                return CreateState();
            }
            // fallback create state locally
            catch(NullReferenceException)
            {    
                GameSimulationState currentState = new GameSimulationState(){Players = new PlayerSimulationState[2], Manager = localGame.SaveState(), Data = Data.SaveState(), PostRound = false};
                currentState.Players[0] = ServiceLocator.DataService.GetPlayer(0).Inputs.Player.SaveState();
                currentState.Players[1] = ServiceLocator.DataService.GetPlayer(1).Inputs.Player.SaveState();
                return currentState;
            }
        }

        public void OnSampleFailed(object sender, EventArgs e)
        {
            Play = false;   
        }

        public override void OnDestroy()
        {
            PlayerManager.PostTickDebug -= OnPostTick;
            base.OnDestroy();
        }

        public void OnGUI()
        {
            if(m_replayMode == ReplayMode.SyncTest)
            {
                GUILayout.Window(2, new Rect (Screen.width - 300, 175, 300, 100), SyncTestingWindow, "Sync testing");
            }
            else
            {
                GUILayout.Window(2, new Rect (Screen.width / 2 - 300, Screen.height - 250, 600, 100), DoMyWindow, "Multiplayer playback");
            }
        }

        void SyncTestingWindow(int windowID)
        {
            var syncTester = (_debugger as OfflineRuntimeDebugger).SyncTester;
            if(GUILayout.Button("Enabled?: " + syncTester.Enabled))
            {
                syncTester.Enabled = !syncTester.Enabled;
            }
            GUI.color = syncTester.Failed ? Color.red : Color.green;
            GUILayout.Label("Status: " + (syncTester.Failed ? "Failed" : "Good"));
            GUI.color = Color.white;
        }

        // Make the contents of the window
        void DoMyWindow(int windowID)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Frame: {Frame}");
            if(dirty)
            {
                GUILayout.EndHorizontal();
            return;
            }
            if(Play)
            {
                if(GUILayout.Button("Pause"))
                {
                    Play = false;
                    dirty = true;
                }
            }
            else
            {
                if(GUILayout.Button("Play"))
                {
                    Play = true;
                    dirty = true;
                }
            }
            if(GUILayout.Button("+1"))
            {
                AdvanceFrame();
                    dirty = true;
            }
            if(GUILayout.Button("-1"))
            {
                BackFrame();
                    dirty = true;
            }
            if(GUILayout.Button("+25"))
            {
                for(int i = 0; i < 25; i++)
                {
                    AdvanceFrame();
                    dirty = true;
                }
            }
            if(GUILayout.Button("-25"))
            {
                RollbackBy(25);
                    dirty = true;
            }
            if(GUILayout.Button("Checkpoint"))
            {
                savedFrame = Frame;
                    dirty = true;
            }
            if(GUILayout.Button("Force Failure"))
            {
                ForceSampleFailure();
                    dirty = true;
            }
            if(GUILayout.Button($"LoadCheckpoint {savedFrame}"))
            {
                if(Frame < savedFrame)
                {
                    for(int i = (int)Frame; i < savedFrame; i++)
                    {
                        AdvanceFrame();
                    }
                }
                else
                {
                    RollbackTo(savedFrame);
                }
                    dirty = true;
            }
            if(GUILayout.Button("Reset"))
            {
                RollbackTo(1);
                    dirty = true;
            }
            GUILayout.EndHorizontal();
        }
        
        public enum ReplayMode{None, Play, DebugExactCopy, SyncTest}
        */
    }

    // generic player that is created at runtime via the reflection settings to 'match' the coherence input simulation and control it without an online connection
    public class ReplaySimulationPlayer<TState> : IReplayPlayer
    {
        // public accessors
        public IReplay CurrentReplay => m_replay;
        public int Frame => m_frame;
        public bool Play { get; set; }

        private Replay<TState> m_replay;
        public event EventHandler<ReplayPlayedEvent> Updated;
        // reference to the gameobject that holds this player
        public LocalRollbackSimulation LocalSimulationGameobject;
        private int m_frame;
        // NOTE: replace this type with CoherenceInputSimulation<T> after coherence 2.0.0 only using new class to access protected members
        // reference to the user defined input simulation
        private TEMP_PublicCoherenceInputWrapper<TState> m_simulation;

        public void AttachToHarness(IReplayHarness Other)
        {
            throw new NotImplementedException();
        }

        public ReplaySimulationPlayer(LocalRollbackSimulation context)
        {
            LocalSimulationGameobject = context;
            if(LocalSimulationGameobject.TryGetComponent(out TEMP_PublicCoherenceInputWrapper<TState> validSimuation))
            {
                m_simulation = validSimuation;
            }
            else
            {
                Debug.LogError($"Failed to attach replay player to user-definined coherenceinputsimulation, looking for type {typeof(TEMP_PublicCoherenceInputWrapper<TState>).FullName} on object {LocalSimulationGameobject} but got null\nDoes Replay player share a gameobject with your user-definied coherence input simulation?", context);
            }
        }
        public void SetReplay(IReplay replayAsset)
        {
            var cast = replayAsset as Replay<TState>;
            if(!cast)
            {
                throw new ArgumentException($"asked to play a replay of an unexpected type, wanted {typeof(Replay<TState>).FullName} got {replayAsset?.GetType().Name} instead");
            }
            m_replay = cast;
            m_replay.LoadReplayInSingleplayer(LocalSimulationGameobject.LocalClientPrefab, m_simulation);
        }

        public void AdvanceFrame()
        {
            SetFrame(m_frame + 1);
        }

        public void SetFrame(int frame)
        {
            m_frame = Mathf.Clamp(frame, 0, m_replay.Duration - 1);
            m_simulation.TEMP_Rollback(m_frame, m_replay.States[m_frame].InitalState);
        }
    }

    // this is a temporary class that exposes the protected members of coherence input simulation, remove this with the release of coherence 2.0.0
    public abstract class TEMP_PublicCoherenceInputWrapper<TState> : CoherenceInputSimulation<TState>
    {   
            public void TEMP_Simulate(long simulationFrame)
            {
                Simulate(simulationFrame);
            }

            public void TEMP_Rollback(long toFrame, TState state)
            {
                Rollback(toFrame, state);
            }

            public TState TEMP_CreateState()
            {
                return CreateState();
            }
    }
}