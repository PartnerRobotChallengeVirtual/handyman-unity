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
		WaitForIamReady, 
		TaskStart,
		SendInstruction,
		WaitForRoomReached,
		WaitForObjectGrasped,
		WaitForTaskFinished,
		WaitForNextTask,
	}

	public class HandymanModerator : MonoBehaviour, IRosMsgReceiveHandler
	{
		private const int SendingAreYouReadyInterval = 1000;

		private readonly Color GreenColor = new Color(  0/255f, 143/255f, 36/255f, 255/255f);
		private readonly Color RedColor   = new Color(255/255f,   0/255f,  0/255f, 255/255f);

		private const string MsgAreYouReady     = "Are_you_ready?";
		private const string MsgInstruction     = "Instruction";
		private const string MsgTaskSucceeded   = "Task_succeeded";
		private const string MsgTaskFailed      = "Task_failed";
		private const string MsgMissionComplete = "Mission_complete";

		private const string ReasonTimeIsUp = "Time_is_up";
		private const string ReasonGiveUp   = "Give_up";

		private const string MsgIamReady      = "I_am_ready";
		private const string MsgRoomReached   = "Room_reached";
		private const string MsgObjectGrasped = "Object_grasped";
		private const string MsgTaskFinished  = "Task_finished";

		//-----------------------------

		public List<GameObject> environments;

		public GameObject worldPlayback;

		//-----------------------------

		private HandymanModeratorTool tool;
		private StepTimer stepTimer;

		private HandymanMenu  handymanMenu;
		private HandymanScoreManager scoreManager;

		private GameObject targetRoom;
		private string taskMessage;

		private ModeratorStep step;

		private Dictionary<string, bool> receivedMessageMap;

		private bool isAllTaskFinished;
		private string interruptedReason;

		private float noticeHideTime;


		void Awake()
		{
			try
			{
				this.tool = new HandymanModeratorTool(this.environments);

				this.stepTimer = new StepTimer();

				this.tool.InitPlaybackVariables(this.worldPlayback);


				GameObject mainMenu = GameObject.FindGameObjectWithTag("MainMenu");

				this.handymanMenu    = mainMenu.GetComponent<HandymanMenu>();
				this.scoreManager = mainMenu.GetComponent<HandymanScoreManager>();
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
			this.noticeHideTime    = 0.0f;


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
			this.scoreManager.SetChallengeInfoText();

			SIGVerseLogger.Info("##### " + this.scoreManager.GetChallengeInfoText() + " #####");

			this.scoreManager.ResetTimeLeftText();


			this.taskMessage = this.tool.GenerateTaskMessage();

			this.scoreManager.SetTaskMessageText(this.taskMessage);

			Debug.Log(this.taskMessage);

			this.receivedMessageMap = new Dictionary<string, bool>();
			this.receivedMessageMap.Add(MsgIamReady,      false);
			this.receivedMessageMap.Add(MsgRoomReached,   false);
			this.receivedMessageMap.Add(MsgObjectGrasped, false);
			this.receivedMessageMap.Add(MsgTaskFinished,  false);

			this.tool.InitializePlayback();

			SIGVerseLogger.Info("End of PreProcess:" + HandymanConfig.Instance.numberOfTrials);
		}


		private void PostProcess()
		{
			SIGVerseLogger.Info("Task end");

			if (HandymanConfig.Instance.numberOfTrials == HandymanConfig.Instance.configFileInfo.maxNumberOfTrials)
			{
				this.SendRosMessage(MsgMissionComplete, "");

				SIGVerseLogger.Info("All tasks finished.");
				this.isAllTaskFinished = true;
			}
			else
			{
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
					this.ShowNotice("Failed\n"+ interruptedReason.Replace('_',' '), 100, RedColor);
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
							if(!this.tool.IsPlaybackInitialized()) { break; }

							this.step++;
						}

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
							this.SendRosMessage(MsgAreYouReady, "");
						}

						break;
					}
					case ModeratorStep.TaskStart:
					{
						SIGVerseLogger.Info("Task start!");

						this.scoreManager.TaskStart();

						this.tool.StartPlayback();

						this.step++;

						break;
					}
					case ModeratorStep.SendInstruction:
					{
						// Wait for sending tf 
						if (this.stepTimer.IsTimePassed((int)this.step, 1000))
						{
							this.SendRosMessage(MsgInstruction, this.taskMessage);

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
								this.ShowNotice("Good", 150, GreenColor);
								this.scoreManager.AddScore(Score.Type.RoomReachedSuccess);
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgRoomReached + "'");
								this.ShowNotice("Failed\n" + MsgRoomReached.Replace('_', ' '), 100, RedColor);
								this.GoToNextTaskTaskFailed("Failed " + MsgRoomReached);

								return;
							}

							this.step++;

							SIGVerseLogger.Info("Waiting for '" + MsgObjectGrasped + "'");

							break;
						}

						break;
					}
					case ModeratorStep.WaitForObjectGrasped:
					{
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
								this.ShowNotice("Good", 150, GreenColor);
								this.scoreManager.AddScore(Score.Type.ObjectGraspedSuccess);
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgObjectGrasped + "'");
								this.ShowNotice("Failed\n" + MsgObjectGrasped.Replace('_', ' '), 100, RedColor);
								this.GoToNextTaskTaskFailed("Failed " + MsgObjectGrasped);

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
							// Check for robot position
							bool isSucceeded = this.tool.IsTaskFinishedSucceeded(this.transform.position);

							if (HandymanConfig.Instance.configFileInfo.isAlwaysGoNext)
							{
								SIGVerseLogger.Warn("!!! DEBUG MODE !!! : always go next step : result=" + isSucceeded);
								isSucceeded = true;
							}

							if (isSucceeded)
							{
								SIGVerseLogger.Info("Succeeded '" + MsgTaskFinished + "'");
								this.ShowNotice("Succeeded!", 150, GreenColor);
								this.scoreManager.AddScore(Score.Type.ComeBackSuccess);

								this.GoToNextTaskTaskSucceeded();
							}
							else
							{
								SIGVerseLogger.Info("Failed '" + MsgTaskFinished + "'");
								this.ShowNotice("Failed\nCome back", 100, RedColor);
								this.GoToNextTaskTaskFailed("Failed " + MsgTaskFinished);
							}
						}

						break;
					}
					case ModeratorStep.WaitForNextTask:
					{
						if (this.stepTimer.IsTimePassed((int)this.step, 5000))
						{
							if(!this.tool.IsPlaybackFinished()) { break; }

							SceneManager.LoadScene(SceneManager.GetActiveScene().name);
						}

						break;
					}
				}
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

		public void InterruptTimeIsUp()
		{
			this.interruptedReason = HandymanModerator.ReasonTimeIsUp;
		}

		public void InterruptGiveUp()
		{
			this.interruptedReason = HandymanModerator.ReasonGiveUp;
		}

		private void GoToNextTaskTaskSucceeded()
		{
			this.GoToNextTask(MsgTaskSucceeded, "");
		}

		private void GoToNextTaskTaskFailed(string detail)
		{
			this.GoToNextTask(MsgTaskFailed, detail);
		}

		private void GoToNextTask(string message, string detail)
		{
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

		public void OnReceiveRosMessage(ROSBridge.handyman.HandymanMsg handymanMsg)
		{
			if(this.receivedMessageMap.ContainsKey(handymanMsg.message))
			{
				// Check message order
				if(handymanMsg.message==MsgObjectGrasped)
				{
					if(!this.receivedMessageMap[MsgRoomReached]) { return; }
				}

				// Check message order
				if(handymanMsg.message==MsgTaskFinished)
				{
					if(!this.receivedMessageMap[MsgObjectGrasped]) { return; }
				}

				this.receivedMessageMap[handymanMsg.message] = true;
			}
			else
			{
				SIGVerseLogger.Warn("Received Illegal message : " + handymanMsg.message);
			}
		}

		private void ShowNotice(string message, int fontSize, Color color)
		{
			this.handymanMenu.notice.SetActive(true);

			Text noticeText = this.handymanMenu.notice.GetComponentInChildren<Text>();

			noticeText.text     = message;
			noticeText.fontSize = fontSize;
			noticeText.color    = color;

			this.noticeHideTime = UnityEngine.Time.time + 2.0f;

			StartCoroutine(this.HideNotice()); // Hide after 2[s]
		}

		private IEnumerator HideNotice()
		{
			while(UnityEngine.Time.time < this.noticeHideTime)
			{
				yield return null;
			}

			this.handymanMenu.notice.SetActive(false);
		}
	}
}
