using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanAvatarMotionCommon))]
	public class HandymanAvatarMotionPlayer : WorldPlaybackPlayer
	{
		protected override void Awake()
		{
			this.isPlay = !HandymanConfig.Instance.configFileInfo.isGraspableObjectsPositionRandom;

			base.Awake();
		}

		// Use this for initialization
		protected override void Start()
		{
			base.Start();  // Avatar motion data

			this.transformController.IsRigidbodiesDisable = false;
			this.transformController.IsCollidersDisable   = false;
		}

		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + HandymanAvatarMotionCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}

		public bool IsInitialized()
		{
			return this.isInitialized;
		}

		public bool IsFinished()
		{
			return this.step == Step.Waiting;
		}
	}
}

