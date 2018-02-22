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
			base.isPlay = HandymanConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay;

			base.Awake();

			if (base.isPlay)
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
			}
		}


		public override void OnReadFileButtonClick()
		{
			base.trialNo = int.Parse(base.trialNoInputField.text);

			string filePath = string.Format(Application.dataPath + HandymanPlaybackCommon.FilePathFormat, base.trialNo);

			base.Initialize(filePath);
		}
	}
}

