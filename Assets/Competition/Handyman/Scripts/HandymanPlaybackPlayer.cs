using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanPlaybackCommon))]
	public class HandymanPlaybackPlayer : TrialPlaybackPlayer
	{
		[HeaderAttribute("Handyman Objects")]
		public HandymanScoreManager scoreManager;

		protected override void Awake()
		{
			this.isPlay = HandymanConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay;

			base.Awake();

			if (this.isPlay)
			{
				Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

//				robot.Find("CompetitionScripts").gameObject.SetActive(false);
				robot.Find("RosBridgeScripts")  .gameObject.SetActive(false);

				Transform moderator = GameObject.FindGameObjectWithTag("Moderator").transform;

				moderator.GetComponent<HandymanModerator>() .enabled = false;
				moderator.GetComponent<HandymanPubMessage>().enabled = false;
				moderator.GetComponent<HandymanSubMessage>().enabled = false;

				this.scoreManager.enabled = false;

				foreach(GameObject graspingCandidatePosition in GameObject.FindGameObjectsWithTag("GraspingCandidatesPosition"))
				{
					graspingCandidatePosition.SetActive(false);
				}

				this.timeLimit = HandymanConfig.Instance.configFileInfo.sessionTimeLimit;
			}
		}


		public override void OnReadFileButtonClick()
		{
			this.trialNo = int.Parse(this.trialNoInputField.text);

			string filePath = string.Format(Application.dataPath + HandymanPlaybackCommon.FilePathFormat, this.trialNo);

			this.Initialize(filePath);
		}
	}
}

