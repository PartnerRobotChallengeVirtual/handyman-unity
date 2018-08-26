using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanPlaybackCommon))]
	public class HandymanPlaybackPlayer : TrialPlaybackPlayer
	{
		[HeaderAttribute("Handyman Objects")]
		public HandymanScoreManager scoreManager;

		//---------------------------------------

		protected HandymanPlaybackEnvironmentEventController environmentController;  // Environment

		protected override void Awake()
		{
			this.isPlay = HandymanConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay;

			base.Awake();

			if (this.isPlay)
			{
				Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

//				robot.Find("CompetitionScripts").gameObject.SetActive(false);
				robot.Find("RosBridgeScripts")  .gameObject.SetActive(false);

				Rigidbody[] robotRigidbodies = robot.GetComponentsInChildren<Rigidbody>(true);
				foreach(Rigidbody rigidbody in robotRigidbodies) { rigidbody.isKinematic = true; }


				Transform moderator = GameObject.FindGameObjectWithTag("Moderator").transform;

				moderator.GetComponent<HandymanModerator>() .enabled = false;
				moderator.GetComponent<HandymanPubMessage>().enabled = false;
				moderator.GetComponent<HandymanSubMessage>().enabled = false;

				Rigidbody[] moderatorRigidbodies = moderator.GetComponentsInChildren<Rigidbody>(true);
				foreach(Rigidbody rigidbody in moderatorRigidbodies) { rigidbody.isKinematic = true; }


				this.scoreManager.enabled = false;

				foreach(GameObject graspingCandidatePosition in GameObject.FindGameObjectsWithTag("GraspingCandidatesPosition"))
				{
					graspingCandidatePosition.SetActive(false);
				}

				this.timeLimit = HandymanConfig.Instance.configFileInfo.sessionTimeLimit;
			}
		}


		// Use this for initialization
		protected override void Start()
		{
			base.Start();

			HandymanPlaybackCommon common = this.GetComponent<HandymanPlaybackCommon>();

			this.environmentController = new HandymanPlaybackEnvironmentEventController(common.environments);
		}

		protected override void ReadData(string[] headerArray, string dataStr)
		{
			base.ReadData(headerArray, dataStr);

			this.environmentController.ReadEvents(headerArray, dataStr); // Environment
		}

		protected override void StartInitializing()
		{
			base.StartInitializing();

			this.environmentController.StartInitializingEvents(); // Environment
		}

		public override void OnReadFileButtonClick()
		{
			this.trialNo = int.Parse(this.trialNoInputField.text);

			string filePath = string.Format(Application.dataPath + HandymanPlaybackCommon.FilePathFormat, this.trialNo);

			this.Initialize(filePath);

			this.StartCoroutine(this.ActivateEnvironment());
		}

		private IEnumerator ActivateEnvironment()
		{
			float startTime = Time.time;

			while(this.step != Step.Waiting && (Time.time - startTime) < 30.0f) // Wait at most 30 seconds
			{
				yield return null;
			}

//			Debug.Log("reading file time="+(Time.time - startTime));

			this.environmentController.ExecuteFirstEvent(); // Enable the environment

			base.transformController.ExecuteFirstEvent();  // Initialize transforms
		}
	}
}

