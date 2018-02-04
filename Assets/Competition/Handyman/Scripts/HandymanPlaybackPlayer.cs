using UnityEngine;


namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanPlaybackCommon))]
	public class HandymanPlaybackPlayer : WorldPlaybackPlayer
	{
		void Awake()
		{
			if (HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypePlay)
			{
				Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

				robot.Find("CompetitionScripts").gameObject.SetActive(false);
				robot.Find("RosBridgeScripts")  .gameObject.SetActive(false);

				GameObject mainMenu = GameObject.FindGameObjectWithTag("MainMenu");

				mainMenu.GetComponentInChildren<HandymanScoreManager>().enabled = false;

				foreach(GameObject graspingCandidatePosition in GameObject.FindGameObjectsWithTag("GraspingCandidatesPosition"))
				{
					graspingCandidatePosition.SetActive(false);
				}
			}
			else
			{
				this.enabled = false;
			}
		}

		public bool Initialize()
		{
			string filePath = string.Format(Application.dataPath + HandymanPlaybackCommon.FilePathFormat, 0);

			return this.Initialize(filePath);
		}
	}
}

