using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using SIGVerse.Common;
using SIGVerse.ToyotaHSR;
using UnityEngine.UI;

namespace SIGVerse.Competition.Handyman
{
	public enum ModeratorStep
	{
		Initialize,
		WaitForStart,
		TaskStart,
		WaitForIamReady, 
		SendInstruction,
		WaitForRoomReached,
		WaitForDoesNotExist, 
		WaitForObjectGrasped,
		WaitForTaskFinished,
		Judgement,
		WaitForNextTask,
	}

	public class HandymanModerator : MonoBehaviour, IRosMsgReceiveHandler, ITimeIsUpHandler, IGiveUpHandler
	{
		private const int SendingAreYouReadyInterval = 1000;

		private const string MsgAreYouReady          = "Are_you_ready?";
		private const string MsgEnvironment          = "Environment";
		private const string MsgInstruction          = "Instruction";
		private const string MsgCorrectedInstruction = "Corrected_instruction";
		private const string MsgTaskSucceeded        = "Task_succeeded";
		private const string MsgTaskFailed           = "Task_failed";
		private const string MsgMissionComplete      = "Mission_complete";

		private const string MsgIamReady      = "I_am_ready";
		private const string MsgRoomReached   = "Room_reached";
		private const string MsgDoesNotExist  = "Does_not_exist";
		private const string MsgObjectGrasped = "Object_grasped";
		private const string MsgTaskFinished  = "Task_finished";
		private const string MsgGiveUp        = "Give_up";

		private const string ReasonTimeIsUp = "Time_is_up";
		private const string ReasonGiveUp   = MsgGiveUp;

		//-----------------------------

		public List<GameObject> environments;

		public HandymanScoreManager scoreManager;
		public GameObject avatarMotionPlayback;
		public GameObject playbackManager;

		public AudioSource objectCollisionAudioSource;

		//-----------------------------

		private HandymanModeratorTool tool;
		private StepTimer stepTimer;

		private GameObject mainMenu;
		private PanelMainController mainPanelController;

		private string taskMessage;
		private string correctedTaskMessage;

		private bool shouldWaitDoesNotExist;

		private ModeratorStep step;

		private Dictionary<string, bool> receivedMessageMap;

		private bool isAllTaskFinished;
		private string interruptedReason;


		void Awake()
		{
			try
			{
				if(HandymanConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay) { return; }

				this.tool = new HandymanModeratorTool(this);

				this.stepTimer = new StepTimer();

				this.mainMenu = GameObject.FindGameObjectWithTag("MainMenu");
				this.mainPanelController = mainMenu.GetComponent<PanelMainController>();
			}
			catch (Exception exception)
			{
				Debug.LogError(exception);
				SIGVerseLogger.Error(exception.Message);
				SIGVerseLogger.Error(exception.StackTrace);
				this.ApplicationQuitAfter1sec();
			}
		}


		// Use this for initialization
		void Start()
		{
			this.step = ModeratorStep.Initialize;

			this.isAllTaskFinished = false;
			this.interruptedReason = string.Empty;

			List<GameObject> graspables = this.tool.GetGraspables();

			for (int i=0; i<graspables.Count; i++)
			{
				Rigidbody rigidbody = graspables[i].GetComponent<Rigidbody>();

				rigidbody.constraints
					= RigidbodyConstraints.FreezeRotation |
					  RigidbodyConstraints.FreezePositionX |
					  RigidbodyConstraints.FreezePositionZ;

				rigidbody.maxDepenetrationVelocity = 0.5f;

				StartCoroutine(this.tool.LoosenRigidbodyConstraints(rigidbody));
			}
		}


		private void PreProcess()
		{
			this.mainPanelController.SetTrialNumberText(HandymanConfig.Instance.numberOfTrials);

			SIGVerseLogger.Info("##### " + this.mainPanelController.GetTrialNumberText() + " #####");

			this.scoreManager.ResetTimeLeftText();


			this.taskMessage          = this.tool.GetTaskMessage();
			this.correctedTaskMessage = this.tool.GetCorrectedTaskMessage();

			this.shouldWaitDoesNotExist = this.correctedTaskMessage != string.Empty;

			this.mainPanelController.SetTaskMessageText(this.taskMessage);

			SIGVerseLogger.Info(this.taskMessage);


			this.receivedMessageMap = new Dictionary<string, bool>();
			this.receivedMessageMap.Add(MsgIamReady,      false);
			this.receivedMessageMap.Add(MsgRoomReached,   false);
			this.receivedMessageMap.Add(MsgDoesNotExist,  false);
			this.receivedMessageMap.Add(MsgObjectGrasped, false);
			this.receivedMessageMap.Add(MsgTaskFinished,  false);
			this.receivedMessageMap.Add(MsgGiveUp,        false);

			this.tool.InitializePlayback();

			SIGVerseLogger.Info("End of PreProcess:" + HandymanConfig.Instance.numberOfTrials);
		}


		private void PostProcess()
		{
			SIGVerseLogger.Info("Task end");

			if (HandymanConfig.Instance.numberOfTrials == HandymanConfig.Instance.configFileInfo.maxNumberOfTrials)
			{
				this.SendRosMessage(MsgMissionComplete, string.Empty);

				SIGVerseLogger.Info("All tasks finished.");

				StartCoroutine(this.tool.CloseRosConnections());

				this.isAllTaskFinished = true;
			}
			else
			{
				StartCoroutine(this.tool.ClearRosConnections());

				this.step = ModeratorStep.WaitForNextTask;
			}
		}

		// Update is called once per frame
		void Update ()
		{
			try
			{
				if(this.isAllTaskFinished) { return; }

				if(this.interruptedReason!=string.Empty && this.step != ModeratorStep.WaitForNextTask)
				{
					SIGVerseLogger.Info("Failed '" + this.interruptedReason + "'");
					this.SendPanelNotice("Failed\n"+ this.interruptedReason.Replace('_',' '), 100, PanelNoticeStatus.Red);

					if(this.interruptedReason==ReasonTimeIsUp)
					{
						this.tool.AddSpeechQueModerator(ReasonTimeIsUp);
					}
					else if(this.interruptedReason==ReasonGiveUp)
					{
						this.tool.AddSpeechQueHsr(ReasonGiveUp);
					}
					
					this.GoToNextTaskTaskFailed(this.interruptedReason);
				}

				switch (this.step)
				{
					case ModeratorStep.Initialize:
					{
						SIGVerseLogger.Info("Initialize");
						this.PreProcess();
						this.step++;
						break;
					}
					case ModeratorStep.WaitForStart:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 3000))
						{
							if(this.tool.IsPlaybackInitialized() && this.tool.IsConnectedToRos())
							{ 
								this.step++;
							}
						}
						break;
					}
					case ModeratorStep.TaskStart:
					{
						SIGVerseLogger.Info("Task start!");
						this.tool.AddSpeechQueModerator("Task start!");

						this.scoreManager.TaskStart();

						this.tool.StartPlayback();
						this.tool.StartAvatarMotionPlayback();

						this.step++;

						break;
					}
					case ModeratorStep.WaitForIamReady:
					{
						if (this.receivedMessageMap[MsgIamReady])
						{
							this.step++;
							break;
						}

						if (this.stepTimer.IsTimePassed((int)this.step, SendingAreYouReadyInterval))
						{
							this.SendRosMessage(MsgAreYouReady, string.Empty);
							this.SendRosMessage(MsgEnvironment, this.tool.GetEnvironmentName());
						}
						break;
					}
					case ModeratorStep.SendInstruction:
					{
						// Wait for sending tf 
						if (this.stepTimer.IsTimePassed((int)this.step, 1000))
						{
							this.SendRosMessage(MsgInstruction, this.taskMessage);
							this.tool.AddSpeechQueModerator(this.taskMessage, true);

							this.step++;

							SIGVerseLogger.Info("Waiting for '" + MsgRoomReached + "'");
						}
						break;
					}
					case ModeratorStep.WaitForRoomReached:
					{
						if (this.receivedMessageMap[MsgRoomReached])
						{
							// Check for robot position
							bool isSucceeded = this.tool.IsRoomReachedSucceeded();

							if (HandymanConfig.Instance.configFileInfo.isAlwaysGoNext)
							{
								SIGVerseLogger.Warn("!!! DEBUG MODE !!! : always go next step : result=" + isSucceeded);
								isSucceeded = true;
							}

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgRoomReached + "'");
								this.SendPanelNotice("Good", 150, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.RoomReachingSuccess);
								this.tool.AddSpeechQueModeratorGood();
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgRoomReached + "'");
								this.SendPanelNotice("Failed\n" + MsgRoomReached.Replace('_', ' '), 100, PanelNoticeStatus.Red);
								this.scoreManager.AddScore(Score.Type.RoomReachingFailure);
								this.tool.AddSpeechQueModeratorFailed();

								this.GoToNextTaskTaskFailed(MsgRoomReached);

								return;
							}

							this.step++;
							
							if(this.shouldWaitDoesNotExist)
							{
								SIGVerseLogger.Info("Waiting for '" + MsgDoesNotExist + "'");
							}
							else
							{
								SIGVerseLogger.Info("Waiting for '" + MsgObjectGrasped + "'");
							}
						}
						break;
					}
					case ModeratorStep.WaitForDoesNotExist:
					{
						if(!this.shouldWaitDoesNotExist)
						{
							this.step++;
							break;
						}

						if (this.receivedMessageMap[MsgDoesNotExist])
						{
							string detail = "Send a new message";
							SIGVerseLogger.Info("Succeeded '" + MsgDoesNotExist + "'");
							this.SendPanelNotice("Good\n"+detail, 95, PanelNoticeStatus.Green);
							this.scoreManager.AddScore(Score.Type.TargetConfirmationSuccess);
							this.tool.AddSpeechQueModeratorGood();

							this.SendRosMessage(MsgCorrectedInstruction, this.correctedTaskMessage);
							this.mainPanelController.SetTaskMessageText(this.correctedTaskMessage);
							
							this.tool.AddSpeechQueModerator(this.correctedTaskMessage, true);

							this.step++;

							SIGVerseLogger.Info("Waiting for '" + MsgObjectGrasped + "'");
							break;
						}

						if (this.receivedMessageMap[MsgObjectGrasped])
						{
							string detail = "Target doesn't exist";
							SIGVerseLogger.Info("Failed '" + MsgDoesNotExist + "'");
							this.SendPanelNotice("Failed\n"+detail, 90, PanelNoticeStatus.Red);
							this.scoreManager.AddScore(Score.Type.TargetConfirmationFailure);
							this.tool.AddSpeechQueModeratorFailed();

							this.GoToNextTaskTaskFailed(MsgDoesNotExist);

							return;
						}
						break;
					}
					case ModeratorStep.WaitForObjectGrasped:
					{
						if(!this.shouldWaitDoesNotExist)
						{
							if (this.receivedMessageMap[MsgDoesNotExist])
							{
								string detail = "The target exist";
								SIGVerseLogger.Info("Failed '" + MsgObjectGrasped + "'");
								this.SendPanelNotice("Failed\n"+detail, 100, PanelNoticeStatus.Red);
								this.scoreManager.AddScore(Score.Type.GraspingFailure);
								this.tool.AddSpeechQueModeratorFailed();

								this.GoToNextTaskTaskFailed(detail);

								return;
							}
						}

						if (this.receivedMessageMap[MsgObjectGrasped])
						{
							// Check for grasping
							bool isSucceeded = this.tool.IsObjectGraspedSucceeded();

							if (HandymanConfig.Instance.configFileInfo.isAlwaysGoNext)
							{
								SIGVerseLogger.Warn("!!! DEBUG MODE !!! : always go next step : result=" + isSucceeded);
								isSucceeded = true;
							}

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgObjectGrasped + "'");
								this.SendPanelNotice("Good", 150, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.GraspingSuccess);
								this.tool.AddSpeechQueModeratorGood();
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgObjectGrasped + "'");
								this.SendPanelNotice("Failed\n" + MsgObjectGrasped.Replace('_', ' '), 100, PanelNoticeStatus.Red);
								this.scoreManager.AddScore(Score.Type.GraspingFailure);
								this.tool.AddSpeechQueModeratorFailed();

								this.GoToNextTaskTaskFailed(MsgObjectGrasped);

								return;
							}

							this.step++;

							SIGVerseLogger.Info("Waiting for '" + MsgTaskFinished + "'");

							break;
						}
						break;
					}
					case ModeratorStep.WaitForTaskFinished:
					{
						if (this.receivedMessageMap[MsgTaskFinished])
						{
							StartCoroutine(this.tool.UpdatePlacementStatus(this));

							this.step++;
						}
						break;
					}
					case ModeratorStep.Judgement:
					{
						if (this.tool.IsPlacementCheckFinished())
						{
							bool isSucceeded = this.tool.IsPlacementSucceeded();

							if (HandymanConfig.Instance.configFileInfo.isAlwaysGoNext)
							{
								SIGVerseLogger.Warn("!!! DEBUG MODE !!! : always go next step : result=" + isSucceeded);
								isSucceeded = true;
							}

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgTaskFinished + "'");
								this.SendPanelNotice("Succeeded!", 150, PanelNoticeStatus.Green);
								this.scoreManager.AddScore(Score.Type.PlacementSuccess);
								this.tool.AddSpeechQueModerator("Excellent!");

								this.GoToNextTaskTaskSucceeded();
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgTaskFinished + "'");
								this.SendPanelNotice("Failed\n" + MsgTaskFinished.Replace('_', ' '), 100, PanelNoticeStatus.Red);
								this.scoreManager.AddScore(Score.Type.PlacementFailure);
								this.tool.AddSpeechQueModeratorFailed();

								this.GoToNextTaskTaskFailed(MsgTaskFinished);
							}
						}
						break;
					}
					case ModeratorStep.WaitForNextTask:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 5000) && !this.tool.IsSpeaking())
						{
							if(!this.tool.IsPlaybackFinished()) { break; }

							SceneManager.LoadScene(SceneManager.GetActiveScene().name);
						}

						break;
					}
				}

				this.tool.ControlSpeech(this.step==ModeratorStep.WaitForNextTask); // Speech
			}
			catch (Exception exception)
			{
				Debug.LogError(exception);
				SIGVerseLogger.Error(exception.Message);
				SIGVerseLogger.Error(exception.StackTrace);
				this.ApplicationQuitAfter1sec();
			}
		}

		private void ApplicationQuitAfter1sec()
		{
			Thread.Sleep(1000);
			Application.Quit();
		}


		private void GoToNextTaskTaskSucceeded()
		{
			this.GoToNextTask(MsgTaskSucceeded, string.Empty);
		}

		private void GoToNextTaskTaskFailed(string detail)
		{
			this.GoToNextTask(MsgTaskFailed, detail);
		}

		private void GoToNextTask(string message, string detail)
		{
			this.tool.AddSpeechQueModerator("Let's go to the next session");

			this.tool.StopPlayback();

			this.scoreManager.TaskEnd();

			this.SendRosMessage(message, detail);

			this.PostProcess();
		}


		private void SendRosMessage(string message, string detail)
		{
			ExecuteEvents.Execute<IRosMsgSendHandler>
			(
				target: this.gameObject, 
				eventData: null, 
				functor: (reciever, eventData) => reciever.OnSendRosMessage(message, detail)
			);
		}

		private void SendPanelNotice(string message, int fontSize, Color color)
		{
			PanelNoticeStatus noticeStatus = new PanelNoticeStatus(message, fontSize, color, 2.0f);

			// For changing the notice of a panel
			ExecuteEvents.Execute<IPanelNoticeHandler>
			(
				target: this.mainMenu, 
				eventData: null, 
				functor: (reciever, eventData) => reciever.OnPanelNoticeChange(noticeStatus)
			);

			// For recording
			ExecuteEvents.Execute<IPanelNoticeHandler>
			(
				target: this.playbackManager, 
				eventData: null, 
				functor: (reciever, eventData) => reciever.OnPanelNoticeChange(noticeStatus)
			);
		}


		public void OnReceiveRosMessage(RosBridge.handyman.HandymanMsg handymanMsg)
		{
			if(this.receivedMessageMap.ContainsKey(handymanMsg.message))
			{
				// Check message order
				if(handymanMsg.message==MsgIamReady)
				{
					if(this.step!=ModeratorStep.WaitForIamReady) { SIGVerseLogger.Warn("Illegal timing. message : " + handymanMsg.message + ", step="+this.step); return; }
				}

				if(handymanMsg.message==MsgRoomReached)
				{
					if(this.step!=ModeratorStep.WaitForRoomReached) { SIGVerseLogger.Warn("Illegal timing. message : " + handymanMsg.message + ", step="+this.step); return; }
					this.tool.AddSpeechQueHsr("I arrived at the room");
				}

				if(handymanMsg.message==MsgDoesNotExist)
				{
					if(this.step!=ModeratorStep.WaitForDoesNotExist && this.step!=ModeratorStep.WaitForObjectGrasped) { SIGVerseLogger.Warn("Illegal timing. message : " + handymanMsg.message + ", step="+this.step); return; }
					this.tool.AddSpeechQueHsr("I think the target does not exist");
				}

				if(handymanMsg.message==MsgObjectGrasped)
				{
					if(this.step!=ModeratorStep.WaitForDoesNotExist && this.step!=ModeratorStep.WaitForObjectGrasped) { SIGVerseLogger.Warn("Illegal timing. message : " + handymanMsg.message + ", step="+this.step); return; }
					this.tool.AddSpeechQueHsr("I grasped the object");
				}

				if(handymanMsg.message==MsgTaskFinished)
				{
					if(this.step!=ModeratorStep.WaitForTaskFinished) { SIGVerseLogger.Warn("Illegal timing. message : " + handymanMsg.message + ", step="+this.step); return; }
					this.tool.AddSpeechQueHsr(MsgTaskFinished);
				}

				if(handymanMsg.message==MsgGiveUp)
				{
					this.OnGiveUp();
				}

				this.receivedMessageMap[handymanMsg.message] = true;
			}
			else
			{
				SIGVerseLogger.Warn("Received Illegal message : " + handymanMsg.message);
			}
		}


		public void OnTimeIsUp()
		{
			this.interruptedReason = ReasonTimeIsUp;
		}

		public void OnGiveUp()
		{
			if(this.step > ModeratorStep.TaskStart && this.step < ModeratorStep.WaitForNextTask)
			{
				this.interruptedReason = ReasonGiveUp;
			}
			else
			{
				SIGVerseLogger.Warn("It is a timing not allowed to give up.");
			}
		}
	}
}

