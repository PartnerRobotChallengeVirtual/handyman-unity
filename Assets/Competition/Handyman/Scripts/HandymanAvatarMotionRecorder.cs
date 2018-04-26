using UnityEngine;
using System;

namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanAvatarMotionCommon))]
	public class HandymanAvatarMotionRecorder : WorldPlaybackRecorder
	{
		protected override void Awake()
		{
			this.isRecord = HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom;

			base.Awake();
		}
		
		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + HandymanAvatarMotionCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}
	}
}

