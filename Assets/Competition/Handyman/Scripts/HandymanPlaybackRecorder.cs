using System.Collections.Generic;
using UnityEngine;

namespace SIGVerse.Competition.Handyman
{
	[RequireComponent(typeof (HandymanPlaybackCommon))]
	public class HandymanPlaybackRecorder : TrialPlaybackRecorder
	{
		private string environmentName;

		protected override void Awake()
		{
			this.isRecord = HandymanConfig.Instance.configFileInfo.playbackType == HandymanPlaybackCommon.PlaybackTypeRecord;

			base.Awake();
		}

		protected override List<string> GetDefinitionLines()
		{
			List<string> definitionLines = base.GetDefinitionLines();

			// Environment
			definitionLines.Add(HandymanPlaybackEnvironmentEventController.GetDefinitionLine(this.environmentName));
			
			return definitionLines;
		}


		public bool Initialize(int numberOfTrials)
		{
			string filePath = string.Format(Application.dataPath + HandymanPlaybackCommon.FilePathFormat, numberOfTrials);

			return this.Initialize(filePath);
		}

		public void SetEnvironmentName(string environmentName)
		{
			this.environmentName = environmentName;
		}
	}
}
