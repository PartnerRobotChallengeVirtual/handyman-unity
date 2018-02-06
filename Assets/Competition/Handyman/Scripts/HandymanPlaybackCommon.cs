using System.Collections.Generic;
using UnityEngine;
using SIGVerse.ToyotaHSR;

namespace SIGVerse.Competition.Handyman
{
	public class HandymanPlaybackCommon : TrialPlaybackCommon
	{
		public const string FilePathFormat = "/../SIGVerseConfig/Handyman/Playback{0:D2}.dat";

		protected override void Awake()
		{
			base.Awake();

			// Robot
			Transform robot = GameObject.FindGameObjectWithTag("Robot").transform;

			this.targetTransforms.Add(robot);

			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.BaseFootPrintName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.ArmLiftLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.ArmFlexLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.ArmRollLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.WristFlexLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.WristRollLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.HeadPanLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.HeadTiltLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.TorsoLiftLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.HandLProximalLinkName));
			this.targetTransforms.Add(HSRCommon.FindGameObjectFromChild(robot, HSRCommon.HandRProximalLinkName));
		}
	}
}

