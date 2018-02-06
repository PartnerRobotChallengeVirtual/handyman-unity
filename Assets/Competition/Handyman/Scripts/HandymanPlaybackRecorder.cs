using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanPlaybackCommon))]
	public class HandymanPlaybackRecorder : TrialPlaybackRecorder
	{
		void Awake()
		{
			if(HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord)
			{
			}
			else
			{
				this.enabled = false;
			}
		}

		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + HandymanPlaybackCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}
	}
}
