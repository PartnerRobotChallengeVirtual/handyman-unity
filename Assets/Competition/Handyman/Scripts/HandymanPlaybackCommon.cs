using System.Collections.Generic;
using UnityEngine;
using SIGVerse.ToyotaHSR;
using SIGVerse.Common;

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

