using System.Collections.Generic;
using UnityEngine;
using SIGVerse.ToyotaHSR;
using SIGVerse.Common;

namespace SIGVerse.Competition.Handyman
{
	public class HandymanPlaybackCommon : TrialPlaybackCommon
	{
		private const string TagGraspingCandidatesPosition = "GraspingCandidatesPosition";

		// Events
		public const string DataType1EnvironmentInfo = "31";

		public const string FilePathFormat = "/../SIGVerseConfig/Handyman/Playback{0:D2}.dat";

		[HeaderAttribute("Environments")]
		public List<GameObject> environments;

		//---------------------------------------

		protected override void Awake()
		{
			bool isPlayMode = HandymanConfig.Instance.configFileInfo.playbackType == WorldPlaybackCommon.PlaybackTypePlay;

			if(isPlayMode)
			{
				foreach (GameObject environment in this.environments){ environment.SetActive(true); }  // Temporarily activate the environments to initialize the Transform Event.

				// Deactivate the cubes of candidates positions
				GameObject[] graspingCandidatesPositions = GameObject.FindGameObjectsWithTag(TagGraspingCandidatesPosition);

				foreach(GameObject graspingCandidatesPosition in graspingCandidatesPositions)
				{
					graspingCandidatesPosition.SetActive(false);
				}
			}


			base.Awake();

			if(isPlayMode){ foreach(GameObject environment in this.environments){ environment.SetActive(false); } } // Deactivate environments

			// Robot
			Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

			this.targetTransforms.Add(robot);

			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.BaseFootPrintName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.ArmLiftLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.ArmFlexLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.ArmRollLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.WristFlexLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.WristRollLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HeadPanLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HeadTiltLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.TorsoLiftLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HandLProximalLinkName));
			this.targetTransforms.Add(SIGVerseUtils.FindTransformFromChild(robot, HSRCommon.HandRProximalLinkName));
		}
	}
}

